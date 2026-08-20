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
    /// </summary>
    public partial class CollectDiesForm : System.Windows.Forms.Form
    {
        public static List<string> sheetSets = new List<string>();
        public static Autodesk.Revit.DB.Document doc = null;
        public static Autodesk.Revit.UI.UIDocument uiDoc = null;
        public static List<string> phaseTypes = new List<string>();
        public static List<FamilySymbol> fastenerList = new List<FamilySymbol>();
        public static List<FamilySymbol> gasketList = new List<FamilySymbol>();
        public static List<FamilySymbol> extrusionList = new List<FamilySymbol>();
        public static List<FamilySymbol> doNotSchedule = new List<FamilySymbol>();
        public static List<FamilySymbol> otherFamilies = new List<FamilySymbol>();
        public static ProgressForm.ProgressForm currentPForm = null;

        public CollectDiesForm()
        {
            InitializeComponent();
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
            // parameter names in use in this model (stops at the first type that has any).
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
        /// detail-component type in the model (one transaction, with progress). Then, if a sheet-set
        /// checkbox is checked and the phase step didn't error, walks every project sheet whose
        /// "Grouping - Usage" matches a selected set, collects distinct family+type combinations from
        /// its drafting-view viewports (bucketed into fastener/gasket/extrusion/other lists by a
        /// name-prefix heuristic), and places one instance of each into a new drafting view — flagging
        /// the "do not schedule" phase parameter (value 1) on fastener-plan/extrusion-mod variants and
        /// on the optional "other" bucket. Any placement failures are collected and written into a
        /// text note on the new view instead of aborting.
        /// </summary>
        private void button_Start_Click(object sender, EventArgs e)
        {
            var items = checkedListBox1.CheckedIndices;
            List<string> selected = new List<string>();

            var checkedPhases = checkedListBox2.CheckedIndices;
            List<string> phases = new List<string>();
            foreach (int indexChecked in checkedPhases)
            {
                phases.Add(checkedListBox2.Items[indexChecked].ToString());
            }

            bool error = false;
            List<string> errors = new List<string>();

            if (phases.Any())
            {
                FilteredElementCollector fec = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_DetailComponents).WhereElementIsElementType();
                List<Element> families = fec.ToList();

                string st = "Processing element {0} of " + families.Count.ToString() + "...";
                string fi = "Regenerating model...";
                string caption = "Void Selected Phase Information";

                using (ProgressForm.ProgressForm pform = new ProgressForm.ProgressForm(caption, st, families.Count, fi))
                {
                    errors.Clear();
                    error = false;
                    currentPForm = pform;
                    LaunchForm.thisForm.Visible = false;

                    using (Transaction tr = new Transaction(doc, "Void Component Phases"))
                    {
                        tr.Start();


                        foreach (Element el in families)
                        {
                            pform.Visible = true;
                            pform.Increment();

                            ParameterSet paramSet = el.Parameters;
                            foreach (Parameter p in paramSet)
                            {
                                if (phases.Contains(p.Definition.Name))
                                {
                                    try
                                    {
                                        p.Set((int)0);
                                    }
                                    catch (Exception exc)
                                    {
                                        errors.Add(el.Id.ToString() + " - " + exc.Message);
                                        error = true;
                                    }
                                }
                            }
                        }
                        tr.Commit();
                    }
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

            if (items.Count > 0 && !error)
            {
                selected.Clear();
                foreach (int index in items)
                {
                    if (sheetSets[index] != "???")
                    {
                        selected.Add(sheetSets[index]);
                    }
                    else
                    {
                        selected.Add("");
                    }
                }

                List<string> familyNames = new List<string>();
                fastenerList.Clear();
                gasketList.Clear();
                extrusionList.Clear();
                doNotSchedule.Clear();
                otherFamilies.Clear();

                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector = collector.OfCategory(BuiltInCategory.OST_Sheets).WhereElementIsNotElementType().OfClass(typeof(ViewSheet));

                List<Element> elemList = collector.ToElements().ToList();

                string s = "Processing sheet {0} of " + elemList.Count.ToString() + "...";
                string f = "Creating new sheet and regenerating model...";
                string captionText = "Collect Detail Items";

                using (ProgressForm.ProgressForm pf = new ProgressForm.ProgressForm(captionText, s, elemList.Count, f))
                {
                    currentPForm = pf;
                    LaunchForm.thisForm.Visible = false;

                    foreach (Element elem in elemList)
                    {
                        pf.Visible = true;
                        pf.Increment();
                        ViewSheet vs = (ViewSheet)elem;
                        ParameterSet parameters = vs.Parameters;

                        foreach (Parameter param in parameters)
                        {
                            if (param.Definition.Name == "Grouping - Usage" && selected.Contains(param.AsString()))
                            {
                                ICollection<ElementId> listViews = vs.GetAllViewports();
                                foreach (ElementId eid in listViews)
                                {
                                    Element ElementViewport = doc.GetElement(eid);
                                    Viewport viewport = ElementViewport as Viewport;
                                    ElementId vpID = viewport.ViewId;
                                    Autodesk.Revit.DB.View currentView = doc.GetElement(vpID) as Autodesk.Revit.DB.View;
                                    if (checkBoxCollectAllViewTypes.Checked || currentView.ViewType == ViewType.DraftingView)
                                    {
                                        FilteredElementCollector fec = new FilteredElementCollector(doc, vpID);
                                        fec = fec.OfClass(typeof(FamilyInstance));
                                        List<Element> families = fec.ToList();

                                        foreach (Element fam in families)
                                        {
                                            FamilyInstance fi = fam as FamilyInstance;
                                            FamilySymbol fs = fi.Symbol;
                                            string familyAndType = fi.get_Parameter(BuiltInParameter.ELEM_FAMILY_AND_TYPE_PARAM).AsValueString();

                                            // Bucket by family-name prefix: plain "Fastener"/"Extrusion"
                                            // families are scheduled columns; "...Plan"/"...Mod" variants
                                            // are placed but flagged "do not schedule"; anything else only
                                            // gets collected if "Collect All" is checked.
                                            if (!string.IsNullOrWhiteSpace(familyAndType) && !familyNames.Contains(familyAndType))
                                            {
                                                familyNames.Add(familyAndType);
                                                if (familyAndType.ToLower().StartsWith("fastener") && !familyAndType.ToLower().Contains("plan"))
                                                {
                                                    fastenerList.Add(fs);
                                                }
                                                else if (familyAndType.ToLower().StartsWith("component"))
                                                {
                                                    gasketList.Add(fs);
                                                }
                                                else if (familyAndType.ToLower().StartsWith("extrusion") && !familyAndType.ToLower().Contains("mod"))
                                                {
                                                    extrusionList.Add(fs);
                                                }
                                                else if (familyAndType.ToLower().StartsWith("fastener") && familyAndType.ToLower().Contains("plan"))
                                                {
                                                    doNotSchedule.Add(fs);
                                                }
                                                else if (familyAndType.ToLower().StartsWith("extrusion") && familyAndType.ToLower().Contains("mod"))
                                                {
                                                    doNotSchedule.Add(fs);
                                                }
                                                else if (checkBoxCollectAll.Checked)
                                                {
                                                    otherFamilies.Add(fs);
                                                }
                                            }
                                        }
                                    }
                                }
                                break;
                            }
                        }
                    }

                    string newViewName = "";
                    foreach (string type in selected)
                    {
                        if (!string.IsNullOrWhiteSpace(type))
                        {
                            newViewName += type + " ";
                        }
                        else
                        {
                            newViewName += "NO USAGE TYPE" + " ";
                        }
                    }

                    ViewDrafting newView = null;
                    using (Transaction tr1 = new Transaction(doc, "Create New DraftingView"))
                    {
                        tr1.Start();
                        ViewFamilyType vd = new FilteredElementCollector(doc).OfClass(typeof(ViewFamilyType)).Cast<ViewFamilyType>().FirstOrDefault(q => q.ViewFamily == ViewFamily.Drafting);

                        ViewDrafting draftView = ViewDrafting.Create(doc, vd.Id);
                        draftView.Name = "Detail Items - " + newViewName + DateTime.Now.ToString("yyyyMMddHHmmss");
                        newView = draftView;

                        double x = 0;
                        double y = 0;
                        string errorList = "";

                        // Each family bucket is placed down its own column (x offset), stacked
                        // vertically (y decreasing) within the column.
                        x += 1.5;
                        y = 0;
                        foreach (FamilySymbol fastenerFmsy in fastenerList)
                        {
                            y -= .5;

                            try
                            {
                                FamilyInstance newFI = doc.Create.NewFamilyInstance(new XYZ(x, y, 0), fastenerFmsy, draftView);
                                Element elm = doc.GetElement(newFI.GetTypeId());
                                updateParameters(phases, elm);
                            }
                            catch
                            {
                                y += .5;
                                errorList += fastenerFmsy.FamilyName + " - " + fastenerFmsy.Name + System.Environment.NewLine;
                            }
                        }

                        x += 1.5;
                        y = 0;
                        foreach (FamilySymbol componentFmsy in gasketList)
                        {
                            y -= .5;

                            try
                            {
                                FamilyInstance newFI = doc.Create.NewFamilyInstance(new XYZ(x, y, 0), componentFmsy, draftView);
                                Element elm = doc.GetElement(newFI.GetTypeId());
                                updateParameters(phases, elm);
                            }
                            catch
                            {
                                y += .5;
                                errorList += componentFmsy.FamilyName + " - " + componentFmsy.Name + System.Environment.NewLine;
                            }
                        }

                        x += 1.5;
                        y = 0;
                        foreach (FamilySymbol extrusionFmsy in extrusionList)
                        {
                            y -= 1;

                            try
                            {
                                FamilyInstance newFI = doc.Create.NewFamilyInstance(new XYZ(x, y, 0), extrusionFmsy, draftView);
                                Element elm = doc.GetElement(newFI.GetTypeId());
                                updateParameters(phases, elm);
                            }
                            catch
                            {
                                y += 1;
                                errorList += extrusionFmsy.FamilyName + " - " + extrusionFmsy.Name + System.Environment.NewLine;
                            }
                        }

                        // "Collect All" additionally places every do-not-schedule variant (Plan/Mod
                        // families) and any other family type not matched by the buckets above.
                        if (checkBoxCollectAll.Checked)
                        {
                            x += 1.5;
                            y = 0;
                            List<FamilySymbol> combined = new List<FamilySymbol>();
                            combined.Clear();
                            foreach (FamilySymbol fs in doNotSchedule)
                            {
                                combined.Add(fs);
                            }
                            foreach (FamilySymbol fs1 in otherFamilies)
                            {
                                combined.Add(fs1);
                            }
                            foreach (FamilySymbol fmsy in combined)
                            {
                                y -= 1;

                                try
                                {
                                    FamilyInstance newFI = doc.Create.NewFamilyInstance(new XYZ(x, y, 0), fmsy, draftView);
                                    if (doNotSchedule.Contains(fmsy))
                                    {
                                        Element elm = doc.GetElement(newFI.GetTypeId());
                                        updateParameters(phases, elm);
                                    }
                                }
                                catch
                                {
                                    y += 1;
                                    errorList += fmsy.FamilyName + " - " + fmsy.Name + System.Environment.NewLine;
                                }
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(errorList))
                        {
                            ElementId defaultTypeId = doc.GetDefaultElementTypeId(ElementTypeGroup.TextNoteType);

                            TextNote note = TextNote.Create(doc, draftView.Id, new XYZ(0, 0, 0), .25, errorList, defaultTypeId);
                        }

                        tr1.Commit();
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
                closeForm();
            }
            else
            {
                //do nothing
            }
        }

        /// <summary>Sets every phase parameter named in <paramref name="phases"/> to 1 ("do not
        /// schedule") on the given element's type — used to flag Plan/Mod family instances placed
        /// into the collection view.</summary>
        public void updateParameters(List<string> phases, Element el)
        {
            ParameterSet pSet = el.Parameters;
            foreach (Parameter p in pSet)
            {
                if (phases.Contains(p.Definition.Name))
                {
                    p.Set((int)1);
                }
            }
        }

        public static void closeForm()
        {
            LaunchForm.thisForm.Close();
        }
    }

    /// <summary>
    /// Entry point wired as the ribbon's "Collect Detail Items" command. Gathers the distinct
    /// "Grouping - Usage" values present on the project's sheets (used to populate
    /// <see cref="CollectDiesForm"/>'s sheet-set checklist), then shows the form modally.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchForm : IExternalCommand
    {
        public static System.Windows.Forms.Form thisForm = null;
        public static ExternalCommandData ECD = null;
        public static string mess = null;
        public static ElementSet ElSet = null;
        public static Document doc = null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            CollectDiesForm.sheetSets.Clear();
            ECD = commandData;
            mess = message;
            ElSet = elements;
            UIApplication uiApp = ECD.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            UIDocument uidoc = uiApp.ActiveUIDocument;
            CollectDiesForm.doc = doc;
            CollectDiesForm.uiDoc = uidoc;

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector = collector.OfCategory(BuiltInCategory.OST_Sheets).WhereElementIsNotElementType().OfClass(typeof(ViewSheet));

            var sheets = collector.ToElements();

            foreach (Element elem in sheets)
            {
                ParameterSet parameters = elem.Parameters;

                foreach (Parameter param in parameters)
                {
                    if (param.Definition.Name == "Grouping - Usage")
                    {
                        if (!CollectDiesForm.sheetSets.Contains(param.AsString()))
                        {
                            CollectDiesForm.sheetSets.Add(param.AsString());
                        }
                        break;
                    }
                }
            }

            System.Windows.Forms.Form form = new CollectDiesForm();
            thisForm = form;
            form.ShowDialog();
            return Result.Succeeded;
        }
    }
}