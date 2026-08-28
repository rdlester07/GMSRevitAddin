#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using GMSRevitAddin;

namespace GMS.Tools.DetailItemPalette
{
    /// <summary>
    /// Owns all Detail Item Palette coordination: the dockable pane, the navigate external event,
    /// and the SelectionChanged handler that drives the pane. The host wires this up once at
    /// startup; the ribbon command initializes it on demand if it runs first. All entry points
    /// are idempotent.
    /// </summary>
    public static class PaletteModule
    {
        public static readonly DockablePaneId PaneId =
            new DockablePaneId(new Guid("A7B3C9D1-E2F4-4A5B-8C6D-7E8F9A0B1C2D"));

        internal static DetailItemPalettePane? PaneInstance;
        internal static NavigateToSheetHandler? NavigateHandler;
        internal static ExternalEvent? NavigateEvent;

        private static UIApplication? _uiApp;
        private static bool _eventsInitialized;
        private static bool _selectionSubscribed;

        /// <summary>Creates and registers the dockable pane. Call from OnStartup.</summary>
        public static void RegisterPane(UIControlledApplication application)
        {
            PaneInstance = new DetailItemPalettePane();
            application.RegisterDockablePane(PaneId, "Detail Item Palette", PaneInstance);
        }

        /// <summary>Creates the navigate ExternalEvent, wires the pane, and applies the theme.</summary>
        public static void EnsureEvents(UIApplication uiApp)
        {
            if (_eventsInitialized) return;
            _eventsInitialized = true;

            _uiApp = uiApp;
            NavigateHandler = new NavigateToSheetHandler();
            NavigateEvent = ExternalEvent.Create(NavigateHandler);
            PaneInstance?.SetExternalEvent(NavigateEvent, NavigateHandler);
            GmsLog.Info("PaletteModule: ExternalEvents created");

            if (PaneInstance != null)
            {
                var theme = UIThemeManager.CurrentTheme;
                PaneInstance.Dispatcher.Invoke(() => PaneInstance.ApplyTheme(theme));
            }
        }

        /// <summary>Re-applies Revit's current theme to the pane (e.g. when it is shown), so a
        /// light/dark switch made during the session is reflected without restarting Revit.</summary>
        public static void RefreshTheme()
        {
            if (PaneInstance != null)
                PaneInstance.Dispatcher.Invoke(() => PaneInstance.ApplyCurrentTheme());
        }

        public static void SubscribeSelectionChanged(UIApplication uiApp)
        {
            if (_selectionSubscribed) return;
            _selectionSubscribed = true;
            _uiApp ??= uiApp;
            uiApp.SelectionChanged += OnSelectionChanged;
            GmsLog.Info("PaletteModule: SelectionChanged subscribed");
        }

        /// <summary>
        /// Revit persists a dockable pane's shown/hidden state across sessions, keyed by
        /// AddInId + DockablePaneId — so if the palette was left open when Revit last closed,
        /// Revit re-opens it itself as soon as a document loads, before the user has asked for it
        /// this session. Called once from the one-shot first-Idling handler so the palette always
        /// starts off on a fresh session; the user opens it explicitly via the ribbon button
        /// (ShowPaletteCommand), same as any other tool. (Same fix as
        /// TaggingPaletteModule's force-initial-hide, applied here too.)
        /// </summary>
        public static void ForceInitialHide(UIApplication uiApp)
        {
            try
            {
                DockablePane pane;
                try { pane = uiApp.GetDockablePane(PaneId); }
                catch (Autodesk.Revit.Exceptions.ArgumentException) { return; }

                if (pane.IsShown())
                    pane.Hide();
            }
            catch (Exception ex) { GmsLog.Error("PaletteModule.ForceInitialHide", ex); }
        }

        public static void UnsubscribeSelectionChanged(UIApplication uiApp)
        {
            if (!_selectionSubscribed) return;
            _selectionSubscribed = false;
            uiApp.SelectionChanged -= OnSelectionChanged;
        }

