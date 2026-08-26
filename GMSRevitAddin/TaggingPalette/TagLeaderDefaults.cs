#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using GMSRevitAddin;

namespace GMS.Tools.TaggingPalette
{
    /// <summary>
    /// Defaults every newly placed "Detail Item Tags" / "Generic Model Tags" tag's Leader Type to
    /// <c>Free End</c>, regardless of how it was placed — this palette's Tag by Category button,
    /// the native Annotate/GMS ribbon, Tag All, copy/paste, etc. There is no public Revit API to
    /// pre-seed the interactive Tag tool's Options Bar "Leader Type" default the way
    /// <c>Document.SetDefaultFamilyTypeId</c> pre-seeds the *type* (see
    /// <see cref="TaggingPaletteEventHandler"/>), so this instead corrects each new tag's
    /// <see cref="IndependentTag.LeaderEndCondition"/> right after Revit creates it. Only touches
    /// the leader's end condition, not whether a leader is shown at all (<c>HasLeader</c> is left
    /// alone — whatever the user chose on the Options Bar).
    ///
    /// Split across two application-level events, mirroring the existing
    /// <c>DuplicateViewOptions.RevitStartup</c> idiom: <see cref="ControlledApplication_DocumentChanged"/>
    /// only caches the new tag ids (modifying the document directly from inside DocumentChanged is
    /// unsupported), and a persistent <see cref="OnIdling"/> handler applies the actual change on
    /// the next idle tick, in its own transaction.
    /// </summary>
    public static class TagLeaderDefaults
    {
        private static readonly HashSet<BuiltInCategory> TargetCategories = new()
        {
            BuiltInCategory.OST_DetailComponentTags,
            BuiltInCategory.OST_GenericModelTags
        };

        private static readonly List<(Document Doc, ElementId Id)> _pendingTags = new();

        public static void ControlledApplication_DocumentChanged(object sender, DocumentChangedEventArgs e)
        {
            try
            {
                var doc = e.GetDocument();

                var addedTagIds = new FilteredElementCollector(doc, e.GetAddedElementIds())
                    .OfClass(typeof(IndependentTag))
                    .Cast<IndependentTag>()
                    .Where(tag => tag.Category != null && TargetCategories.Contains((BuiltInCategory)tag.Category.Id.Value))
                    .Select(tag => tag.Id);

                foreach (var id in addedTagIds)
                    _pendingTags.Add((doc, id));
            }
            catch (System.Exception ex) { GmsLog.Error("TagLeaderDefaults.DocumentChanged", ex); }
        }

        public static void OnIdling(object sender, IdlingEventArgs e)
        {
            if (_pendingTags.Count == 0) return;

            var pending = _pendingTags.ToList();
            _pendingTags.Clear();

            foreach (var group in pending.GroupBy(p => p.Doc))
            {
                var doc = group.Key;
                if (doc == null || !doc.IsValidObject) continue;

                try
                {
                    using (var t = new Transaction(doc, "Default tag leader type to Free End"))
                    {
                        t.Start();
                        foreach (var (_, id) in group)
                        {
                            if (doc.GetElement(id) is not IndependentTag tag) continue;
                            if (tag.CanLeaderEndConditionBeAssigned(LeaderEndCondition.Free))
                                tag.LeaderEndCondition = LeaderEndCondition.Free;
                        }
                        t.Commit();
                    }
                }
                catch (System.Exception ex) { GmsLog.Error("TagLeaderDefaults.OnIdling", ex); }
            }
        }
    }
}
