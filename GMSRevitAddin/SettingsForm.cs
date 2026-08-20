using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Visual;
using System.Threading;
using System.Data.OleDb;
using System.IO;
using Application = Autodesk.Revit.ApplicationServices.Application;
using System.Windows.Controls;
using static System.Net.WebRequestMethods;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace SettingsForm
{
    /// <summary>
    /// The GMS Tools "Settings" dialog. Shows/edits the per-project "Parts Manager Location" and
    /// "Fastener Image Scale" Revit GlobalParameters, several add-in behavior toggles persisted in
    /// <c>Properties.Settings</c> (warning suppression, unit-line visibility, open-original/open-new
    /// on link, view-name display), and hosts the "Purge Linked Model" and "Batch Update View Names"
    /// utility buttons. Opened modally by <see cref="LaunchForm"/> (wired as the ribbon's Settings
    /// button command).
    /// </summary>
    public partial class SettingsForm : System.Windows.Forms.Form
    {
        public SettingsForm()
        {
            InitializeComponent();
        }

        /// <summary>
        /// Applies the shared theme, loads (or creates, via <see cref="LaunchForm.LoadConfig"/>) the
        /// per-project GMS config, and populates every control from the current GlobalParameter /
        /// <c>Properties.Settings</c> values.
        /// </summary>
        private void SettingsForm_Load(object sender, EventArgs e)
        {
            // buttonClose is the dialog's only "proceed" action (no separate Cancel), so it gets the
            // primary treatment; buttonPurge stays neutral gray since it's a distinct, riskier action
            // that shouldn't be the visually emphasized control.
            GMSRevitAddin.DarkTheme.MarkPrimary(buttonClose);
            GMSRevitAddin.DarkTheme.Apply(this);
            this.ActiveControl = buttonClose;

            // There's no meaningful version number to show (the csproj never sets an
            // AssemblyVersion, so it's always the SDK default 1.0.0.0) — the build timestamp is
            // what actually identifies which build is currently deployed.
            textBoxBuildInfo.Text = "GMSRevitAddin  ·  Revit " + GMSRevitAddin.GmsVersion.Number +
                "  ·  Build " + GMSRevitAddin.GmsVersion.BuildTimestamp;

            // LoadConfig ensures the "GMS config" folder + GlobalParameters exist for this project
            // (workshared, non-detached, non-family docs only) and returns the central model path.
            string modelPath = LaunchForm.LoadConfig();
            if (string.IsNullOrWhiteSpace(modelPath))
            {
                // Not a workshared/central model (or config setup failed) — no Parts Manager location applies.
                textBoxPMLocation.Text = "";
                textBoxPMLocation.Enabled = false;
                buttonBrowseForPM.Enabled = false;
            }
            else
            {
                textBoxPMLocation.Enabled = true;
                buttonBrowseForPM.Enabled = true;
                ElementId gpID = ElementId.InvalidElementId;
                if (GlobalParametersManager.AreGlobalParametersAllowed(LaunchForm.docu))
                {
                    gpID = GlobalParametersManager.FindByName(LaunchForm.docu, "Parts Manager Location");
                }

                string partsManagerLocation = "Not found.";

                if (gpID != null && gpID != ElementId.InvalidElementId)
                {
                    GlobalParameter gp = LaunchForm.docu.GetElement(gpID) as GlobalParameter;
                    StringParameterValue spv = gp.GetValue() as StringParameterValue;
                    string text = spv.Value;
                    if (text != "Not assigned")
                    {
                        // Display the UNC share path as its mapped drive letter for readability.
                        partsManagerLocation = text.Replace("\\\\gmsfs01\\Common\\Projects", "J:");
                    }
                    else
                    {
                        partsManagerLocation = "Not assigned";
                    }
                }

                textBoxPMLocation.Text = partsManagerLocation;
            }

            if (GMSRevitAddin.Properties.Settings.Default.AutoWarningSuppress == true)
            {
                checkBoxWarningSuppress.Checked = true;
            }
            else
            {
                checkBoxWarningSuppress.Checked = false;
            }
            if (GMSRevitAddin.Properties.Settings.Default.OpenOrig == true)
            {
                checkBoxOpenOrig.Checked = true;
            }
            else
            {
                checkBoxOpenOrig.Checked = false;
            }
            if (GMSRevitAddin.Properties.Settings.Default.OpenNew == true)
            {
                checkBoxOpenNew.Checked = true;
            }
            else
            {
                checkBoxOpenNew.Checked = false;
            }
            if (GMSRevitAddin.Properties.Settings.Default.UnitLines == true)
            {
                checkBoxUnitLines.Checked = true;
            }
            else
            {
                checkBoxUnitLines.Checked = false;
            }
            if (GMSRevitAddin.Properties.Settings.Default.ViewName == true)
            {
                checkBoxViewName.Checked = true;
            }
            else
            {
                checkBoxViewName.Checked = false;
            }

            // "IsUniqueName" returning false means the GlobalParameter already exists — read its
            // current value; otherwise default the control to 10 (matches LoadConfig's default).
            if (!GlobalParametersManager.IsUniqueName(LaunchForm.docu, "Fastener Image Scale"))
            {
                ElementId fsGpId = ElementId.InvalidElementId;
                fsGpId = GlobalParametersManager.FindByName(LaunchForm.docu, "Fastener Image Scale");
                if(fsGpId != ElementId.InvalidElementId)
                {
                    GlobalParameter fsGp = LaunchForm.docu.GetElement(fsGpId) as GlobalParameter;
                    IntegerParameterValue integerParameterValue = fsGp.GetValue() as IntegerParameterValue;
                    numericUpDownFastenerScale.Value = integerParameterValue.Value;
                }
                else
                {
                    numericUpDownFastenerScale.Value = 10;
                }
            }
            else
            {
                numericUpDownFastenerScale.Value = 10;
            }
        }

        /// <summary>Lets the user browse for the Parts Manager Access DB and writes the chosen path
        /// into the "Parts Manager Location" GlobalParameter (consumed by <c>ExportParts.cs</c>).</summary>
        private void buttonBrowseForPM_Click(object sender, EventArgs e)
        {
            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                ElementId gpID = ElementId.InvalidElementId;
                if (GlobalParametersManager.AreGlobalParametersAllowed(LaunchForm.docu))
                {
                    using (Transaction tr = new Transaction(LaunchForm.docu))
                    {
                        tr.Start("Update Parts Manager Parameter");

                        gpID = GlobalParametersManager.FindByName(LaunchForm.docu, "Parts Manager Location");
                        GlobalParameter gp = LaunchForm.docu.GetElement(gpID) as GlobalParameter;
                        StringParameterValue spv = gp.GetValue() as StringParameterValue;
                        spv.Value = openFileDialog1.FileName;
                        gp.SetValue(spv);

                        string text = spv.Value;
                        text = text.Replace("\\\\gmsfs01\\Common\\Projects", "J:"); // display-only mapped-drive form
                        textBoxPMLocation.Text = text;

                        tr.Commit();
                    }
                }
            }
        }

        /// <summary>Persists the checkbox settings + fastener scale back to <c>Properties.Settings</c>
        /// / the "Fastener Image Scale" GlobalParameter, then closes the dialog.</summary>
        private void buttonClose_Click(object sender, EventArgs e)
        {
            if (checkBoxOpenOrig.Checked)
            {
                GMSRevitAddin.Properties.Settings.Default.OpenOrig = true;
                GMSRevitAddin.Properties.Settings.Default.Save();
            }
            else
            {
                GMSRevitAddin.Properties.Settings.Default.OpenOrig = false;
                GMSRevitAddin.Properties.Settings.Default.Save();
            }
            if (checkBoxOpenNew.Checked)
            {
                GMSRevitAddin.Properties.Settings.Default.OpenNew = true;
                GMSRevitAddin.Properties.Settings.Default.Save();
            }
            else
            {
                GMSRevitAddin.Properties.Settings.Default.OpenNew = false;
                GMSRevitAddin.Properties.Settings.Default.Save();
            }
            if (checkBoxViewName.Checked)
            {
                GMSRevitAddin.Properties.Settings.Default.ViewName = true;
                GMSRevitAddin.Properties.Settings.Default.Save();
            }
            else
            {
                GMSRevitAddin.Properties.Settings.Default.ViewName = false;
                GMSRevitAddin.Properties.Settings.Default.Save();
            }

            if (!GlobalParametersManager.IsUniqueName(LaunchForm.docu, "Fastener Image Scale"))
            {
                ElementId fsGpId = ElementId.InvalidElementId;
                fsGpId = GlobalParametersManager.FindByName(LaunchForm.docu, "Fastener Image Scale");
                if (fsGpId != ElementId.InvalidElementId)
                {
                    using (Transaction tr = new Transaction(LaunchForm.docu))
                    {
                        tr.Start("Update Fastener Image Scale Parameter");
                        GlobalParameter fsGp = LaunchForm.docu.GetElement(fsGpId) as GlobalParameter;
                        IntegerParameterValue integerParameterValue = fsGp.GetValue() as IntegerParameterValue;
                        integerParameterValue.Value = Convert.ToInt32(numericUpDownFastenerScale.Value);
                        fsGp.SetValue(integerParameterValue);
                        tr.Commit();
                    }
                }
            }

            LaunchForm.thisForm.Close();
        }

        /// <summary>Toggles automatic Revit warning suppression on/off for the session, both updating
        /// the shared state flag and (re)registering/unregistering the suppression event handler.</summary>
        private void checkBoxWarningSuppress_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxWarningSuppress.Checked == true)
            {
                RevitWarningSuppresion.registerWarningSuppresion.updatedbool(true);
            }
            else
            {
                RevitWarningSuppresion.registerWarningSuppresion.updatedbool(false);
            }

            LaunchForm.ExecuteWarningSuppression(checkBoxWarningSuppress.Checked);
        }

        /// <summary>Runs the "Batch Update View Names" utility against the active document.</summary>
        private void buttonBatchUpdateViewNames_Click(object sender, EventArgs e)
        {
            ViewNames.ManualUpdate.Update(LaunchForm.docu);
        }

        private void checkBoxUnitLines_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxUnitLines.Checked)
            {
                NotaReference.RevitStartup.updatedbool(true);
            }
            else
            {
                NotaReference.RevitStartup.updatedbool(false);
            }
        }

        public static ProgressForm.ProgressForm currentPForm = null;

        /// <summary>
        /// "Purge Linked Model" — only runs against a **detached**, non-family document (a copy meant
        /// to be stripped down for cross-firm sharing). Deletes RVT/DWG links, point clouds, scope
        /// boxes, all views except the active one, all schedules, all sheets except the active one,
        /// unused families (multi-pass), and reference planes/lines; reassigns Levels/Grids to a
        /// "Shared Levels and Grids" workset; then posts Revit's native "Purge Unused" command. Reports
        /// progress via the shared modeless <c>ProgressForm</c> across a dozen discrete steps.
        /// </summary>
        private void buttonPurge_Click(object sender, EventArgs e)
        {
            if (LaunchForm.docu.IsDetached && !LaunchForm.docu.IsFamilyDocument)
            {
                this.Hide();
                using (ProgressForm.ProgressForm pgfm = new ProgressForm.ProgressForm("Purge Linked Model", null, 12, string.Empty))
                {

                    Document doc = LaunchForm.docu;
                    UIDocument uidoc = LaunchForm.uidoc;
                    currentPForm = pgfm;
                    ElementId currentView = uidoc.ActiveView.Id;

                    //disable analytical model
                    pgfm.IncrementWithText("Disable Analytical Model");
                    if (doc.Application.IsStructuralAnalysisEnabled)
                    {
                        using (Transaction t = new Transaction(doc, "Disable Analytical Model"))
                        {
                            t.Start();
                            doc.Application.IsStructuralAnalysisEnabled = false;
                            t.Commit();
                        }
                    }

                    //delete .rvt and .dwg links and point clouds (all external file references)
                    pgfm.IncrementWithText("Deleting Links");
                    IList<ElementId> IdsToDelete = new List<ElementId>();

                    ICollection<ElementId> extFileIDs = ExternalFileUtils.GetAllExternalFileReferences(doc);
                    foreach (ElementId elid in extFileIDs)
                    {
                        if (!IdsToDelete.Contains(elid))
                        {
                            IdsToDelete.Add(elid);
                        }
                    }

                    FilteredElementCollector fff = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_PointClouds);
                    foreach (Element el in fff)
                    {
                        if (!IdsToDelete.Contains(el.Id))
                        {
                            IdsToDelete.Add(el.Id);
                        }
                    }

                    if (IdsToDelete.Any())
                    {
                        // Deleted one at a time (own transaction each) so one bad reference doesn't
                        // block deletion of the rest.
                        foreach (ElementId eid in IdsToDelete)
                        {
                            try
                            {
                                using (Transaction t = new Transaction(doc, "Delete Links"))
                                {
                                    t.Start();
                                    doc.Delete(eid);
                                    t.Commit();
                                }
                            }
                            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("SettingsForm", __ex); }
                        }
                    }

                    //delete all scope boxes
                    IdsToDelete.Clear();
                    pgfm.IncrementWithText("Deleting Scope Boxes");
                    foreach (Element ScheduleElement in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_VolumeOfInterest))
                    {
                        if (!IdsToDelete.Contains(ScheduleElement.Id))
                        {
                            IdsToDelete.Add(ScheduleElement.Id);
                        }
                    }
                    using (Transaction t = new Transaction(doc, "Delete Scope Boxes"))
                    {
                        t.Start();
                        doc.Delete(IdsToDelete);
                        t.Commit();
                    }

                    //delete all views except the currently active view (kept so the user has
                    //something open once the purge finishes)
                    IdsToDelete.Clear();
                    pgfm.IncrementWithText("Deleting Views");
                    foreach (Element ViewElement in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views))
                    {
                        if (!IdsToDelete.Contains(ViewElement.Id) && ViewElement.Id != currentView)
                        {
                            IdsToDelete.Add(ViewElement.Id);
                        }
                    }

                    if (IdsToDelete.Any())
                    {
                        using (Transaction t = new Transaction(doc, "Delete Views"))
                        {
                            t.Start();
                            doc.Delete(IdsToDelete);
                            t.Commit();
                        }
                    }

                    //delete all schedules
                    IdsToDelete.Clear();
                    pgfm.IncrementWithText("Deleting Schedules");
                    foreach (Element ScheduleElement in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Schedules))
                    {
                        if (!IdsToDelete.Contains(ScheduleElement.Id))
                        {
                            IdsToDelete.Add(ScheduleElement.Id);
                        }
                    }
                    using (Transaction t = new Transaction(doc, "Delete Schedules"))
                    {
                        t.Start();
                        doc.Delete(IdsToDelete);
                        t.Commit();
                    }

                    //delete all sheets except the currently active one (if it happens to be a sheet)
                    IdsToDelete.Clear();
                    pgfm.IncrementWithText("Deleting Sheets");
                    foreach (Element SheetElement in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sheets))
                    {
                        if (!IdsToDelete.Contains(SheetElement.Id) && SheetElement.Id != currentView)
                        {
                            IdsToDelete.Add(SheetElement.Id);
                        }
                    }
                    using (Transaction t = new Transaction(doc, "Delete Sheets"))
                    {
                        t.Start();
                        doc.Delete(IdsToDelete);
                        t.Commit();
                    }

                    //delete all unused families — up to 3 passes, since removing one unused family
                    //can make another family (that referenced it) newly unused
                    bool continueDUE = false;
                    pgfm.IncrementWithText("Deleting Unused Families. Pass 1 of 3");
                    continueDUE = deleteUnused(1, doc);
                    if (continueDUE)
                    {
                        continueDUE = false;
                        pgfm.IncrementWithText("Deleting Unused Families. Pass 2 of 3");
                        continueDUE = deleteUnused(2, doc);
                        if (continueDUE)
                        {
                            pgfm.IncrementWithText("Deleting Unused Families. Pass 3 of 3");
                            deleteUnused(3, doc);
                        }
                        else
                        {
                            pgfm.IncrementWithText("Deleting Unused Families. Pass 3 of 3");
                        }
                    }
                    else
                    {
                        pgfm.IncrementWithText("Deleting Unused Families. Pass 2 of 3");
                        pgfm.IncrementWithText("Deleting Unused Families. Pass 3 of 3");
                    }

                    //delete all reference planes and reference lines
                    IdsToDelete.Clear();
                    pgfm.IncrementWithText("Deleting Reference Planes and Lines");

                    foreach (Element refPlaneElement in new FilteredElementCollector(doc).OfClass(typeof(ReferencePlane)))
                    {
                        if (!IdsToDelete.Contains(refPlaneElement.Id))
                        {
                            IdsToDelete.Add(refPlaneElement.Id);
                        }
                    }
                    foreach (Element refLineElement in new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_ReferenceLines))
                    {
                        if (!IdsToDelete.Contains(refLineElement.Id))
                        {
                            IdsToDelete.Add(refLineElement.Id);
                        }
                    }
                    foreach (ElementId eid in IdsToDelete)
                    {
                        try
                        {
                            using (Transaction t = new Transaction(doc, "Delete Ref Plane"))
                            {
                                t.Start();
                                doc.Delete(eid);
                                t.Commit();
                            }
                        }
                        catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("SettingsForm", __ex); }
                    }

                    //Levels and Grids to correct workset
                    pgfm.IncrementWithText("Levels and Grids to \"Shared Levels and Grids\" workset");

                    if (doc.IsWorkshared)
                    {
                        Workset sharedWS = null;
                        string wsName = "Shared Levels and Grids";
                        // "IsWorksetNameUnique" true means the workset does NOT exist yet — create it;
                        // otherwise look it up so grids/levels get reassigned to the existing one.
                        if (WorksetTable.IsWorksetNameUnique(doc, wsName))
                        {
                            using (Transaction t = new Transaction(doc, "Create Workset"))
                            {
                                t.Start();
                                sharedWS = Workset.Create(doc, wsName);
                                t.Commit();
                            }
                        }
                        else
                        {
                            FilteredWorksetCollector fwc = new FilteredWorksetCollector(doc).OfKind(WorksetKind.UserWorkset);
                            sharedWS = fwc.First<Workset>(w => w.Name == "Shared Levels and Grids");
                        }

                        if (null == sharedWS)
                        {
                            MessageBox.Show("Unable to locate or create \"Shared Levels and Grids\" workset", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else
                        {
                            FilteredElementCollector grids = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.Grid));
                            FilteredElementCollector levels = new FilteredElementCollector(doc).OfClass(typeof(Level));
                            FilteredElementCollector multiseg = new FilteredElementCollector(doc).OfClass(typeof(MultiSegmentGrid));
                            if (grids.Any() || levels.Any() || multiseg.Any())
                            {
                                using (Transaction t = new Transaction(doc, "Change Workset"))
                                {
                                    t.Start();
                                    if (grids.Any())
                                    {
                                        foreach (Element elG in grids)
                                        {
                                            Parameter wsparam = elG.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                                            if (!wsparam.IsReadOnly)
                                            {
                                                wsparam.Set(sharedWS.Id.IntegerValue);
                                            }
                                        }
                                    }
                                    if (levels.Any())
                                    {
                                        foreach (Element elL in levels)
                                        {
                                            Parameter wsparam = elL.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                                            if (!wsparam.IsReadOnly)
                                            {
                                                wsparam.Set(sharedWS.Id.IntegerValue);
                                            }
                                        }
                                    }
                                    if (multiseg.Any())
                                    {
                                        foreach(Element elM in multiseg)
                                        {
                                            Parameter wsparam = elM.get_Parameter(BuiltInParameter.ELEM_PARTITION_PARAM);
                                            if (!wsparam.IsReadOnly)
                                            {
                                                wsparam.Set(sharedWS.Id.IntegerValue);
                                            }
                                        }
                                    }
                                    t.Commit();
                                }
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("This is not a workshared model. Cannot create \"Shared Levels and Grids\" workset", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }

                    pgfm.IncrementWithText("Clean Linked Model complete");
                }

                var commandId = RevitCommandId.LookupPostableCommandId(PostableCommand.PurgeUnused);

                LaunchForm.uiapp.PostCommand(commandId);
                this.Close();
            }
            else if (!LaunchForm.docu.IsDetached)
            {
                MessageBox.Show("Revit Model is not detached. Cancelling process.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else if (LaunchForm.docu.IsFamilyDocument)
            {
                MessageBox.Show("Current document is a family, not a project. Cancelling process.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            else
            {
                MessageBox.Show("Error attempting to clean up model. Cancelling process.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Runs Revit's built-in "purge unused elements" performance-adviser rule and deletes every
        /// element it flags, in one transaction. Returns true if anything was deleted (signaling the
        /// caller it's worth running another pass, since deletions can cascade to newly-unused items).
        /// </summary>
        public bool deleteUnused(int attempt, Document doc)
        {
            List<ElementId> purgableElementIds = new List<ElementId>();
            IList<PerformanceAdviserRuleId> ruleIds = new List<PerformanceAdviserRuleId>();
            string PurgeGuid = "e8c63650-70b7-435a-9010-ec97660c1bda"; // Revit's built-in "unused elements" rule GUID
            foreach (PerformanceAdviserRuleId rule in PerformanceAdviser.GetPerformanceAdviser().GetAllRuleIds())
            {
                if (rule.Guid == Guid.Parse(PurgeGuid))
                {
                    ruleIds.Add(rule);
                    break;
                }
            }

            IList<FailureMessage> failureMessages = PerformanceAdviser.GetPerformanceAdviser().ExecuteRules(doc, ruleIds);

            if (failureMessages.Count > 0)
            {
                foreach (FailureMessage fm in failureMessages)
                {
                    ICollection<ElementId> elList = fm.GetFailingElements();
                    foreach (ElementId elid in elList)
                    {
                        if (!purgableElementIds.Contains(elid))
                        {
                            purgableElementIds.Add(elid);
                        }
                    }
                }
            }

            if (purgableElementIds.Any())
            {
                using (Transaction t = new Transaction(doc, "Delete Unused Items"))
                {
                    t.Start();
                    doc.Delete(purgableElementIds);
                    t.Commit();
                }

                return true;
            }
            else
            {
                return false;
            }
        }
    }

    /// <summary>
    /// Entry point wired as the ribbon's Settings button command ("SettingsForm.LaunchForm"). Stashes
    /// the current <see cref="ExternalCommandData"/>/document on static fields (consumed by the form
    /// and by <see cref="LoadConfig"/>/<see cref="ExecuteWarningSuppression"/>), then shows
    /// <see cref="SettingsForm"/> modally.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchForm : IExternalCommand
    {
        public static System.Windows.Forms.Form thisForm = null;
        public static ExternalCommandData ECD = null;
        public static string mess = null;
        public static ElementSet ElSet = null;
        public static Document docu = null;
        public static UIDocument uidoc = null;
        public static UIApplication uiapp = null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            ECD = commandData;
            mess = message;
            ElSet = elements;
            System.Windows.Forms.Form form = new SettingsForm();
            thisForm = form;
            form.ShowDialog();
            return Result.Succeeded;
        }

        //public static void UpdateTags()
        //{
        //    var command = new ManualUpdateOrigins.ManualUpdate();
        //    command.Execute(ECD, ref mess, ElSet);
        //}

        /// <summary>
        /// For a workshared, non-detached, non-family document: resolves the central model path,
        /// runs <see cref="LoadConfig.Execute(UIApplication, Document)"/> (creates the "GMS config"
        /// folder + Parts Manager/Fastener Scale GlobalParameters if missing) and returns the resolved
        /// path, or null if this document doesn't qualify.
        /// </summary>
        public static string LoadConfig()
        {
            UIApplication uiApp = ECD.Application;
            UIDocument uiDoc = ECD.Application.ActiveUIDocument;
            uidoc = uiDoc;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            LaunchForm.docu = doc;
            string modelPath = null;
            if (doc.IsWorkshared && !doc.IsDetached && !doc.IsFamilyDocument)
            {
                modelPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(doc.GetWorksharingCentralModelPath());
            }
            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                var command = new LoadConfig();
                command.Execute(uiApp, doc);
                return modelPath;
            }
            else
            {
                return null;
            }
        }

        /// <summary>Enables/disables the app-level Revit warning-suppression event handler by
        /// invoking the corresponding register/unregister command.</summary>
        public static void ExecuteWarningSuppression(bool enabled)
        {
            if (enabled)
            {
                var command = new RevitWarningSuppresion.registerWarningSuppresion();
                command.Execute(ECD, ref mess, ElSet);
            }
            else
            {
                var command = new RevitWarningSuppresion.disableregisterWarningSuppresion();
                command.Execute(ECD, ref mess, ElSet);
            }
        }
    }

    /// <summary>
    /// Resolves the current document's central-model path and ensures its per-project "GMS config"
    /// folder and two GlobalParameters ("Parts Manager Location", "Fastener Image Scale") exist,
    /// creating them with default values if not. Run both from <see cref="SettingsForm"/> and as an
    /// application-event handler (document opened/synced/saved, via <see cref="RevitStartup"/>) so the
    /// config self-heals whenever a project touches the model.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LoadConfig : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            LaunchForm.uiapp = uiApp;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);

            return Execute(commandData.Application, doc);
        }

        /// <summary>
        /// Overload usable outside an <see cref="IExternalCommand"/> callback (e.g. from an
        /// application event handler that only has a <see cref="Document"/>, not command data).
        /// For a cloud model, derives the path from the project number under
        /// <see cref="GMSRevitAddin.GmsPaths.ProjectsFolder"/>; for a local central model, converts
        /// the worksharing central model path directly.
        /// </summary>
        public Result Execute(UIApplication uiapp, Document doc)
        {
            LaunchForm.uiapp = uiapp;
            string modelPath = null;
            if (doc.IsWorkshared && !doc.IsDetached && !doc.IsFamilyDocument)
            {
                if (doc.IsModelInCloud)
                {
                    string projectsFolder = GMSRevitAddin.GmsPaths.ProjectsFolder;
                    ProjectInfo projectInfo = doc.ProjectInformation;
                    string projectNumber = projectInfo.Number.Replace("#", "");
                    if (!string.IsNullOrWhiteSpace(projectNumber))
                    {
                        string[] projectDirectories = System.IO.Directory.GetDirectories(projectsFolder, projectNumber + "*", SearchOption.TopDirectoryOnly);
                        if (projectDirectories.Any())
                        {
                            if (projectDirectories.Count() > 1)
                            {
                                TaskDialog td = new TaskDialog("Error");
                                td.MainInstruction = "More than one project directory found beginning with " + projectNumber + ".";
                                td.CommonButtons = TaskDialogCommonButtons.Close;
                                TaskDialogResult tdr = td.Show();

                                modelPath = null;
                            }
                            else
                            {
                                string revitFolder = FindProjectFolder.FindProjectFolder.FindRevitModelFolder(projectDirectories[0]);
                                if (!string.IsNullOrWhiteSpace(revitFolder))
                                {
                                    modelPath = Path.Combine(revitFolder, "blank.rvt");
                                }
                                else
                                {
                                    TaskDialog td = new TaskDialog("Error");
                                    td.MainInstruction = "Could not locate the Revit model folder (a \"Main\" folder containing a \"RevitModel\" subfolder) under " + projectDirectories[0] + ".";
                                    td.CommonButtons = TaskDialogCommonButtons.Close;
                                    TaskDialogResult tdr = td.Show();

                                    modelPath = null;
                                }
                            }
                        }
                        else
                        {
                            TaskDialog td = new TaskDialog("Error");
                            td.MainInstruction = "Could not find project directory beginning with " + projectNumber + ".";
                            td.CommonButtons = TaskDialogCommonButtons.Close;
                            TaskDialogResult tdr = td.Show();

                            modelPath = null;
                        }
                    }
                    else
                    {
                        TaskDialog td = new TaskDialog("Error");
                        td.MainInstruction = "Project Number is empty. Please set the Project Number under Manage > Project Information.";
                        td.CommonButtons = TaskDialogCommonButtons.Close;
                        TaskDialogResult tdr = td.Show();

                        modelPath = null;
                    }
                }
                else
                {
                    modelPath = ModelPathUtils.ConvertModelPathToUserVisiblePath(doc.GetWorksharingCentralModelPath());
                }
            }

            if (!string.IsNullOrWhiteSpace(modelPath))
            {
                string[] parse = modelPath.Split('\\');
                string projectRevitFolder = modelPath.Replace(parse[parse.Length - 1], "");
                string configDirectory = Path.Combine(projectRevitFolder, "GMS config");
                if (projectRevitFolder.IndexOf("Main", StringComparison.OrdinalIgnoreCase) >= 0
                    && projectRevitFolder.IndexOf("RevitModel", StringComparison.OrdinalIgnoreCase) >= 0
                    && Directory.Exists(projectRevitFolder))
                {
                    if (!Directory.Exists(configDirectory))
                    {
                        try
                        {
                            // Hidden so it doesn't clutter the project folder for end users.
                            DirectoryInfo di = Directory.CreateDirectory(configDirectory);
                            di.Attributes = FileAttributes.Directory | FileAttributes.Hidden;
                        }
                        catch
                        {
                            MessageBox.Show("Error creating config directory", "Error");
                        }
                    }

                    //parts manager parameter — create with a placeholder value if the GlobalParameter
                    //doesn't exist yet, otherwise leave an already-assigned value alone
                    using (Transaction tr = new Transaction(doc))
                    {
                        tr.Start("Parts Manager Parameter");

                        if (GlobalParametersManager.IsUniqueName(doc, "Parts Manager Location"))
                        {
                            //GlobalParameter gp = GlobalParameter.Create(doc, "Parts Manager Location", ParameterType.Text);
                            GlobalParameter gp = GlobalParameter.Create(doc, "Parts Manager Location", SpecTypeId.String.Text);

                            if (gp != null)
                            {
                                StringParameterValue spv = gp.GetValue() as StringParameterValue;
                                spv.Value = "Not assigned";
                                gp.SetValue(spv);
                            }
                        }
                        else
                        {
                            try
                            {
                                ElementId partsManagerParamID = GetGlobalParameterByName(doc, "Parts Manager Location");
                                GlobalParameter gp = doc.GetElement(partsManagerParamID) as GlobalParameter;
                                StringParameterValue spv = gp.GetValue() as StringParameterValue;
                                string partsManagerLocation = spv.Value;

                                if (string.IsNullOrWhiteSpace(partsManagerLocation) || spv == null)
                                {
                                    spv.Value = "Not assigned";
                                    gp.SetValue(spv);
                                }
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show(e.ToString());
                            }
                        }
                        tr.Commit();
                    }
                }
                else
                {
                    return Result.Failed;
                }

                //fastener scale parameter — same create-if-missing / heal-if-invalid pattern as above
                using (Transaction tr1 = new Transaction(doc))
                {
                    tr1.Start("Fastener Scale Parameter");

                    if (GlobalParametersManager.IsUniqueName(doc, "Fastener Image Scale"))
                    {
                        GlobalParameter gp1 = GlobalParameter.Create(doc, "Fastener Image Scale", SpecTypeId.Int.Integer);

                        if (gp1 != null)
                        {
                            IntegerParameterValue integerParameterValue = gp1.GetValue() as IntegerParameterValue;
                            integerParameterValue.Value = 10;
                            gp1.SetValue(integerParameterValue);
                        }
                    }
                    else
                    {
                        try
                        {
                            ElementId fastenerScaleParamID = GetGlobalParameterByName(doc, "Fastener Image Scale");
                            GlobalParameter gp1 = doc.GetElement(fastenerScaleParamID) as GlobalParameter;
                            IntegerParameterValue integerParameterValue = gp1.GetValue() as IntegerParameterValue;
                            int fastenerScalePercentage = integerParameterValue.Value;

                            if (fastenerScalePercentage <= 0)
                            {
                                integerParameterValue.Value = 10;
                                gp1.SetValue(integerParameterValue);
                            }
                        }
                        catch (Exception e)
                        {
                            MessageBox.Show(e.ToString());
                        }
                    }
                    tr1.Commit();
                }

                return Result.Succeeded;

            }
            else
            {
                return Result.Failed;
            }
        }

        /// <summary>Null-safe lookup of a GlobalParameter's ElementId by name; returns
        /// <see cref="ElementId.InvalidElementId"/> if GlobalParameters aren't allowed on this doc.</summary>
        public ElementId GetGlobalParameterByName(Document doc, string name)
        {
            if (GlobalParametersManager.AreGlobalParametersAllowed(doc))
            {
                return GlobalParametersManager.FindByName(doc, name);
            }
            return ElementId.InvalidElementId;
        }
    }

    /// <summary>
    /// Registers app-level Revit document events (opened / synced-with-central / saved) in
    /// <see cref="OnStartup"/>, each of which re-runs <see cref="LoadConfig"/> so the GMS config
    /// folder + GlobalParameters self-heal any time the model is touched, without requiring the user
    /// to open the Settings dialog.
    /// </summary>
    public class RevitStartup : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;
            application.ControlledApplication.DocumentSynchronizedWithCentral += ControlledApplication_DocumentSynchronizedWithCentral;
            application.ControlledApplication.DocumentSaved += ControlledApplication_DocumentSaved;
            return Result.Succeeded;

        }

        /// <summary>Re-runs <see cref="LoadConfig"/> whenever a document is opened.</summary>
        public static void ControlledApplication_DocumentOpened(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs e)
        {
            var command = new LoadConfig();
            command.Execute(new UIApplication(sender as Autodesk.Revit.ApplicationServices.Application), e.Document);
        }

        /// <summary>Re-runs <see cref="LoadConfig"/> whenever a document is synced with central.</summary>
        public static void ControlledApplication_DocumentSynchronizedWithCentral(object sender, Autodesk.Revit.DB.Events.DocumentSynchronizedWithCentralEventArgs e)
        {
            var command = new LoadConfig();
            command.Execute(new UIApplication(sender as Autodesk.Revit.ApplicationServices.Application), e.Document);
        }

        /// <summary>Re-runs <see cref="LoadConfig"/> whenever a document is saved.</summary>
        public static void ControlledApplication_DocumentSaved(object sender, Autodesk.Revit.DB.Events.DocumentSavedEventArgs e)
        {
            var command = new LoadConfig();
            command.Execute(new UIApplication(sender as Autodesk.Revit.ApplicationServices.Application), e.Document);
        }
    }
}
