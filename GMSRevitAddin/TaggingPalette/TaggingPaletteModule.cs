#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using GMSRevitAddin;

namespace GMS.Tools.TaggingPalette
{
    /// <summary>
    /// Owns all Tagging Palette coordination: the dockable pane and the ExternalEvent used to
    /// activate a type / refresh from the pane's WPF click handlers. Mirrors
    /// <c>DetailItemPalette.PaletteModule</c>'s shape and idioms. All entry points are idempotent.
    /// </summary>
    public static class TaggingPaletteModule
    {
        public static readonly DockablePaneId PaneId =
            new DockablePaneId(new Guid("B17645EC-E55A-44CF-951C-525A8AF5FDFB"));

        internal static TaggingPalettePane? PaneInstance;
        internal static TaggingPaletteEventHandler? EventHandler;
        internal static ExternalEvent? Event;

        private static bool _eventsInitialized;

        // Set once the user explicitly opens the palette via ShowPaletteCommand this session.
        // OnIdling's force-hide check below stops acting once this is true — see
        // MarkUserRequestedShow. (Same fix as DetailItemPalette.PaletteModule's.)
        private static bool _userRequestedShow;

        /// <summary>Marks that the user explicitly asked to open the palette (via
        /// <see cref="ShowPaletteCommand"/>). Stops <see cref="OnIdling"/> from closing the pane
        /// again for the rest of this Revit session.</summary>
        public static void MarkUserRequestedShow() => _userRequestedShow = true;

        // Last-scanned groups, cached so a view switch can re-evaluate IsEnabled (which categories
        // have anything to tag in the now-active view) without re-scanning the whole document.
        private static List<TagFamilyGroup> _lastDetailItemGroups = new();
        private static List<TagFamilyGroup> _lastGenericModelGroups = new();
        private static List<TagFamilyGroup> _lastUnitPieceGroups = new();

        /// <summary>Creates and registers the dockable pane. Call from OnStartup.</summary>
        public static void RegisterPane(UIControlledApplication application)
        {
            PaneInstance = new TaggingPalettePane();
            application.RegisterDockablePane(PaneId, "Tagging Palette", PaneInstance);
        }

        /// <summary>Creates the ExternalEvent, wires the pane, and applies the current theme.
        /// Safe to call repeatedly — only the first call does anything.</summary>
        public static void EnsureEvents(UIApplication uiApp)
        {
            if (_eventsInitialized) return;
            _eventsInitialized = true;

            EventHandler = new TaggingPaletteEventHandler();
            Event = ExternalEvent.Create(EventHandler);
            PaneInstance?.SetExternalEvent(Event, EventHandler);
            GmsLog.Info("TaggingPaletteModule: ExternalEvent created");

            if (PaneInstance != null)
            {
                var theme = UIThemeManager.CurrentTheme;
                PaneInstance.Dispatcher.Invoke(() => PaneInstance.ApplyTheme(theme));
            }
        }

        /// <summary>Re-applies Revit's current theme to the pane, so a light/dark switch made
        /// during the session is reflected without restarting Revit.</summary>
        public static void RefreshTheme()
        {
            if (PaneInstance != null)
                PaneInstance.Dispatcher.Invoke(() => PaneInstance.ApplyCurrentTheme());
        }