        private static void OnSelectionChanged(object? sender, SelectionChangedEventArgs e)
        {
            try
            {
                // Prefer the stored UIApplication over the sender cast — Revit does not
                // guarantee sender is the UIApplication instance in all versions.
                var uiApp = _uiApp ?? sender as UIApplication;
                if (uiApp == null) { GmsLog.Info("OnSelectionChanged: uiApp is null"); return; }

                // GetDockablePane throws if the pane isn't registered yet (e.g. very early, or
                // in a doc context where dockable panes aren't available) — treat that as "skip".
                DockablePane pane;
                try { pane = uiApp.GetDockablePane(PaneId); }
                catch (Autodesk.Revit.Exceptions.ArgumentException) { return; }

                if (!pane.IsShown())
                {
                    GmsLog.Info("  Pane not visible — skipping update");
                    return;
                }

                var uiDoc = uiApp.ActiveUIDocument;
                if (uiDoc?.Document == null) { GmsLog.Info("OnSelectionChanged: no active document"); return; }

                var selectedIds = uiDoc.Selection.GetElementIds();
                GmsLog.Info($"OnSelectionChanged: {selectedIds.Count} element(s) selected");

                var detailItemCategoryId = new ElementId(BuiltInCategory.OST_DetailComponents);
                var detailGroupCategoryId = new ElementId(BuiltInCategory.OST_IOSDetailGroups);

                var detailItems = selectedIds
                    .Select(id => uiDoc.Document.GetElement(id))
                    .Where(el => el?.Category?.Id != null && el.Category.Id.Equals(detailItemCategoryId))
                    .OfType<FamilyInstance>()
                    .ToList();

                var detailGroups = selectedIds
                    .Select(id => uiDoc.Document.GetElement(id))
                    .Where(el => el?.Category?.Id != null && el.Category.Id.Equals(detailGroupCategoryId))
                    .OfType<Group>()
                    .ToList();

                // Allow multiple selected elements as long as they're all the same symbol/group type.
                bool allSameDetailItem = detailItems.Count >= 1
                    && detailItems.Count == selectedIds.Count
                    && detailItems[0].Symbol != null
                    && detailItems.All(fi => fi.Symbol?.Id?.Value == detailItems[0].Symbol!.Id.Value);

                bool allSameDetailGroup = detailGroups.Count >= 1
                    && detailGroups.Count == selectedIds.Count
                    && detailGroups[0].GroupType != null
                    && detailGroups.All(g => g.GroupType?.Id?.Value == detailGroups[0].GroupType!.Id.Value);

                if (allSameDetailItem)
                {
                    var sheets = FindSheetsForDetailItem(detailItems[0], uiDoc.Document);
                    var typeName = detailItems[0].Symbol?.Name ?? "Detail Item";
                    GmsLog.Info($"  Detail item '{typeName}' ({detailItems.Count} selected) found on {sheets.Count} sheet(s)");

                    var symbolId = detailItems[0].Symbol?.Id;
                    PaneInstance?.Dispatcher.Invoke(() => PaneInstance.UpdateSheets(typeName, symbolId, false, sheets));
                }
                else if (allSameDetailGroup)
                {
                    var sheets = FindSheetsForDetailGroup(detailGroups[0], uiDoc.Document);
                    var typeName = detailGroups[0].GroupType?.Name ?? "Detail Group";
                    GmsLog.Info($"  Detail group '{typeName}' ({detailGroups.Count} selected) found on {sheets.Count} sheet(s)");

                    var groupTypeId = detailGroups[0].GroupType?.Id;
                    PaneInstance?.Dispatcher.Invoke(() => PaneInstance.UpdateSheets(typeName, groupTypeId, true, sheets));
                }
                else
                {
                    PaneInstance?.Dispatcher.Invoke(() => PaneInstance?.ClearSheets());
                }
            }
            catch (Exception ex)
            {
                GmsLog.Info($"OnSelectionChanged EXCEPTION: {ex}");
            }
        }

        private static List<SheetInfo> FindSheetsForDetailItem(FamilyInstance item, Document doc)
        {
            var symbolId = item.Symbol?.Id;
            if (symbolId == null)
            {
                GmsLog.Info("  Symbol is null");
                return [];
            }

            GmsLog.Info($"  Symbol: '{item.Symbol!.Name}' id={symbolId}");

            // viewId → count of instances in that view
            // FamilyInstanceFilter is a quick (native) filter — Revit excludes non-matching
            // elements before unmarshalling them into .NET, which is faster than LINQ on large models.
            var viewCounts = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_DetailComponents)
                .WherePasses(new FamilyInstanceFilter(doc, symbolId))
                .Cast<FamilyInstance>()
                .Where(fi => fi.OwnerViewId.Value > 0)
                .GroupBy(fi => fi.OwnerViewId.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            GmsLog.Info($"  Owner views across all instances: {viewCounts.Count}");

            return BuildSheetList(viewCounts, doc);
        }

        private static List<SheetInfo> FindSheetsForDetailGroup(Group group, Document doc)
        {
            var groupTypeId = group.GroupType?.Id;
            if (groupTypeId == null)
            {
                GmsLog.Info("  GroupType is null");
                return [];
            }

            GmsLog.Info($"  GroupType: '{group.GroupType!.Name}' id={groupTypeId}");

            var viewCounts = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_IOSDetailGroups)
                .OfClass(typeof(Group))
                .Cast<Group>()
                .Where(g => g.GroupType?.Id.Value == groupTypeId.Value && g.OwnerViewId.Value > 0)
                .GroupBy(g => g.OwnerViewId.Value)
                .ToDictionary(g => g.Key, g => g.Count());

            GmsLog.Info($"  Owner views across all group instances: {viewCounts.Count}");

            return BuildSheetList(viewCounts, doc);
        }

        /// <summary>
        /// Walks every sheet → its viewports and accumulates, per sheet, the instance counts of the
        /// views named in <paramref name="viewCounts"/> (viewId → instance count). Shared by the
        /// detail-item and detail-group paths. Returns sheets ordered by sheet number.
        /// </summary>
        private static List<SheetInfo> BuildSheetList(Dictionary<long, int> viewCounts, Document doc)
        {
            if (viewCounts.Count == 0) return [];

            var sheetResults = new Dictionary<long, SheetInfo>();

            foreach (var sheet in new FilteredElementCollector(doc)
                                       .OfClass(typeof(ViewSheet))
                                       .Cast<ViewSheet>())
            {
                foreach (var vpId in sheet.GetAllViewports())
                {
                    if (doc.GetElement(vpId) is Viewport vp &&
                        viewCounts.TryGetValue(vp.ViewId.Value, out int count))
                    {
                        if (!sheetResults.TryGetValue(sheet.Id.Value, out var info))
                        {
                            GmsLog.Info($"  Found on sheet: {sheet.SheetNumber}");
                            info = new SheetInfo
                            {
                                SheetId = sheet.Id,
                                SheetNumber = sheet.SheetNumber,
                                SheetName = sheet.Name
                            };
                            sheetResults[sheet.Id.Value] = info;
                        }
                        info.InstanceCount += count;
                    }
                }
            }

            GmsLog.Info($"  Total sheets: {sheetResults.Count}");
            return [.. sheetResults.Values.OrderBy(s => s.SheetNumber)];
        }
    }
}
