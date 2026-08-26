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
    /// <summary>
    /// "Collect Detail Items" dialog: lets the user pick one or more sheet "Grouping - Usage" sets
    /// (and, optionally, which phase parameters to void first), then collects every distinct detail
    /// component / family instance placed on drafting views of the matching sheets into a single new
    /// drafting view — grouped into columns (fasteners, gasket/components, extrusions, and, if
    /// requested, everything else) so all the dies/parts used across a set of sheets can be reviewed
    /// or scheduled in one place. Opened by <see cref="LaunchForm"/> (wired to the ribbon).
    ///
    /// All state (document, the sheet list, the discovered "Grouping - Usage"/phase names, and the
    /// per-bucket family lists) is instance-level rather than static — this dialog is modal
    /// (<c>ShowDialog</c> blocks <see cref="LaunchForm.Execute"/> until it closes), so there is never
    /// more than one instance alive, and static fields only added risk without buying anything.
    /// </summary>
    public partial class CollectDiesForm : System.Windows.Forms.Form
    {
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
        /// the user zero out phase params before collecting).</summary>
        private void CollectDiesForm_Load(object sender, EventArgs e)
        {
            GMSRevitAddin.DarkTheme.MarkPrimary(button_Start);
            GMSRevitAddin.DarkTheme.Apply(this);
            this.Text = "Collect Detail Items";

            button_Start.Enabled = true;
            button_Start.Visible = true;
            checkBoxCollectAll.Checked = false;

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
        }

        /// <summary>
        /// If any phase checkboxes are checked, first zeroes out those phase parameters on every
        /// detail-component type in the model, then — if a sheet-set checkbox is checked and the phase
        /// step didn't error — walks every project sheet whose "Grouping - Usage" matches a selected
        /// set, collects distinct family+type combinations from its drafting-view viewports (bucketed
        /// by <see cref="ClassifyFamily"/>), and places one instance of each into a new drafting view.
        /// Both steps run inside a single <see cref="TransactionGroup"/> so an unexpected failure
        /// midway (e.g. while building the new view) rolls back the phase-void step too, instead of
        /// leaving phase parameters zeroed with no resulting view. Any placement failures are
        /// collected and written into a text note on the new view instead of aborting.
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

                            switch (ClassifyFamily(familyAndType))
                            {
                                case FamilyBucket.Fastener: fastenerList.Add(fs); break;
                                case FamilyBucket.Component: gasketList.Add(fs); break;
                                case FamilyBucket.Extrusion: extrusionList.Add(fs); break;
                                case FamilyBucket.DoNotSchedule: doNotSchedule.Add(fs); break;
                                case FamilyBucket.Other:
                                    if (checkBoxCollectAll.Checked) otherFamilies.Add(fs);
                                    break;
                            }
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

                    string errorList = "";

                    // Each family bucket is placed down its own column (x offset), stacked vertically
                    // (y decreasing) within the column.
                    double x = 1.5;
                    PlaceColumn(draftView, fastenerList, x, .5, phases, placeProgress, ref errorList);

                    x += 1.5;
                    PlaceColumn(draftView, gasketList, x, .5, phases, placeProgress, ref errorList);

                    x += 1.5;
                    PlaceColumn(draftView, extrusionList, x, 1, phases, placeProgress, ref errorList);

                    // "Collect All" additionally places every do-not-schedule variant (Plan/Mod
                    // families) and any other family type not matched by the buckets above.
                    if (checkBoxCollectAll.Checked)
                    {
                        x += 1.5;
                        List<FamilySymbol> combined = new List<FamilySymbol>(doNotSchedule);
                        combined.AddRange(otherFamilies);
                        PlaceColumn(draftView, combined, x, 1, phases, placeProgress, ref errorList, flagOnly: doNotSchedule);
                    }

                    if (!string.IsNullOrWhiteSpace(errorList))
                    {
                        ElementId defaultTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);
                        TextNote.Create(doc, draftView.Id, new XYZ(0, 0, 0), .25, errorList, defaultTypeId);
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
        }

        /// <summary>Places one instance of each symbol in <paramref name="column"/> down a single
        /// column at <paramref name="x"/>, stacked by <paramref name="rowHeight"/>. Every placed
        /// instance's type has <paramref name="phases"/> flagged "do not schedule" (value 1) unless
        /// <paramref name="flagOnly"/> is given, in which case only symbols in that list are flagged
        /// (used by the combined do-not-schedule + other column). Placement failures are logged and
        /// appended to <paramref name="errorList"/> instead of aborting the rest of the column.</summary>
        private void PlaceColumn(ViewDrafting draftView, List<FamilySymbol> column, double x, double rowHeight,
            List<string> phases, ProgressForm.ProgressForm progress, ref string errorList, List<FamilySymbol> flagOnly = null)
        {
            double y = 0;
            foreach (FamilySymbol fmsy in column)
            {
                y -= rowHeight;
                progress.Visible = true;
                progress.Increment();

                try
                {
                    FamilyInstance newFI = doc.Create.NewFamilyInstance(new XYZ(x, y, 0), fmsy, draftView);
                    if (flagOnly == null || flagOnly.Contains(fmsy))
                    {
                        Element elm = doc.GetElement(newFI.GetTypeId());
                        updateParameters(phases, elm);
                    }
                }
                catch (Exception ex)
                {
                    y += rowHeight;
                    GMSRevitAddin.GmsLog.Error("CollectDies: failed to place " + fmsy.FamilyName + " - " + fmsy.Name, ex);
                    errorList += fmsy.FamilyName + " - " + fmsy.Name + System.Environment.NewLine;
                }
            }
        }

        private enum FamilyBucket { Fastener, Component, Extrusion, DoNotSchedule, Other }

        /// <summary>
        /// Buckets a family+type by the GMS naming convention: plain "Fastener"/"Extrusion" families
        /// are scheduled columns; their "...Plan"/"...Mod" variants are placed but flagged "do not
        /// schedule"; "Component" families (gaskets) are always scheduled; anything else is
        /// <see cref="FamilyBucket.Other"/> and is only collected when "Collect All" is checked. Pulled
        /// out of the placement loop into one place so the rule set is easy to find and extend.
        /// </summary>
        private static FamilyBucket ClassifyFamily(string familyAndType)
        {
            string name = familyAndType.ToLowerInvariant();
            if (name.StartsWith("fastener"))
            {
                return name.Contains("plan") ? FamilyBucket.DoNotSchedule : FamilyBucket.Fastener;
            }
            if (name.StartsWith("component"))
            {
                return FamilyBucket.Component;
            }
            if (name.StartsWith("extrusion"))
            {
                return name.Contains("mod") ? FamilyBucket.DoNotSchedule : FamilyBucket.Extrusion;
            }
            return FamilyBucket.Other;
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