        /// <summary>Re-scans the active document's tag/component families and repopulates all
        /// three panels. Must be called from a valid Revit API context (an IExternalCommand's
        /// Execute, an application-level event, or this module's own ExternalEvent) — never
        /// directly from a WPF click handler.</summary>
        public static void Refresh(UIApplication uiApp)
        {
            var doc = uiApp.ActiveUIDocument?.Document;
            if (doc == null || PaneInstance == null) return;

            var detailItemTags = CollectByCategory(doc, BuiltInCategory.OST_DetailComponentTags, requiresModelView: false);
            var genericModelTags = CollectByCategory(doc, BuiltInCategory.OST_GenericModelTags, requiresModelView: true);

            // "Unit / Piece Tags" mixes a real Tag category (Curtain Wall Panel Tags, a model category —
            // nothing to tag in a Drafting View) with "GAIT - Piece Tag", a plain Generic Annotation
            // family placed via the Symbol tool, not tied to model geometry — see ResolveCommand.
            var curtainPanelTags = CollectByCategory(doc, BuiltInCategory.OST_CurtainWallPanelTags, requiresModelView: true);
            var pieceTagFamily = CollectByFamilyName(doc, "GAIT - Piece Tag", requiresModelView: false);
            var unitPieces = curtainPanelTags.Concat(pieceTagFamily)
                .OrderBy(g => g.FamilyName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            GmsLog.Info($"TaggingPaletteModule.Refresh: {detailItemTags.Count} detail-item-tag family(ies), " +
                        $"{genericModelTags.Count} generic-model-tag family(ies), {unitPieces.Count} unit/piece family(ies)");

            _lastDetailItemGroups = detailItemTags;
            _lastGenericModelGroups = genericModelTags;
            _lastUnitPieceGroups = unitPieces;
            ApplyEnablement(uiApp);

            PaneInstance.Dispatcher.Invoke(() =>
                PaneInstance.LoadGroups(detailItemTags, genericModelTags, unitPieces));
        }

        // Tracks the last active view seen by OnIdling, so enablement is only recomputed when the
        // view actually changed (comparing .Value, not the ElementId itself, matching the rest of
        // the codebase's ElementId-comparison idiom).
        private static long? _lastActiveViewIdValue;

        /// <summary>Re-evaluates which buttons are enabled for the now-active view (a Drafting
        /// View can't show model geometry, so any type that tags a real model category has nothing
        /// to tag there) and pushes the change straight to the already-bound buttons via
        /// <c>INotifyPropertyChanged</c> — no re-scan, no ItemsSource reassignment needed. Called
        /// from <see cref="OnIdling"/>, and at the end of <see cref="Refresh"/> so a freshly-scanned
        /// list starts with correct enablement.</summary>
        public static void ApplyEnablement(UIApplication uiApp)
        {
            bool isDraftingView = uiApp.ActiveUIDocument?.Document?.ActiveView?.ViewType == ViewType.DraftingView;
            const string reason = "Not available in a Drafting View — it has no model elements to tag.";

            foreach (var group in _lastDetailItemGroups.Concat(_lastGenericModelGroups).Concat(_lastUnitPieceGroups))
            {
                foreach (var type in group.Types)
                {
                    bool enabled = !(type.RequiresModelView && isDraftingView);
                    type.IsEnabled = enabled;
                    type.DisabledReason = enabled ? null : reason;
                }
            }
        }

        /// <summary>
        /// Wired to a persistent <c>UIControlledApplication.Idling</c> subscription (NOT
        /// <c>ViewActivated</c> — that event only fires when the top-level active view changes,
        /// e.g. switching view tabs or opening a view from the browser; it does NOT fire when a
        /// view is "activated" inside a sheet by double-clicking a viewport, a known Revit API gap
        /// — so it silently missed exactly the sheet-workflow case this feature needs). Idling
        /// fires very frequently but the check is cheap (one nullable-long comparison) and skips
        /// everything else — the real work only runs when the pane is shown AND the active view id
        /// actually changed since the last tick. Also does one unrelated job on every call: forcing
        /// the pane closed if Revit auto-reopened it from persisted session state (see the comment
        /// inline below).
        /// </summary>
        public static void OnIdling(object sender, IdlingEventArgs e)
        {
            try
            {
                if (sender is not UIApplication uiApp) return;

                DockablePane pane;
                try { pane = uiApp.GetDockablePane(PaneId); }
                catch (Autodesk.Revit.Exceptions.ArgumentException) { return; }

                // Revit persists a dockable pane's shown/hidden state across sessions, keyed by
                // AddInId + DockablePaneId — so if the palette was left open when Revit last
                // closed, Revit re-opens it itself as soon as a document loads, before the user
                // has asked for it this session. Keep forcing it closed on every tick — not just
                // the first — until the user explicitly opens it via the ribbon button
                // (ShowPaletteCommand, which sets _userRequestedShow): a single check on the first
                // Idling tick isn't reliable, because Revit's own restore-from-persisted-state can
                // land on a later tick than the add-in's first observed one, after a one-shot check
                // has already given up watching. Once the user has asked for it, this stops acting
                // for the rest of the session — see _userRequestedShow.
                if (!_userRequestedShow && pane.IsShown())
                {
                    try { pane.Hide(); }
                    catch (System.Exception ex) { GmsLog.Error("TaggingPaletteModule.ForceInitialHide", ex); }
                }

                if (!pane.IsShown()) return;

                long? activeViewIdValue = uiApp.ActiveUIDocument?.Document?.ActiveView?.Id.Value;
                if (activeViewIdValue == _lastActiveViewIdValue) return;

                _lastActiveViewIdValue = activeViewIdValue;
                ApplyEnablement(uiApp);
            }
            catch (System.Exception ex) { GmsLog.Error("TaggingPaletteModule.OnIdling", ex); }
        }

        private static List<TagFamilyGroup> CollectByCategory(Document doc, BuiltInCategory bic, bool requiresModelView)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(bic)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .GroupBy(fs => fs.Family?.Name ?? "(unnamed)")
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase)
                .Select(g => new TagFamilyGroup
                {
                    FamilyName = g.Key,
                    Types = ToTypeButtons(g, requiresModelView)
                })
                .ToList();
        }

