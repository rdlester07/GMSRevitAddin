using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.DB.Events;
using System.Windows.Forms;
using View = Autodesk.Revit.DB.View;
using System.IO;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace CreateUnitSheet
{
    /// <summary>
    /// "Create Unit Sheet" — part of the Unit Drawing Tools ribbon panel. Prompts (via
    /// <see cref="CreateUnitSheetForm.CreateUnitSheetForm"/>) for a new unit sheet number and an
    /// optional existing unit to duplicate, then either creates a brand-new unit sheet
    /// (<see cref="RunCreateNewUnit"/>) or duplicates an existing unit's drafting view onto a
    /// freshly created sheet and retags its pieces to the new unit (<see cref="RunDuplicateUnit"/>).
    /// Either way the model's single shared <see cref="UnitScheduleName"/> schedule is placed on the
    /// new sheet — a per-unit schedule is no longer generated (changed 2026-07-31).
    ///
    /// Both entry points wrap every Revit mutation they perform in a single
    /// <see cref="TransactionGroup"/>, so any failure partway through (missing titleblock, a
    /// schedule that silently duplicates because it isn't filtered by sheet, a failed view copy,
    /// etc.) rolls back everything atomically — no orphaned sheet/schedule/view is ever left behind.
    /// Every user-facing failure goes through <see cref="GMSRevitAddin.GmsUi"/> (proper Revit-window
    /// ownership + <see cref="GMSRevitAddin.GmsLog"/> logging) instead of a bare
    /// <see cref="MessageBox"/>. The slower duplicate-unit path shows a modeless
    /// <see cref="ProgressForm.ProgressForm"/> across sheet/schedule/view creation and the piece-tag
    /// retag loop, the same pattern already used by <c>UpdateFramingWeights</c>.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CreateUnitSheet : IExternalCommand
    {
        /// <summary>Name of the single shared piece schedule that gets placed on every unit sheet.</summary>
        public const string UnitScheduleName = "(Do Not Open) Unit Pieces";

        /// <summary>Default sheet-space location for the schedule when there's no source sheet to copy from.</summary>
        private static readonly XYZ DefaultSchedulePlacement = new XYZ(1.0625, 0.89575, 0);

        public static bool copyUnit { get; set; }
        public static string newUnitNumber { get; set; }
        public static string copyUnitNumber { get; set; }
        public static List<Element> unitViews = new List<Element>();
        public static List<string> unitViewNames = new List<string>();
        public static Document document = null;

        /// <summary>Why <see cref="CreateSheetAndPlaceSchedule"/> failed to produce a usable sheet.</summary>
        private enum SheetCreationFailure
        {
            None,
            TitleblockNotFound,    // CUS1
            ScheduleMissing,       // CUS7 (defensive re-check; Execute already pre-flighted this)
            ScheduleDuplicated,    // CUS10
            SheetCreateFailed,     // Error CUS3 — the sheet Transaction itself threw
            SchedulePlaceFailed    // Error CUS3 — the schedule Transaction threw for some other reason
        }

        /// <summary>Outcome of <see cref="CreateSheetAndPlaceSchedule"/>, carrying enough detail for
        /// <see cref="ShowSheetCreationFailure"/> to show the right message on failure.</summary>
        private class SheetCreationResult
        {
            public bool Success;
            public SheetCreationFailure Failure;
            public ElementId SheetId = ElementId.InvalidElementId;
            public string DuplicateScheduleName;
            public Exception Exception;
        }

        /// <summary>
        /// Returns the model's shared <see cref="UnitScheduleName"/> schedule, or null if it doesn't
        /// exist. Matched case-insensitively — the other GMS schedules use an all-caps
        /// "(DO NOT OPEN) …" convention, so casing shouldn't silently break the lookup.
        /// </summary>
        public static ViewSchedule FindUnitPiecesSchedule(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .FirstOrDefault(vs => string.Equals(vs.Name, UnitScheduleName, StringComparison.OrdinalIgnoreCase));
        }

        /// <summary>Returns every existing unit sheet's "Sheet Number" (sheets whose title/number
        /// contains "U-"), sorted, for populating the "duplicate from" combo box.</summary>
        public static List<string> getCurrentUnitSheetNames()
        {
            var sheets = new List<string>();
            if (document != null)
            {
                var sheetEls = new FilteredElementCollector(document)
                    .OfCategory(BuiltInCategory.OST_Sheets)
                    .WhereElementIsNotElementType();
                foreach (Element el in sheetEls)
                {
                    ViewSheet v = el as ViewSheet;
                    if (v != null && v.Title.Contains("U-") && v.SheetNumber != null && v.SheetNumber.Contains("U-"))
                    {
                        sheets.Add(v.SheetNumber);
                    }
                }
            }
            sheets.Sort();
            return sheets;
        }

        /// <summary>
        /// Gathers existing unit-sheet views (for the dialog's "duplicate from" list), shows
        /// <see cref="CreateUnitSheetForm.CreateUnitSheetForm"/>, pre-flight-checks that the shared
        /// schedule exists, and on OK dispatches to <see cref="RunDuplicateUnit"/> or
        /// <see cref="RunCreateNewUnit"/> depending on whether the user chose to duplicate an
        /// existing unit. Any unexpected exception from either path is caught here, logged, and
        /// shown via <see cref="GMSRevitAddin.GmsUi"/> instead of surfacing a raw Revit crash dialog.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument.Document;
            document = doc;
            UIDocument uidoc = new UIDocument(doc);

            // "Sheet Number" here is the instance parameter Revit stamps on a view once it's placed
            // on a sheet (not the ViewSheet.SheetNumber property) — this loop scans every kind of
            // view, not just sheets, so it has to stay a parameter lookup.
            FilteredElementCollector fec = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views);
            fec.WhereElementIsNotElementType();
            unitViews.Clear();
            unitViewNames.Clear();
            foreach (Element el in fec.ToElements())
            {
                View v = el as View;
                string sheetNumber = GMSRevitAddin.RevitParameterHelper.GetString(v, "Sheet Number");
                if (sheetNumber != null && sheetNumber.Contains("U-"))
                {
                    unitViews.Add(el);
                    unitViewNames.Add(sheetNumber);
                }
            }

            using (System.Windows.Forms.Form form = new CreateUnitSheetForm.CreateUnitSheetForm())
            {
                if (form.ShowDialog() != DialogResult.OK)
                {
                    return Result.Succeeded;
                }
            }

            // Pre-flight: the shared schedule must exist before anything is created, so a missing
            // one aborts cleanly instead of leaving a half-built sheet behind.
            if (FindUnitPiecesSchedule(doc) == null)
            {
                GMSRevitAddin.GmsUi.ShowError(
                    "Could not find a schedule named \"" + UnitScheduleName + "\" in this model." +
                    System.Environment.NewLine + System.Environment.NewLine +
                    "No sheet was created. Add the schedule to the model and try again.",
                    "CUS7 Error");
                return Result.Cancelled;
            }

            try
            {
                return copyUnit ? RunDuplicateUnit(doc, uidoc) : RunCreateNewUnit(doc, uidoc);
            }
            catch (Exception ex)
            {
                GMSRevitAddin.GmsUi.ShowError(
                    "An unexpected error occurred while creating the unit sheet.",
                    "CreateUnitSheet Error", ex);
                return Result.Failed;
            }
        }

        // ============================================================================
        // Pre-flight / validation helpers — read-only, no document mutation.
        // ============================================================================

        /// <summary>Finds the "GASB - Unit - 11x17" titleblock family type, or
        /// <see cref="ElementId.InvalidElementId"/> if it isn't in the model.</summary>
        private static ElementId FindUnitTitleblockTypeId(Document doc)
        {
            FamilySymbol fs = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_TitleBlocks)
                .WhereElementIsElementType()
                .Cast<FamilySymbol>()
                .FirstOrDefault(x => x.Family.Name == "GASB - Unit - 11x17");
            return fs == null ? ElementId.InvalidElementId : fs.Id;
        }

        /// <summary>Finds the <see cref="ViewSheet"/> with the given sheet number, via the direct
        /// <see cref="ViewSheet.SheetNumber"/> property.</summary>
        private static ViewSheet FindSheetByNumber(Document doc, string sheetNumber)
        {
            return new FilteredElementCollector(doc)
                .WhereElementIsNotElementType()
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .FirstOrDefault(vs => vs.SheetNumber == sheetNumber);
        }

        /// <summary>Finds a view (of any kind, not necessarily the sheet itself) by the "Sheet
        /// Number" instance parameter Revit stamps on a view once it's placed on a sheet — distinct
        /// from <see cref="ViewSheet.SheetNumber"/>, which only exists on the sheet element.</summary>
        private static View FindPlacedViewBySheetNumber(Document doc, string sheetNumber)
        {
            return new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Views)
                .WhereElementIsNotElementType()
                .Cast<View>()
                .FirstOrDefault(v => string.Equals(GMSRevitAddin.RevitParameterHelper.GetString(v, "Sheet Number"), sheetNumber, StringComparison.Ordinal));
        }

        /// <summary>Finds the placed instance of <paramref name="schedule"/> on the given sheet, or
        /// null if it isn't placed there.</summary>
        private static ScheduleSheetInstance FindScheduleInstanceOnSheet(Document doc, ViewSchedule schedule, ElementId sheetId)
        {
            if (schedule == null)
            {
                return null;
            }
            return new FilteredElementCollector(doc, sheetId)
                .OfClass(typeof(ScheduleSheetInstance))
                .Cast<ScheduleSheetInstance>()
                .FirstOrDefault(s => s.ScheduleId == schedule.Id);
        }

        /// <summary>Finds the source view's viewport on <paramref name="sourceSheet"/> and returns
        /// its bounding-box center (the sheet-space point the new viewport should be placed at), or
        /// null if that viewport can't be found.</summary>
        private static XYZ FindSourceViewportCenter(Document doc, ViewSheet sourceSheet, View sourceView)
        {
            List<Viewport> viewports = sourceSheet.GetAllViewports()
                .Select(id => doc.GetElement(id) as Viewport)
                .ToList();

            foreach (Viewport vp in viewports)
            {
                if (vp.ViewId == sourceView.Id)
                {
                    Outline outline = vp.GetBoxOutline();
                    XYZ min = outline.MinimumPoint;
                    XYZ max = outline.MaximumPoint;
                    return new XYZ((min.X + max.X) / 2, (min.Y + max.Y) / 2, (min.Z + max.Z) / 2);
                }
            }
            return null;
        }

        /// <summary>Counts "GAIT - Piece Tag" family instances scoped to <paramref name="view"/> —
        /// used only to size the <see cref="ProgressForm.ProgressForm"/> before the retag loop runs.</summary>
        private static int CountPieceTagsOnView(Document doc, View view)
        {
            return new FilteredElementCollector(doc, view.Id)
                .OfClass(typeof(FamilyInstance))
                .Cast<FamilyInstance>()
                .Count(x => x.Symbol.Family.Name.Equals("GAIT - Piece Tag"));
        }

        // ============================================================================
        // Revit-mutation helpers — each opens/commits its own Transaction(s) but never owns a
        // TransactionGroup; the group-owning entry points below decide when to Assimilate/RollBack.
        // ============================================================================

        /// <summary>
        /// Creates a new <see cref="ViewSheet"/> using the "GASB - Unit - 11x17" titleblock, sets its
        /// sheet number and "Grouping - Usage" to "Unit Drawings", then places the model's shared
        /// <see cref="UnitScheduleName"/> schedule on it (at <paramref name="schedulePlacement"/>, or
        /// <see cref="DefaultSchedulePlacement"/> when null — the from-scratch path, which has no
        /// source sheet) and clears the sheet's issue date. Runs as two separate <see cref="Transaction"/>s
        /// but performs no rollback of earlier steps on its own — that's the caller's
        /// <see cref="TransactionGroup"/> to decide, via the returned <see cref="SheetCreationResult"/>.
        /// </summary>
        private SheetCreationResult CreateSheetAndPlaceSchedule(Document doc, ViewSchedule unitSchedule, XYZ schedulePlacement)
        {
            var result = new SheetCreationResult();

            if (unitSchedule == null)
            {
                result.Failure = SheetCreationFailure.ScheduleMissing;
                return result;
            }

            ElementId titleblockId = FindUnitTitleblockTypeId(doc);
            if (titleblockId == ElementId.InvalidElementId)
            {
                result.Failure = SheetCreationFailure.TitleblockNotFound;
                return result;
            }

            ElementId sheetId;
            using (Transaction tr = new Transaction(doc, "Create new Unit Sheet"))
            {
                tr.Start();
                try
                {
                    ViewSheet newSheet = ViewSheet.Create(doc, titleblockId);
                    if (newSheet == null)
                    {
                        throw new Exception("ViewSheet.Create returned null.");
                    }
                    sheetId = newSheet.Id;
                    newSheet.SheetNumber = newUnitNumber;
                    GMSRevitAddin.RevitParameterHelper.TrySetString(newSheet, "Grouping - Usage", "Unit Drawings");
                    doc.Regenerate();
                    tr.Commit();
                }
                catch (Exception ex)
                {
                    tr.RollBack();
                    result.Failure = SheetCreationFailure.SheetCreateFailed;
                    result.Exception = ex;
                    return result;
                }
            }

            // Place the shared unit-pieces schedule on the new sheet. The schedule is created and
            // maintained in the model (not generated per unit), so this only places another instance
            // of it.
            XYZ location = schedulePlacement ?? DefaultSchedulePlacement;
            using (Transaction trSc = new Transaction(doc, "Place Unit Piece Schedule"))
            {
                trSc.Start();
                try
                {
                    ScheduleSheetInstance ssi = ScheduleSheetInstance.Create(doc, sheetId, unitSchedule.Id, location);

                    // A ViewSchedule can only be placed on more than one sheet if its definition has
                    // "Filter by: Sheet" enabled (Schedule Properties -> Fields tab, "Filter by:"
                    // dropdown). Without that setting, placing it on a second sheet doesn't fail
                    // outright — Revit silently duplicates the schedule (renaming the copy, e.g.
                    // "<name> 2") and places the DUPLICATE instead of the original. Detect that here
                    // (the returned instance's ScheduleId no longer matches the schedule we asked
                    // for) and abort. The caller's TransactionGroup.RollBack() undoes both this
                    // transaction AND the earlier-committed sheet-creation transaction in one atomic
                    // step, so no manual "delete the sheet" cleanup is needed here.
                    if (ssi.ScheduleId != unitSchedule.Id)
                    {
                        result.Failure = SheetCreationFailure.ScheduleDuplicated;
                        result.DuplicateScheduleName = (doc.GetElement(ssi.ScheduleId) as ViewSchedule)?.Name ?? "(unknown)";
                        trSc.RollBack();
                        return result;
                    }

                    ssi.Pinned = true;

                    //set issue_date to empty string
                    doc.GetElement(sheetId).get_Parameter(BuiltInParameter.SHEET_ISSUE_DATE).Set("");

                    doc.Regenerate();
                    trSc.Commit();
                }
                catch (Exception ex)
                {
                    trSc.RollBack();
                    result.Failure = SheetCreationFailure.SchedulePlaceFailed;
                    result.Exception = ex;
                    return result;
                }
            }

            result.Success = true;
            result.SheetId = sheetId;
            return result;
        }

        /// <summary>
        /// Creates a brand-new drafting view (not a <c>View.Duplicate</c>, so it gets its own
        /// independent element copies), copies every categorized non-type element from
        /// <paramref name="sourceView"/> into it (preserving scale), places it on
        /// <paramref name="newSheet"/> at <paramref name="originPoint"/> via a "No Title" viewport
        /// (falling back to the default type with a non-fatal warning if that type is missing), and
        /// copies the sheet description from <paramref name="origSheetName"/>. Deliberately does not
        /// catch its own exceptions — they propagate to the caller's single CUS4 handler, which also
        /// covers <see cref="RetagPiecesOnNewSheet"/>, matching the original single-error-covers-both
        /// behavior.
        /// </summary>
        private void DuplicateViewOntoSheet(Document doc, View sourceView, ViewSheet newSheet, XYZ originPoint, string origSheetName)
        {
            using (Transaction tDV = new Transaction(doc, "Duplicate existing Unit view"))
            {
                tDV.Start();

                // A brand-new drafting view is created (rather than View.Duplicate) so it gets its
                // own independent element copies instead of sharing/duplicating view-specific state
                // with the source.
                ViewFamilyType vft = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(q => q.ViewFamily == ViewFamily.Drafting);
                ViewDrafting createdView = ViewDrafting.Create(doc, vft.Id);

                Parameter viewName = createdView.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
                viewName.Set(newUnitNumber);

                // Copy every real (non-type) element with a category from the source view into the
                // new one, preserving the source view's scale.
                List<ElementId> elementsToCopy = new FilteredElementCollector(doc, sourceView.Id)
                    .WhereElementIsNotElementType()
                    .Where(el => el.Category != null)
                    .Select(el => el.Id)
                    .ToList();

                createdView.Scale = sourceView.Scale;
                ElementTransformUtils.CopyElements(sourceView, elementsToCopy, createdView, null, null);

                // Place the new view's viewport at the same sheet position the source occupied, and
                // match its viewport type to "No Title" (the GMS unit-sheet convention — falls back
                // to the default type with a warning if missing).
                Viewport newVP = Viewport.Create(doc, newSheet.Id, createdView.Id, originPoint);
                Element unitViewportType = newVP.GetValidTypes()
                    .Select(doc.GetElement)
                    .FirstOrDefault(el => el.Name == "No Title");

                if (unitViewportType != null)
                {
                    newVP.ChangeTypeId(unitViewportType.Id);
                }
                else
                {
                    GMSRevitAddin.GmsUi.ShowWarning(
                        "Failed to find viewport type \"No Title\". Continuing with default viewport type...",
                        "Warning");
                }

                //set new sheet description to the same as the original unit sheet description
                newSheet.get_Parameter(BuiltInParameter.SHEET_NAME).Set(origSheetName);

                doc.Regenerate();
                tDV.Commit();
            }
        }

        /// <summary>
        /// The copied piece tags still carry the source unit's Origin/Piece values — retags every
        /// "GAIT - Piece Tag" instance on <paramref name="newSheet"/> so Origin/Origin Sheet/Piece/
        /// Piece Description reflect the new unit number. Each parameter write is individually
        /// try/caught and logged (non-fatal, matching the original) — only an unexpected failure in
        /// the surrounding collection/transaction propagates to the caller's CUS4 handler.
        /// </summary>
        private void RetagPiecesOnNewSheet(Document doc, ViewSheet newSheet, ProgressForm.ProgressForm progress)
        {
            using (Transaction tr = new Transaction(doc, "Origin Update"))
            {
                tr.Start();
                string targetName = "GAIT - Piece Tag";
                string sheetNum = newSheet.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();
                List<FamilyInstance> familyInstances = new FilteredElementCollector(doc, newSheet.Id)
                    .OfClass(typeof(FamilyInstance))
                    .Cast<FamilyInstance>()
                    .Where(x => x.Symbol.Family.Name.Equals(targetName))
                    .ToList();

                foreach (FamilyInstance familyInstance in familyInstances)
                {
                    progress.IncrementWithText("Retagging " +
                        (familyInstance.LookupParameter("Prefix")?.AsString() ?? "?") + "-" +
                        (familyInstance.LookupParameter("Number")?.AsString() ?? "?") + "...");

                    string prefix = familyInstance.LookupParameter("Prefix").AsString();
                    string number = familyInstance.LookupParameter("Number").AsString();
                    string type = familyInstance.LookupParameter("Type").AsValueString();

                    Parameter origin = familyInstance.LookupParameter("Origin");
                    Parameter oSheet = familyInstance.LookupParameter("Origin Sheet");
                    Parameter piece = familyInstance.LookupParameter("Piece");
                    Parameter description = familyInstance.LookupParameter("Piece Description");

                    if (prefix != null && number != null && origin != null && type != null && oSheet != null && piece != null && description != null)
                    {
                        // Same Type -> description-lookup-key mapping as TagUpdateCurrentSheet.
                        string key;
                        switch (type)
                        {
                            case "Customer":
                                key = "CU";
                                break;
                            case "Gasket":
                                key = "GK";
                                break;
                            case "Glazing":
                                key = "GL";
                                break;
                            case "SubUnit":
                                key = "SU";
                                break;
                            default:
                                key = prefix;
                                break;
                        }

                        string testDescription = PieceDescriptions.PieceDescriptions.pieceDefinition(key);

                        if (origin.AsString() != sheetNum)
                        {
                            try { origin.Set(sheetNum); }
                            catch (System.Exception ex) { GMSRevitAddin.GmsLog.Error("CreateUnitSheet", ex); }
                        }

                        if (oSheet.AsString() != sheetNum)
                        {
                            try { oSheet.Set(sheetNum); }
                            catch (System.Exception ex) { GMSRevitAddin.GmsLog.Error("CreateUnitSheet", ex); }
                        }

                        string testPiece = prefix + "-" + number;
                        if (piece.AsString() != testPiece)
                        {
                            try { piece.Set(testPiece); }
                            catch (System.Exception ex) { GMSRevitAddin.GmsLog.Error("CreateUnitSheet", ex); }
                        }

                        if (description.AsString() != testDescription)
                        {
                            try { description.Set(testDescription); }
                            catch (System.Exception ex) { GMSRevitAddin.GmsLog.Error("CreateUnitSheet", ex); }
                        }
                    }
                }
                tr.Commit();
            }
        }

        // ============================================================================
        // Group-owning entry points — each wraps its Revit-mutation helpers in one
        // TransactionGroup, so a failure anywhere inside rolls back everything atomically.
        // ============================================================================

        /// <summary>Blank-sheet path: creates a new unit sheet and places the shared schedule on it,
        /// inside a single <see cref="TransactionGroup"/>.</summary>
        private Result RunCreateNewUnit(Document doc, UIDocument uidoc)
        {
            ViewSchedule unitSchedule = FindUnitPiecesSchedule(doc); // Execute() already confirmed non-null

            using (TransactionGroup group = new TransactionGroup(doc, "Create Unit Sheet"))
            {
                group.Start();
                try
                {
                    SheetCreationResult result = CreateSheetAndPlaceSchedule(doc, unitSchedule, null);
                    if (!result.Success)
                    {
                        if (group.IsValidObject) group.RollBack();
                        ShowSheetCreationFailure(result);
                        return Result.Cancelled;
                    }

                    group.Assimilate();
                    uidoc.RequestViewChange(doc.GetElement(result.SheetId) as View);
                    return Result.Succeeded;
                }
                catch (Exception ex)
                {
                    if (group.IsValidObject) group.RollBack();
                    GMSRevitAddin.GmsUi.ShowError("Error placing schedule on sheet", "Error CUS3", ex);
                    return Result.Failed;
                }
            }
        }

        /// <summary>
        /// Duplicate-unit path: resolves and validates the source sheet/view/schedule and the
        /// viewport insertion point entirely up front (read-only — nothing is created until every
        /// pre-flight check passes), then creates the new sheet, places the shared schedule,
        /// duplicates the source view onto it, and retags its pieces — all inside a single
        /// <see cref="TransactionGroup"/> so any failure rolls back everything. A modeless
        /// <see cref="ProgressForm.ProgressForm"/> tracks progress across the whole mutation phase.
        /// </summary>
        private Result RunDuplicateUnit(Document doc, UIDocument uidoc)
        {
            // --- Pre-flight (read-only) ---
            ViewSheet sourceSheet = FindSheetByNumber(doc, copyUnitNumber);
            if (sourceSheet == null)
            {
                GMSRevitAddin.GmsUi.ShowError(
                    "Could not find unit sheet \"" + copyUnitNumber + "\" to duplicate." +
                    System.Environment.NewLine + System.Environment.NewLine + "No sheet was created.",
                    "CUS8 Error");
                return Result.Cancelled;
            }

            View sourceView = FindPlacedViewBySheetNumber(doc, copyUnitNumber);
            if (sourceView == null)
            {
                GMSRevitAddin.GmsUi.ShowError(
                    "Could not find the view placed on unit sheet \"" + copyUnitNumber + "\" to duplicate." +
                    System.Environment.NewLine + System.Environment.NewLine + "No sheet was created.",
                    "CUS8 Error");
                return Result.Cancelled;
            }

            // The shared schedule must already be placed on the source unit's sheet — its position
            // there is reused for the new sheet, mirroring how the viewport position is copied below.
            ViewSchedule unitSchedule = FindUnitPiecesSchedule(doc); // Execute() already confirmed non-null
            ScheduleSheetInstance sourceSchedule = FindScheduleInstanceOnSheet(doc, unitSchedule, sourceSheet.Id);
            if (sourceSchedule == null)
            {
                GMSRevitAddin.GmsUi.ShowError(
                    "The \"" + UnitScheduleName + "\" schedule is not placed on unit sheet \"" +
                    copyUnitNumber + "\"." + System.Environment.NewLine + System.Environment.NewLine +
                    "Add it to that sheet and try again. No sheet was created.",
                    "CUS9 Error");
                return Result.Cancelled;
            }

            XYZ originPoint = FindSourceViewportCenter(doc, sourceSheet, sourceView);
            if (originPoint == null)
            {
                GMSRevitAddin.GmsUi.ShowError(
                    "Unable to locate insertion point for view on sheet " + sourceSheet.Name,
                    "CUS5 Error");
                return Result.Cancelled;
            }

            string origSheetName = sourceSheet.get_Parameter(BuiltInParameter.SHEET_NAME).AsString();
            int pieceTagCount = CountPieceTagsOnView(doc, sourceSheet);

            // --- Mutation: sheet + schedule + view + retag, all inside one TransactionGroup ---
            var progress = new ProgressForm.ProgressForm("Duplicate Unit Sheet", null, 3 + pieceTagCount, "Done");
            try
            {
                using (TransactionGroup group = new TransactionGroup(doc, "Duplicate Unit Sheet"))
                {
                    group.Start();
                    try
                    {
                        progress.IncrementWithText("Creating sheet " + newUnitNumber + "...");
                        SheetCreationResult sheetResult = CreateSheetAndPlaceSchedule(doc, unitSchedule, sourceSchedule.Point);
                        if (!sheetResult.Success)
                        {
                            if (group.IsValidObject) group.RollBack();
                            ShowSheetCreationFailure(sheetResult);
                            return Result.Cancelled;
                        }
                        progress.IncrementWithText("Placing schedule...");

                        ViewSheet newSheet = doc.GetElement(sheetResult.SheetId) as ViewSheet;

                        progress.IncrementWithText("Duplicating view...");
                        DuplicateViewOntoSheet(doc, sourceView, newSheet, originPoint, origSheetName);
                        uidoc.RequestViewChange(newSheet);

                        RetagPiecesOnNewSheet(doc, newSheet, progress);

                        group.Assimilate();
                        return Result.Succeeded;
                    }
                    catch (Exception ex)
                    {
                        if (group.IsValidObject) group.RollBack();
                        GMSRevitAddin.GmsUi.ShowError("Unable to duplicate and place view on sheet", "CUS4 Error", ex);
                        return Result.Failed;
                    }
                }
            }
            finally
            {
                progress.Close();
            }
        }

        // ============================================================================
        // UI/dialog helpers
        // ============================================================================

        /// <summary>Shows the single message matching a <see cref="CreateSheetAndPlaceSchedule"/>
        /// failure. Collapses a pre-existing bug where a missing titleblock could show both "CUS1"
        /// and the generic "Error CUS3" box back-to-back — now exactly one message per cause.</summary>
        private void ShowSheetCreationFailure(SheetCreationResult result)
        {
            switch (result.Failure)
            {
                case SheetCreationFailure.TitleblockNotFound:
                    GMSRevitAddin.GmsUi.ShowError("Could not find Unit Titleblock", "CUS1 Error");
                    break;

                case SheetCreationFailure.ScheduleMissing:
                    GMSRevitAddin.GmsUi.ShowError(
                        "Could not find a schedule named \"" + UnitScheduleName + "\" in this model.",
                        "CUS7 Error");
                    break;

                case SheetCreationFailure.ScheduleDuplicated:
                    GMSRevitAddin.GmsUi.ShowError(
                        "Placing the \"" + UnitScheduleName + "\" schedule on this sheet would have caused Revit to duplicate it (as \"" + result.DuplicateScheduleName + "\") instead of reusing the original. That change has been undone, so no duplicate or orphaned sheet was left behind." +
                        System.Environment.NewLine + System.Environment.NewLine +
                        "This happens because \"" + UnitScheduleName + "\" does not have \"Filter by: Sheet\" enabled. Open its Schedule Properties -> Fields tab, set the \"Filter by:\" dropdown to filter by sheet, then try again." +
                        System.Environment.NewLine + System.Environment.NewLine + "No sheet was created.",
                        "CUS10 Error");
                    break;

                case SheetCreationFailure.SheetCreateFailed:
                case SheetCreationFailure.SchedulePlaceFailed:
                    GMSRevitAddin.GmsUi.ShowError("Error placing schedule on sheet", "Error CUS3", result.Exception);
                    break;
            }
        }
    }
}
