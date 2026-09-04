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

            // ElementId is a reference type (not a struct), so PendingCategoryId/PendingTypeId's "?"
            // is just a nullable-reference annotation, not Nullable<T> — no .Value unwrap here
            // (that would instead resolve to ElementId.Value, its long id property, a completely
            // different thing). The null-check above already narrows both to non-null.
            var categoryId = PendingCategoryId;
            var typeId = PendingTypeId;
            var postable = PendingCommand.Value;

            // The Escape that ends any still-running placement command is sent by
            // TaggingPalettePane.TypeButton_Click, NOT here — see the comment there. Revit does not
            // run a raised ExternalEvent while an interactive command is active, so by the time this
            // method runs that command has already ended (from that Escape, or because the user
            // ended it themselves); sending another Escape here would only clear their selection.
            //
            // The switch itself is still deferred to TaggingPaletteModule.OnIdling rather than
            // posted directly from here. Posting a command while the previous one is still winding
            // down is silently ignored — the running tool keeps the type it started with, since
            // SetDefaultFamilyTypeId only affects the *next* fresh start of the tool — and
            // CanPostCommand is no help in spotting that state: a placement command's repeat loop
            // isn't modal, so it keeps reporting true throughout. Waiting for an Idling tick is the
            // reliable signal instead: it guarantees the Escape has been pumped through Revit's
            // message loop first.
            GmsLog.Info($"TaggingPalette: queueing switch to {postable} (type {typeId}).");
            TaggingPaletteModule.PendingSwitchCategoryId = categoryId;
            TaggingPaletteModule.PendingSwitchTypeId = typeId;
            TaggingPaletteModule.PendingSwitchCommand = postable;
        }

        /// <summary>Sets <paramref name="typeId"/> as <paramref name="categoryId"/>'s default family
        /// type and posts <paramref name="commandId"/> — the actual "switch to this type and start
        /// placing it" work, shared by the immediate path above and
        /// <see cref="TaggingPaletteModule.OnIdling"/>'s deferred retry.</summary>
        internal static void ApplyTypeAndPost(UIApplication app, Document doc, ElementId categoryId, ElementId typeId, RevitCommandId commandId)
        {
            // SetDefaultFamilyTypeId writes document state (the "last used type" per category),
            // so it needs a transaction even though nothing else about the model changes.
            using (var t = new Transaction(doc, "Set default tag/component type"))
            {
                t.Start();
                doc.SetDefaultFamilyTypeId(categoryId, typeId);
                t.Commit();
            }

            app.PostCommand(commandId);

            // The click that got us here left Win32 keyboard focus on the pane's WPF button, not
            // Revit's main window. PostCommand doesn't change that, so the *first* Escape the user
            // presses while placing tags is consumed by the docked pane instead of reaching Revit's
            // command loop — the tool only exits on a second Escape. Reclaiming focus for Revit's
            // main window right away (same helper FormClosed handlers use to recover activation
            // after a modeless dialog, see GmsUi.ActivateRevit) means the very next Escape goes
            // straight to the placement command, so a single press exits it as expected.
            GmsUi.ActivateRevit();
        }

        public string GetName() => "GMS Tagging Palette";
    }
}