        private static List<TagFamilyGroup> CollectByFamilyName(Document doc, string familyName, bool requiresModelView)
        {
            var symbols = new FilteredElementCollector(doc)
                .OfClass(typeof(FamilySymbol))
                .Cast<FamilySymbol>()
                .Where(fs => string.Equals(fs.Family?.Name, familyName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (symbols.Count == 0) return new List<TagFamilyGroup>();

            return new List<TagFamilyGroup>
            {
                new TagFamilyGroup { FamilyName = familyName, Types = ToTypeButtons(symbols, requiresModelView) }
            };
        }

        private static List<TagTypeButton> ToTypeButtons(IEnumerable<FamilySymbol> symbols, bool requiresModelView) =>
            symbols
                .OrderBy(fs => fs.Name, StringComparer.OrdinalIgnoreCase)
                .Select(fs => new TagTypeButton
                {
                    TypeName = fs.Name,
                    TypeId = fs.Id,
                    CategoryId = fs.Category?.Id ?? ElementId.InvalidElementId,
                    TargetCommand = ResolveCommand(fs.Category),
                    RequiresModelView = requiresModelView
                })
                .ToList();

        /// <summary>
        /// Which native Revit command places a given category's types. Three cases, not two —
        /// a real Tag category (e.g. "Detail Item Tags") uses Tag by Category; a plain Generic
        /// Annotation family like "GAIT - Piece Tag" is a 2D view-specific symbol, placed via the
        /// *Symbol* tool ("Places a 2D annotation drawing symbol in the current view") — NOT Place
        /// a Component, which only places elements "in the building model" and silently does
        /// nothing useful for an annotation-only family; anything else falls back to Place a
        /// Component for genuine model-category families.
        /// </summary>
        internal static PostableCommand ResolveCommand(Category? category)
        {
            if (category == null) return PostableCommand.PlaceAComponent;
            if (category.IsTagCategory) return PostableCommand.TagByCategory;
            if (category.CategoryType == CategoryType.Annotation) return PostableCommand.Symbol;
            return PostableCommand.PlaceAComponent;
        }
    }
}
