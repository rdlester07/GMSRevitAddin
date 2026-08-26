using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Linq.Expressions;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace CollectDiesForm
{
    /// <summary>Which of the GMS-convention naming categories a family+type belongs to (its name
    /// starts with "Fastener"/"Extrusion"/"Component"), or <see cref="Other"/> if it matches none.</summary>
    public enum FamilyCategory
    {
        Fastener,
        Extrusion,
        Component,
        Other
    }

    /// <summary>
    /// "Collect Detail Items" dialog: lets the user pick one or more sheet "Grouping - Usage" sets
    /// (and, optionally, which phase parameters to void first), then collects every distinct detail
    /// component / family instance placed on drafting views of the matching sheets into a single new
    /// drafting view — grouped into columns (fasteners, components, extrusions, and, if requested,
    /// everything else) so the dies/parts used across a set of sheets can be reviewed or scheduled in
    /// one place. Which of the three named categories actually get collected is controlled by the
    /// Fasteners/Extrusions/Components checkboxes. Opened by <see cref="LaunchForm"/> (wired to the
    /// ribbon).
    ///
    /// All state (document, the sheet list, the discovered "Grouping - Usage"/phase names, and the
    /// per-bucket family lists) is instance-level rather than static — this dialog is modal
    /// (<c>ShowDialog</c> blocks <see cref="LaunchForm.Execute"/> until it closes), so there is never
    /// more than one instance alive, and static fields only added risk without buying anything.
    /// </summary>
    public partial class CollectDiesForm : System.Windows.Forms.Form
    {
        /// <summary>Separator between entries in the persisted "last selected sheet sets/phases"
        /// settings — see <see cref="RestoreChecked"/>/<see cref="SaveSelectionSettings"/>.</summary>
        private const string ListSeparator = "||";

        private readonly Document doc;
        private readonly UIDocument uiDoc;
        /// <summary>Every project sheet, gathered once by <see cref="LaunchForm.Execute"/> and reused
        /// here instead of re-querying <see cref="BuiltInCategory.OST_Sheets"/> a second time.</summary>
        private readonly List<ViewSheet> sheets;
        private readonly List<string> sheetSets;
        private readonly List<string> phaseTypes = new List<string>();
        private readonly List<FamilySymbol> fastenerList = new List<FamilySymbol>();
        private readonly List<FamilySymbol> gasketList = new List<FamilySymbol>();
        private readonly List<FamilySymbol> extrusionList = new List<FamilySymbol>();
        private readonly List<FamilySymbol> doNotSchedule = new List<FamilySymbol>();
        private readonly List<FamilySymbol> otherFamilies = new List<FamilySymbol>();

        public CollectDiesForm(Document doc, UIDocument uiDoc, List<ViewSheet> sheets, List<string> sheetSets)
        {
            InitializeComponent();
            this.doc = doc;
            this.uiDoc = uiDoc;
            this.sheets = sheets;
            this.sheetSets = sheetSets;
        }

        /// <summary>Applies the shared theme and populates both checklists: sheet "Grouping - Usage"
        /// values (blank usage shown as "???") gathered by <see cref="LaunchForm"/>, and every
        /// "Phase"-named parameter found on the first detail-component type that has one (used to let
        /// the user zero out phase params before collecting). Restores the checkbox states and
        /// checklist selections saved from the last run of this command (see
        /// <see cref="SaveSelectionSettings"/>).</summary>
        private void CollectDiesForm_Load(object sender, EventArgs e)
        {
            GMSRevitAddin.DarkTheme.MarkPrimary(button_Start);
            GMSRevitAddin.DarkTheme.Apply(this);
            this.Text = "Collect Detail Items";

            button_Start.Enabled = true;
            button_Start.Visible = true;

            var settings = GMSRevitAddin.Properties.Settings.Default;
            checkBoxFasteners.Checked = settings.CollectDiesCollectFasteners;
            checkBoxExtrusions.Checked = settings.CollectDiesCollectExtrusions;
            checkBoxComponents.Checked = settings.CollectDiesCollectComponents;
            checkBoxCollectAll.Checked = settings.CollectDiesCollectAll;
            // checkBoxCollectAllViewTypes is hidden (Visible = false in the designer) and no longer
            // restored from settings, so it stays permanently unchecked — collection is always
            // drafting-view-only now, regardless of what a prior session had saved before it was
            // hidden. Left in place (not deleted) so it's a one-line change to bring back.

            var items = checkedListBox1.Items;
            sheetSets.Sort();
            foreach (string sheetSet in sheetSets)
            {
                if (!string.IsNullOrWhiteSpace(sheetSet))
                {
                    items.Add(sheetSet);
                }
                else
                {
                    items.Add("???");
                }
            }
            RestoreChecked(checkedListBox1, settings.CollectDiesSelectedSheetSets);

            // Sample just the first detail-component type's parameters to discover the "Phase"
            // parameter names in use in this model (stops at the first type that has any). This has
            // to stay a full ParameterSet scan — unlike the named-parameter reads elsewhere in this
            // file, the names themselves aren't known ahead of time.
            phaseTypes.Clear();
            FilteredElementCollector coll = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DetailComponents).WhereElementIsElementType();
            foreach (Element el in coll)
            {
                ParameterSet pSet = el.Parameters;
                foreach (Parameter p in pSet)
                {
                    if (p.Definition.Name.Contains("Phase"))
                    {
                        if (!phaseTypes.Contains(p.Definition.Name))
                        {
                            phaseTypes.Add(p.Definition.Name);
                        }
                    }
                }

                if (phaseTypes.Any())
                {
                    break;
                }
            }
            var phases = checkedListBox2.Items;
            phaseTypes.Sort();
            foreach (string phaseType in phaseTypes)
            {
                phases.Add(phaseType);
            }
            RestoreChecked(checkedListBox2, settings.CollectDiesSelectedPhases);
        }

        /// <summary>Checks every item in <paramref name="box"/> whose text appears in
        /// <paramref name="persisted"/> (a <see cref="ListSeparator"/>-delimited list saved by
        /// <see cref="SaveSelectionSettings"/>). A name no longer present in the list (e.g. a sheet-set
        /// usage value that no longer exists in this model) is simply not found and stays unchecked.</summary>
        private static void RestoreChecked(CheckedListBox box, string persisted)
        {
            if (string.IsNullOrEmpty(persisted))
            {
                return;
            }
            HashSet<string> names = new HashSet<string>(persisted.Split(new[] { ListSeparator }, StringSplitOptions.None));
            for (int i = 0; i < box.Items.Count; i++)
            {
                if (names.Contains(box.Items[i].ToString()))
                {
                    box.SetItemChecked(i, true);
                }
            }
        }

        /// <summary>Persists the current checkbox states and checked sheet-set/phase names so the next
        /// time this dialog opens it starts from the same selection. Called unconditionally at the top
        /// of <see cref="button_Start_Click"/> so it reflects whatever was on screen when Start was
        /// pressed, regardless of what the run itself does afterward.</summary>
        private void SaveSelectionSettings(List<string> phases)
        {
            var settings = GMSRevitAddin.Properties.Settings.Default;
            settings.CollectDiesCollectFasteners = checkBoxFasteners.Checked;
            settings.CollectDiesCollectExtrusions = checkBoxExtrusions.Checked;
            settings.CollectDiesCollectComponents = checkBoxComponents.Checked;
            settings.CollectDiesCollectAll = checkBoxCollectAll.Checked;
            settings.CollectDiesCollectAllViewTypes = checkBoxCollectAllViewTypes.Checked;
            settings.CollectDiesSelectedPhases = string.Join(ListSeparator, phases);

            List<string> checkedSets = new List<string>();
            foreach (int index in checkedListBox1.CheckedIndices)
            {
                checkedSets.Add(checkedListBox1.Items[index].ToString());
            }
            settings.CollectDiesSelectedSheetSets = string.Join(ListSeparator, checkedSets);
            settings.Save();
        }

        /// <summary>
        /// If any phase checkboxes are checked, first zeroes out those phase parameters on every
        /// detail-component type in the model, then — if a sheet-set checkbox is checked and the phase
        /// step didn't error — walks every project sheet whose "Grouping - Usage" matches a selected
        /// set, collects distinct family+type combinations from its drafting-view viewports (bucketed
        /// by <see cref="ClassifyFamily"/> and filtered to the checked Fasteners/Extrusions/Components
        /// categories), and places one instance of each into a new drafting view. Both steps run inside
        /// a single <see cref="TransactionGroup"/> so an unexpected failure midway (e.g. while building
        /// the new view) rolls back the phase-void step too, instead of leaving phase parameters
        /// zeroed with no resulting view. Any placement failures are written into a text note on the
        /// new view (a permanent record) and shown in a results dialog
        /// (<see cref="CollectDiesResultsController"/>) instead of aborting the run.
        /// </summary>
        private void button_Start_Click(object sender, EventArgs e)
        {
            var items = checkedListBox1.CheckedIndices;

            var checkedPhases = checkedListBox2.CheckedIndices;
            List<string> phases = new List<string>();
            foreach (int indexChecked in checkedPhases)
            {
                phases.Add(checkedListBox2.Items[indexChecked].ToString());
            }

            SaveSelectionSettings(phases);

            if (!phases.Any() && items.Count == 0)
            {
                return; // nothing selected — nothing to do
            }

            // Only closes the dialog once a new view has actually been created (collected == true) —
            // matches the original behavior of leaving the dialog open after a phase-only run (or a
            // phase error) so the user can see the result and decide what to do next.
            bool collected = false;
            using (TransactionGroup group = new TransactionGroup(doc, "Collect Detail Items"))
            {
                group.Start();
                try
                {
                    bool error = RunVoidPhases(phases);
                    if (!error && items.Count > 0)
                    {
                        RunCollect(items, phases);
                        collected = true;
                    }

                    group.Assimilate();
                }
                catch (Exception ex)
                {
                    if (group.IsValidObject) group.RollBack();
                    GMSRevitAddin.GmsLog.Error("CollectDies: aborting, rolling back", ex);
                    GMSRevitAddin.GmsUi.ShowError(
                        "An unexpected error occurred and all changes made by this run were rolled back." +
                        System.Environment.NewLine + ex.Message, "Error");
                    this.Visible = true; // undo the RunVoidPhases/RunCollect progress-form hide below
                    return;
                }
            }

            if (collected)
            {
                closeForm();
            }
            else
            {
                // RunVoidPhases hides the dialog behind its progress form and — unlike the collect
                // path, which closes the dialog outright — never got it back on screen afterward in
                // the original code, leaving a phase-only run (or a phase error) with an invisible but
                // still-open modal dialog. Restore it here.
                this.Visible = true;
            }
        }

        /// <summary>Zeroes out every parameter in <paramref name="phases"/> on every detail-component
        /// type in the model, inside its own <see cref="Transaction"/>. Per-element write failures are
        /// collected and shown via <see cref="GMSRevitAddin.GmsUi.ShowError"/> but do not stop the
        /// transaction from committing — the elements that did succeed stay voided. Returns true if any
        /// write failed (the caller skips the collect step in that case, same as before this refactor).</summary>
        private bool RunVoidPhases(List<string> phases)
        {
            if (!phases.Any())
            {
                return false;
            }

            FilteredElementCollector fec = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DetailComponents).WhereElementIsElementType();
            List<Element> families = fec.ToList();

            string st = "Processing element {0} of " + families.Count.ToString() + "...";
            string fi = "Regenerating model...";
            string caption = "Void Selected Phase Information";

            bool error = false;
            List<string> errors = new List<string>();

            using (ProgressForm.ProgressForm pform = new ProgressForm.ProgressForm(caption, st, families.Count, fi))
            {
                this.Visible = false;

                using (Transaction tr = new Transaction(doc, "Void Component Phases"))
                {
                    tr.Start();

                    foreach (Element el in families)
                    {
                        pform.Visible = true;
                        pform.Increment();

                        foreach (string phaseName in phases)
                        {
                            Parameter p = el.LookupParameter(phaseName);
                            if (p == null) continue;

                            try
                            {
                                p.Set(0);
                            }
                            catch (Exception exc)
                            {
                                GMSRevitAddin.GmsLog.Error("CollectDies: void phase '" + phaseName + "' on " + el.Id, exc);
                                errors.Add(el.Id.ToString() + " - " + exc.Message);
                                error = true;
                            }
                        }
                    }
                    tr.Commit();
                }
            }

            if (error)
            {
                string message = "Errors were found while updating item phase parameters.";
                foreach (string s in errors)
                {
                    message += System.Environment.NewLine + s;
                }
                GMSRevitAddin.GmsUi.ShowError(message, "Error");
            }

            return error;
        }

        /// <summary>
        /// Classifies a family+type by the fixed GMS naming convention: a name starting with
        /// "Fastener"/"Extrusion"/"Component" belongs to that <see cref="FamilyCategory"/>; anything
        /// else is <see cref="FamilyCategory.Other"/>. For the Fastener/Extrusion categories,
        /// <paramref name="isVariant"/> reports whether the name is the "…Plan"/"…Mod" spelling — those
        /// are placed but flagged "do not schedule" instead of going in the category's normal column
        /// (Components have no such variant). This intentionally mirrors the original hardcoded logic
        /// (there was a brief detour through a user-editable rule table; it added a footgun — an
        /// accidental grid re-sort could silently break rule precedence — for a naming convention that
        /// never actually changes, so it's back to a fixed method).
        /// </summary>
        private static void ClassifyFamily(string familyAndType, out FamilyCategory category, out bool isVariant)
        {
            string name = familyAndType.ToLowerInvariant();
            if (name.StartsWith("fastener"))
            {
                category = FamilyCategory.Fastener;
                isVariant = name.Contains("plan");
            }
            else if (name.StartsWith("component"))
            {
                category = FamilyCategory.Component;
                isVariant = false;
            }
            else if (name.StartsWith("extrusion"))
            {
                category = FamilyCategory.Extrusion;
                isVariant = name.Contains("mod");
            }
            else
            {
                category = FamilyCategory.Other;
                isVariant = false;
            }
        }

        /// <summary>Whether <paramref name="category"/> is checked to be collected at all — the
        /// Fasteners/Extrusions/Components checkboxes for their respective categories, or "Collect All"
        /// for <see cref="FamilyCategory.Other"/>. Checked once per family+type up front so an
        /// unchecked category (and its "…Plan"/"…Mod" variants) is skipped entirely rather than merely
        /// excluded at placement time.</summary>
        private bool IsCategorySelected(FamilyCategory category)
        {
            switch (category)
            {
                case FamilyCategory.Fastener: return checkBoxFasteners.Checked;
                case FamilyCategory.Extrusion: return checkBoxExtrusions.Checked;
                case FamilyCategory.Component: return checkBoxComponents.Checked;
                default: return checkBoxCollectAll.Checked; // Other
            }
        }

        /// <summary>Scans the matching sheets' drafting-view viewports for distinct family+type
        /// combinations, then places one instance of each into a new drafting view. Runs inside its own
        /// <see cref="Transaction"/> (the caller's <see cref="TransactionGroup"/> decides whether to
        /// keep or roll back the whole run).</summary>
        private void RunCollect(CheckedListBox.CheckedIndexCollection items, List<string> phases)
        {
            List<string> selected = new List<string>();
            foreach (int index in items)
            {
                selected.Add(sheetSets[index] != "???" ? sheetSets[index] : "");
            }

            List<string> familyNames = new List<string>();
            fastenerList.Clear();
            gasketList.Clear();
            extrusionList.Clear();
            doNotSchedule.Clear();
            otherFamilies.Clear();

            string s = "Processing sheet {0} of " + sheets.Count.ToString() + "...";
            string f = "Creating new sheet and regenerating model...";
            string captionText = "Collect Detail Items";

            using (ProgressForm.ProgressForm pf = new ProgressForm.ProgressForm(captionText, s, sheets.Count, f))
            {
                this.Visible = false;

                foreach (ViewSheet vs in sheets)
                {
                    pf.Visible = true;
                    pf.Increment();

                    // Sheets without the "Grouping - Usage" parameter at all are excluded, same as
                    // before this refactor — only sheets that at least carry the parameter (even blank,
                    // shown as "???") are eligible for collection.
                    Parameter usageParam = vs.LookupParameter("Grouping - Usage");
                    if (usageParam == null || !selected.Contains(usageParam.AsString()))
                    {
                        continue;
                    }

                    ICollection<ElementId> listViews = vs.GetAllViewports();
                    foreach (ElementId eid in listViews)
                    {
                        Viewport viewport = doc.GetElement(eid) as Viewport;
                        Autodesk.Revit.DB.View currentView = doc.GetElement(viewport.ViewId) as Autodesk.Revit.DB.View;
                        if (!checkBoxCollectAllViewTypes.Checked && currentView.ViewType != ViewType.DraftingView)
                        {
                            continue;
                        }

                        FilteredElementCollector fec = new FilteredElementCollector(doc, viewport.ViewId).OfClass(typeof(FamilyInstance));
                        foreach (Element fam in fec)
                        {
                            FamilyInstance fi = fam as FamilyInstance;
                            FamilySymbol fs = fi.Symbol;
                            string familyAndType = fi.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).AsValueString();

                            if (string.IsNullOrWhiteSpace(familyAndType) || familyNames.Contains(familyAndType))
                            {
                                continue;
                            }
                            familyNames.Add(familyAndType);

                            FamilyCategory category;
                            bool isVariant;
                            ClassifyFamily(familyAndType, out category, out isVariant);
                            if (!IsCategorySelected(category))
                            {
                                continue;
                            }

                            if (isVariant)
                            {
                                doNotSchedule.Add(fs); // Fastener…Plan or Extrusion…Mod
                            }
                            else if (category == FamilyCategory.Fastener) fastenerList.Add(fs);
                            else if (category == FamilyCategory.Extrusion) extrusionList.Add(fs);
                            else if (category == FamilyCategory.Component) gasketList.Add(fs);
                            else otherFamilies.Add(fs); // Other, and checkBoxCollectAll already confirmed checked
                        }
                    }
                }
            }

            string newViewName = "";
            foreach (string type in selected)
            {
                newViewName += (!string.IsNullOrWhiteSpace(type) ? type : "NO USAGE TYPE") + " ";
            }

            int placeCount = fastenerList.Count + gasketList.Count + extrusionList.Count +
                (checkBoxCollectAll.Checked ? doNotSchedule.Count + otherFamilies.Count : 0);
            string ps = "Placing item {0} of " + placeCount.ToString() + "...";

            ViewDrafting newView;
            List<PlacementFailure> failures = new List<PlacementFailure>();
            using (ProgressForm.ProgressForm placeProgress = new ProgressForm.ProgressForm(captionText, ps, Math.Max(placeCount, 1), "Finishing..."))
            {
                this.Visible = false;

                using (Transaction tr1 = new Transaction(doc, "Create New DraftingView"))
                {
                    tr1.Start();
                    ViewFamilyType vd = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(q => q.ViewFamily == ViewFamily.Drafting);

                    ViewDrafting draftView = ViewDrafting.Create(doc, vd.Id);
                    draftView.Name = "Detail Items - " + newViewName + DateTime.Now.ToString("yyyyMMddHHmmss");
                    newView = draftView;

                    // Each family bucket is placed down its own column, wrapping into additional
                    // columns as needed (see PlaceColumn); x tracks where the next bucket's column(s)
                    // should start, advanced past whatever column(s) the previous bucket used.
                    double x = 1.0;
                    PlaceColumn(draftView, fastenerList, ref x, phases, placeProgress, failures);
                    PlaceColumn(draftView, gasketList, ref x, phases, placeProgress, failures);
                    PlaceColumn(draftView, extrusionList, ref x, phases, placeProgress, failures);

                    // "Collect All" additionally places every do-not-schedule variant (Plan/Mod
                    // families, already filtered to checked categories above) and any other family
                    // type not matched by the buckets above.
                    if (checkBoxCollectAll.Checked)
                    {
                        List<FamilySymbol> combined = new List<FamilySymbol>(doNotSchedule);
                        combined.AddRange(otherFamilies);
                        PlaceColumn(draftView, combined, ref x, phases, placeProgress, failures, flagOnly: doNotSchedule);
                    }

                    if (failures.Count > 0)
                    {
                        string errorText = string.Join(System.Environment.NewLine,
                            failures.Select(fl => fl.FamilyName + " - " + fl.TypeName));
                        ElementId defaultTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
                        TextNote.Create(doc, draftView.Id, new XYZ(0, 0, 0), .25, errorText, defaultTypeId);
                    }

                    tr1.Commit();
                }
            }

            if (uiDoc != null)
            {
                uiDoc.ActiveView = newView;
            }
            else
            {
                GMSRevitAddin.GmsLog.Error("CollectDies: uiDoc is null");
            }

            if (failures.Count > 0)
            {
                CollectDiesResultsController.ShowResults(failures, GMSRevitAddin.GmsUi.Owner);
            }
        }

        /// <summary>
        /// Places one instance of each symbol in <paramref name="column"/>, stacked in a column
        /// starting at x = <paramref name="x"/>. Row spacing comes from each instance's own actual
        /// bounding box (read after a mid-transaction <see cref="Document.Regenerate"/>) rather than a
        /// fixed guess, so items never overlap regardless of how tall/short each detail item actually
        /// is. Once a column's accumulated height passes <c>MaxColumnHeightFeet</c>, placement wraps
        /// into a new column to its right, so a long bucket runs across several columns instead of one
        /// endless one. <paramref name="x"/> is advanced past every column this bucket used, ready for
        /// the next bucket's call.
        ///
        /// Every placed instance's type has <paramref name="phases"/> flagged "do not schedule" (value
        /// 1) unless <paramref name="flagOnly"/> is given, in which case only symbols in that list are
        /// flagged (used by the combined do-not-schedule + other column). Placement failures are logged
        /// and added to <paramref name="failures"/> instead of aborting the rest of the column.
        /// </summary>
        private void PlaceColumn(ViewDrafting draftView, List<FamilySymbol> column, ref double x,
            List<string> phases, ProgressForm.ProgressForm progress, List<PlacementFailure> failures,
            List<FamilySymbol> flagOnly = null)
        {
            const double RowGapFeet = 0.25;
            const double ColumnGapFeet = 1.0;
            const double MaxColumnHeightFeet = 30.0;
            const double FallbackSpanFeet = 0.5; // used if a placed instance's bounding box can't be read

            double y = 0;
            double columnHeight = 0;
            double columnWidth = 0;

            foreach (FamilySymbol fmsy in column)
            {
                progress.Visible = true;
                progress.Increment();

                try
                {
                    FamilyInstance newFI = doc.Create.NewFamilyInstance(new XYZ(x, y, 0), fmsy, draftView);
                    doc.Regenerate(); // so get_BoundingBox below reflects the instance just placed

                    if (flagOnly == null || flagOnly.Contains(fmsy))
                    {
                        Element elm = doc.GetElement(newFI.GetTypeId());
                        updateParameters(phases, elm);
                    }

                    BoundingBoxXYZ bb = newFI.get_BoundingBox(draftView);
                    double height = bb != null ? Math.Max(bb.Max.Y - bb.Min.Y, RowGapFeet) : FallbackSpanFeet;
                    double width = bb != null ? Math.Max(bb.Max.X - bb.Min.X, RowGapFeet) : FallbackSpanFeet;

                    columnWidth = Math.Max(columnWidth, width);
                    y -= height + RowGapFeet;
                    columnHeight += height + RowGapFeet;

                    if (columnHeight > MaxColumnHeightFeet)
                    {
                        x += columnWidth + ColumnGapFeet;
                        y = 0;
                        columnHeight = 0;
                        columnWidth = 0;
                    }
                }
                catch (Exception ex)
                {
                    GMSRevitAddin.GmsLog.Error("CollectDies: failed to place " + fmsy.FamilyName + " - " + fmsy.Name, ex);
                    failures.Add(new PlacementFailure { FamilyName = fmsy.FamilyName, TypeName = fmsy.Name, Reason = ex.Message });
                }
            }

            // Advance past this bucket's last (possibly partial) column so the next bucket starts
            // clear of it. If the column was empty this just reserves a small gap, same as the
            // original fixed-offset scheme always did regardless of whether a bucket had any items.
            x += columnWidth + ColumnGapFeet;
        }

        /// <summary>Sets every parameter named in <paramref name="phases"/> to 1 ("do not schedule") on
        /// the given element's type — used to flag Plan/Mod family instances placed into the collection
        /// view. Uses <c>LookupParameter</c> per known name rather than scanning the full
        /// <see cref="ParameterSet"/>, since the candidate names are already known here.</summary>
        public void updateParameters(List<string> phases, Element el)
        {
            foreach (string phaseName in phases)
            {
                Parameter p = el.LookupParameter(phaseName);
                if (p != null)
                {
                    p.Set(1);
                }
            }
        }

        private void closeForm()
        {
            this.Close();
        }
    }

    /// <summary>
    /// Entry point wired as the ribbon's "Collect Detail Items" command. Gathers every project sheet
    /// and the distinct "Grouping - Usage" values present on them (used to populate
    /// <see cref="CollectDiesForm"/>'s sheet-set checklist), then shows the form modally. The sheet
    /// list is handed to the form so it isn't re-queried a second time when the user clicks Start.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchForm : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Document doc = uiApp.ActiveUIDocument.Document;
            UIDocument uidoc = uiApp.ActiveUIDocument;

            List<ViewSheet> sheets = new FilteredElementCollector(doc)
                .OfCategory(BuiltInCategory.OST_Sheets)
                .WhereElementIsNotElementType()
                .OfClass(typeof(ViewSheet))
                .Cast<ViewSheet>()
                .ToList();

            List<string> sheetSets = new List<string>();
            foreach (ViewSheet vs in sheets)
            {
                Parameter usageParam = vs.LookupParameter("Grouping - Usage");
                if (usageParam == null)
                {
                    continue;
                }
                string usage = usageParam.AsString();
                if (!sheetSets.Contains(usage))
                {
                    sheetSets.Add(usage);
                }
            }

            using (CollectDiesForm form = new CollectDiesForm(doc, uidoc, sheets, sheetSets))
            {
                form.ShowDialog(GMSRevitAddin.GmsUi.Owner);
            }
            return Result.Succeeded;
        }
    }
}
