using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.UI.Selection;
using GMSRevitAddin;

namespace EditDetailItemTag
{
    /// <summary>One editable parameter shown in <see cref="EditAnnotationInstanceForm"/>: its name,
    /// current UI-formatted value, and whether it lives on the Detail Item's type (shared by every
    /// instance of that type) rather than the instance itself — <see cref="IsTypeParameter"/> tells
    /// <see cref="EditDetailItemTagCommand.EditTaggedDetailItem"/> which element to write an edit
    /// back to.</summary>
    public class EditableParameter
    {
        public string Name;
        public string Value;
        public bool IsTypeParameter;
    }

    /// <summary>
    /// Ribbon-registered "Edit Annotation" command. Runs a pick tool restricted to Detail Item Tag
    /// elements (<see cref="Selection.PickObject"/> + <see cref="DetailItemTagSelectionFilter"/>) —
    /// click the ribbon button, then click one Detail Item Tag in the view — resolves the tag to the
    /// Detail Item it's actually tagging, and opens a modal dialog to edit *that* element's
    /// parameters (not the tag family's own parameters — the tag is only how the target is picked).
    /// The same edit flow (<see cref="EditTaggedDetailItem"/>) is also run automatically whenever
    /// the active selection becomes exactly one Detail Item Tag — see
    /// <see cref="EditDetailItemTagModule"/> — so a plain click-select is enough on its own.
    ///
    /// Revit's own Options → User Interface → Double-Click Options… dialog was the original plan
    /// for a true double-click trigger, but it turns out that dialog has no per-category hook for
    /// family/tag instances — its only relevant entry is a single global "Family" category covering
    /// every loadable-family-based element in the model. Binding that to this command would also
    /// replace Revit's default "Edit Family" double-click for every other family category — too
    /// broad a trade-off — so this uses an explicit pick tool (plus the selection-driven
    /// auto-trigger) instead, with no Revit settings changes required.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class EditDetailItemTagCommand : IExternalCommand
    {
        private const string KindLabel = "Detail Item";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIDocument uidoc = commandData.Application.ActiveUIDocument;
            Document doc = uidoc.Document;

            try
            {
                Reference pickedRef = uidoc.Selection.PickObject(ObjectType.Element,
                    new DetailItemTagSelectionFilter(), "Select a Detail Item Tag to edit");

                return EditTaggedDetailItem(doc, doc.GetElement(pickedRef.ElementId));
            }
            catch (OperationCanceledException)
            {
                return Result.Cancelled; // user pressed Esc / right-click-cancelled the pick
            }
            catch (Exception ex)
            {
                GmsLog.Error("EditDetailItemTagCommand.Execute", ex);
                GmsUi.ShowError(ex, "Edit Annotation");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Shared by both entry points: the ribbon/pick-tool <see cref="Execute"/> above, and
        /// <see cref="EditDetailItemTagModule"/>'s selection-driven auto-trigger.
        /// <paramref name="tagElement"/> is the Detail Item Tag the user picked/selected — this
        /// resolves it to the Detail Item it tags (<see cref="ResolveTaggedElement"/>) and shows the
        /// edit dialog for *that* element, writing any edits back to it (not the tag) inside a
        /// Transaction on Save. No-ops (returns <see cref="Result.Succeeded"/> without showing
        /// anything) if <paramref name="tagElement"/> isn't actually a Detail Item Tag, or has no
        /// tagged element to resolve to.
        /// </summary>
        internal static Result EditTaggedDetailItem(Document doc, Element tagElement)
        {
            if (tagElement?.Category == null || tagElement.Category.Id.Value != (long)BuiltInCategory.OST_DetailComponentTags)
                return Result.Succeeded;

            Element detailItem = ResolveTaggedElement(doc, tagElement);
            if (detailItem == null)
            {
                GmsLog.Warn("EditDetailItemTagCommand: tag " + tagElement.Id + " has no tagged element to edit");
                return Result.Succeeded;
            }

            Element detailType = doc.GetElement(detailItem.GetTypeId());
            string typeName = detailType?.Name ?? "";

            List<EditableParameter> editableParams = CollectEditableParameters(detailItem, isTypeParameter: false);
            if (detailType != null)
                editableParams.AddRange(CollectEditableParameters(detailType, isTypeParameter: true));
            editableParams = editableParams
                .OrderBy(p => p.IsTypeParameter)
                .ThenBy(p => p.Name, StringComparer.OrdinalIgnoreCase)
                .ToList();

            using (EditAnnotationInstanceForm form = new EditAnnotationInstanceForm(typeName, KindLabel, editableParams))
            {
                if (form.ShowDialog(GmsUi.Owner) != DialogResult.OK) return Result.Succeeded;

                List<string> failedNames = new List<string>();
                using (Transaction t = new Transaction(doc, "Edit " + KindLabel + " Parameters"))
                {
                    t.Start();
                    foreach (EditableParameter edit in form.EditedValues)
                    {
                        Element target = edit.IsTypeParameter ? detailType : detailItem;
                        // Always written back in full uppercase, regardless of how it was typed.
                        string upperValue = (edit.Value ?? string.Empty).ToUpperInvariant();
                        if (!RevitParameterHelper.TrySetValueString(target, edit.Name, upperValue))
                            failedNames.Add(edit.Name);
                    }
                    t.Commit();
                }

                // A per-parameter write failure used to be silent (only logged) — surface it so it's
                // obvious at Save time instead of only discoverable later in the GmsLog file.
                if (failedNames.Count > 0)
                {
                    GmsUi.ShowWarning(
                        "The following parameter(s) could not be saved:\n" + string.Join("\n", failedNames) +
                        "\n\nSee the GMS log for details.",
                        "Edit " + KindLabel);
                }
            }

            return Result.Succeeded;
        }

        /// <summary>The element a Detail Item Tag is tagging, in the host document (a tag can in
        /// principle reference more than one element; this edits the first and logs when there were
        /// others). Returns null if <paramref name="tagElement"/> isn't an <see cref="IndependentTag"/>
        /// or references nothing in this document (e.g. it only tags an element in a link).</summary>
        private static Element ResolveTaggedElement(Document doc, Element tagElement)
        {
            if (!(tagElement is IndependentTag tag)) return null;

            ICollection<ElementId> taggedIds = tag.GetTaggedLocalElementIds();
            if (taggedIds == null || taggedIds.Count == 0) return null;

            if (taggedIds.Count > 1)
                GmsLog.Info("EditDetailItemTagCommand: tag " + tagElement.Id + " references " +
                    taggedIds.Count + " elements; editing the first.");

            return doc.GetElement(taggedIds.First());
        }

        // Only parameters whose name begins with this are shown/edited — everything else on the
        // Detail Item (and its type) is left out of the dialog entirely.
        private const string ParameterNamePrefix = "Component";

        /// <summary>Parameters on <paramref name="el"/> (an instance or its type — see
        /// <paramref name="isTypeParameter"/>) that can be shown/edited: name starts with
        /// <see cref="ParameterNamePrefix"/>, writable, and not an ElementId (which would need a
        /// picker UI, out of scope here). Each result's current value is read now so the dialog can
        /// pre-populate its grid with it (blank if the parameter has never been set).</summary>
        private static List<EditableParameter> CollectEditableParameters(Element el, bool isTypeParameter)
        {
            List<EditableParameter> result = new List<EditableParameter>();
            foreach (Parameter p in el.Parameters)
            {
                if (p.IsReadOnly || p.StorageType == StorageType.ElementId || p.StorageType == StorageType.None)
                    continue;

                string name = p.Definition?.Name;
                if (string.IsNullOrEmpty(name) || !name.StartsWith(ParameterNamePrefix, StringComparison.OrdinalIgnoreCase))
                    continue;

                string value;
                try { value = p.AsValueString() ?? p.AsString() ?? string.Empty; }
                catch (Exception ex) { GmsLog.Error("EditDetailItemTagCommand.CollectEditableParameters: '" + name + "'", ex); continue; }

                result.Add(new EditableParameter { Name = name, Value = value, IsTypeParameter = isTypeParameter });
            }
            return result;
        }
    }

    /// <summary>Restricts <c>Selection.PickObject</c> in <see cref="EditDetailItemTagCommand"/> to
    /// Detail Item Tag elements, so the pick cursor only lights up (and only accepts) elements this
    /// command can actually do something with.</summary>
    internal class DetailItemTagSelectionFilter : ISelectionFilter
    {
        public bool AllowElement(Element elem)
        {
            return elem.Category != null && elem.Category.Id.Value == (long)BuiltInCategory.OST_DetailComponentTags;
        }

        public bool AllowReference(Reference reference, XYZ position) => false;
    }

    /// <summary>
    /// Watches the active selection and automatically runs <see cref="EditDetailItemTagCommand"/>'s
    /// edit dialog whenever the selection becomes exactly one Detail Item Tag — resolving it to the
    /// Detail Item it tags and editing that element's parameters — so editing a tagged Detail Item
    /// needs only a normal click-select of its tag, no ribbon button/pick tool required.
    ///
    /// Only fires for a single-element selection: a multi-element selection that happens to include
    /// a Detail Item Tag (box-select, Tab-select, another tool's pick, etc.) is left alone, so a
    /// broader selection made for some other purpose never gets interrupted by this dialog.
    ///
    /// Subscribed once, at the first Idling tick (<c>UIApplication</c> isn't available at
    /// <c>OnStartup</c>), and unsubscribed at shutdown — same idiom as
    /// <c>GMS.Tools.DetailItemPalette.PaletteModule</c>'s <c>SelectionChanged</c> wiring; see
    /// <c>GMS_tools.cs</c>'s <c>OnFirstIdle_EditDetailItemTag</c>.
    ///
    /// <c>OnSelectionChanged</c> itself never starts a Transaction — Revit does not allow modifying
    /// the document from directly inside a <c>SelectionChanged</c> handler (attempting to throws,
    /// and since this handler's own catch just logs and swallows it, the previous version of this
    /// class silently failed to save any edit made through the auto-trigger path: the dialog opened
    /// and closed normally, but the write never happened). Like <c>DetailItemPalette</c>/
    /// <c>TaggingPalette</c>/<c>CycleWorkSets</c>, actual document edits are deferred to an
    /// <see cref="ExternalEvent"/> (<see cref="EditDetailItemTagEventHandler"/>), which runs in a
    /// valid API context.
    /// </summary>
    public static class EditDetailItemTagModule
    {
        private static bool _subscribed;
        private static UIApplication _uiApp;
        private static ExternalEvent _event;
        private static EditDetailItemTagEventHandler _handler;

        public static void SubscribeSelectionChanged(UIApplication uiApp)
        {
            if (_subscribed) return;
            _subscribed = true;
            _uiApp = uiApp;
            _handler = new EditDetailItemTagEventHandler();
            _event = ExternalEvent.Create(_handler);
            uiApp.SelectionChanged += OnSelectionChanged;
            GmsLog.Info("EditDetailItemTagModule: SelectionChanged subscribed");
        }

        public static void UnsubscribeSelectionChanged(UIApplication uiApp)
        {
            if (!_subscribed) return;
            _subscribed = false;
            uiApp.SelectionChanged -= OnSelectionChanged;
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Prefer the stored UIApplication over the sender cast — Revit does not guarantee
                // sender is the UIApplication instance in all versions (same caution as
                // PaletteModule.OnSelectionChanged).
                UIApplication uiApp = _uiApp ?? sender as UIApplication;
                UIDocument uiDoc = uiApp?.ActiveUIDocument;
                Document doc = uiDoc?.Document;
                if (doc == null) return;

                ICollection<ElementId> ids = uiDoc.Selection.GetElementIds();
                if (ids.Count != 1) return; // only auto-trigger for a clean single-element selection

                Element el = doc.GetElement(ids.First());
                if (el?.Category == null || el.Category.Id.Value != (long)BuiltInCategory.OST_DetailComponentTags)
                    return;

                if (_handler == null || _event == null) return; // not subscribed yet — shouldn't happen
                _handler.TagElementId = el.Id;
                _event.Raise();
            }
            catch (Exception ex) { GmsLog.Error("EditDetailItemTagModule.OnSelectionChanged", ex); }
        }
    }

    /// <summary>Runs <see cref="EditDetailItemTagCommand.EditTaggedDetailItem"/> in a valid API
    /// context on behalf of <see cref="EditDetailItemTagModule.OnSelectionChanged"/>, which cannot
    /// start a Transaction itself. <see cref="TagElementId"/> is set by the caller immediately
    /// before <c>Raise()</c>.</summary>
    internal class EditDetailItemTagEventHandler : IExternalEventHandler
    {
        public ElementId TagElementId;

        public void Execute(UIApplication app)
        {
            try
            {
                Document doc = app.ActiveUIDocument?.Document;
                if (doc == null || TagElementId == null) return;

                Element tagElement = doc.GetElement(TagElementId);
                EditDetailItemTagCommand.EditTaggedDetailItem(doc, tagElement);
            }
            catch (Exception ex) { GmsLog.Error("EditDetailItemTagEventHandler.Execute", ex); }
        }

        public string GetName() => "Edit Detail Item Tag (selection-driven)";
    }
}
