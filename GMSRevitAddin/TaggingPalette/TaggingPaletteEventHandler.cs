#nullable enable
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GMSRevitAddin;

namespace GMS.Tools.TaggingPalette
{
    /// <summary>What the pane asked Revit's thread to do — set from the WPF click handler,
    /// consumed by <see cref="TaggingPaletteEventHandler.Execute"/>.</summary>
    public enum TaggingPaletteAction
    {
        ActivateType,
        Refresh
    }

    /// <summary>
    /// The pane is a WPF <c>UserControl</c> hosted by Revit's dockable-pane framework; like the
    /// Detail Item Palette's <c>NavigateToSheetHandler</c>, its click handlers must not touch the
    /// Revit API directly and instead raise this <see cref="ExternalEvent"/>.
    ///
    /// <see cref="TaggingPaletteAction.ActivateType"/> sets the clicked type as the document's
    /// default type for its category (<c>Document.SetDefaultFamilyTypeId</c>), then posts the
    /// native Revit command already resolved for that type by
    /// <see cref="TaggingPaletteModule.ResolveCommand"/> (<c>TagByCategory</c> / <c>Symbol</c> /
    /// <c>PlaceAComponent</c>, depending on the category), so Revit's own tool starts pre-loaded
    /// with that type and stays active for repeated placements.
    ///
    /// <see cref="TaggingPaletteAction.Refresh"/> re-scans the active document's tag/component
    /// families and repopulates the pane; used for the pane's own "Refresh" link, so a family
    /// loaded mid-session doesn't need the pane to be closed and reopened.
    /// </summary>
    public sealed class TaggingPaletteEventHandler : IExternalEventHandler
    {
        internal TaggingPaletteAction Action;
        internal ElementId? PendingTypeId;
        internal ElementId? PendingCategoryId;
        internal PostableCommand? PendingCommand;

        public void Execute(UIApplication app)
        {
            try
            {
                if (Action == TaggingPaletteAction.Refresh)
                {
                    TaggingPaletteModule.Refresh(app);
                    return;
                }

                ActivateType(app);
            }
            catch (System.Exception ex) { GmsLog.Error("TaggingPaletteEventHandler.Execute", ex); }
        }

        private void ActivateType(UIApplication app)
        {
            var doc = app.ActiveUIDocument?.Document;
            if (doc == null || PendingTypeId == null || PendingCategoryId == null || PendingCommand == null) return;

            // SetDefaultFamilyTypeId writes document state (the "last used type" per category),
            // so it needs a transaction even though nothing else about the model changes.
            using (var t = new Transaction(doc, "Set default tag/component type"))
            {
                t.Start();
                doc.SetDefaultFamilyTypeId(PendingCategoryId, PendingTypeId);
                t.Commit();
            }

            var postable = PendingCommand.Value;
            var commandId = RevitCommandId.LookupPostableCommandId(postable);
            if (app.CanPostCommand(commandId))
                app.PostCommand(commandId);
            else
                GmsLog.Info($"TaggingPaletteEventHandler: {postable} is not postable right now (modal state?)");
        }

        public string GetName() => "GMS Tagging Palette";
    }
}
