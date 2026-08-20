using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Visual;
using System.Windows.Forms;
using System.IO;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace ShowTags
{
    // This class implements the ICentralLockedCallback to override Revit's default
    // behavior for how to handle when the central model is locked by another user
    class SynchLockCallback : ICentralLockedCallback
    {
        // If unable to lock central, give up rather than waiting
        public bool ShouldWaitForLockAvailability()
        {
            return false;
        }

    }

    /// <summary>
    /// Whole-model "Show Tags" command. Presents the sheet-set (Grouping - Usage) picker, then for
    /// every elevation/floor-plan view under the chosen sheet set(s), swaps the view template to the
    /// "Tag - Elevations"/"Tag - Floor Plans" template and unhides "GAIT - Piece Tag" family instances
    /// (optionally hiding excluded tag types instead). Scope: the entire model's elevation/floor plan
    /// views grouped by sheet set — contrast with <see cref="ShowTagsPerView.ShowTags"/>, which acts
    /// only on the active sheet/view. Syncs with central before and after processing. Wired in
    /// GMS_tools.cs as "ShowTags.ShowTags".
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowTags : IExternalCommand
    {
        public static ProgressForm.ProgressForm currentPForm = null;
        public static bool error = false;
        public static Dictionary<string, List<string>> templateList = new Dictionary<string, List<string>>();
        public static List<string> chosenSheetSets = new List<string>();
        public static Dictionary<string, List<ElementId>> elevationListBySheetSet = new Dictionary<string, List<ElementId>>();
        public static Dictionary<string, List<ElementId>> floorplanListBySheetSet = new Dictionary<string, List<ElementId>>();
        public static List<ElementId> viewList = new List<ElementId>();
        public static List<string> sheetSets = new List<string>();
        public static bool collectElevations = true;
        public static bool collectFloorplans = false;
        public static bool PieceTags = true;
        public static bool GasketTags = true;
        public static bool GlazingTags = true;
        public static bool CustomerTags = true;
        public static bool SubUnitTags = true;

        /// <summary>
        /// Entry point: collects sheet-set groupings from all sheets, lets the user pick which
        /// sheet set(s) to process via <c>ShowHideSheetSets</c>, checks out and validates the affected
        /// views/tags, swaps view templates, and un-hides/hides tag instances accordingly.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);

            templateList.Clear();
            elevationListBySheetSet.Clear();
            floorplanListBySheetSet.Clear();
            viewList.Clear();
            chosenSheetSets.Clear();
            error = false;

            //gather Sheet Set Types for checkedlistbox and retain elevation views by Grouping-Usage
            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector = collector.OfCategory(BuiltInCategory.OST_Sheets).WhereElementIsNotElementType().OfClass(typeof(ViewSheet));

            var sheets = collector.ToElements();

            foreach (Element elem in sheets)
            {
                ParameterSet parameters = elem.Parameters;

                // "Grouping - Usage" is a sheet parameter; only sheets that carry it participate in
                // sheet-set grouping. Each sheet's viewports are bucketed by that usage value so the
                // picker can offer sheet sets instead of individual sheets.
                foreach (Parameter param in parameters)
                {
                    if (param.Definition.Name == "Grouping - Usage")
                    {
                        ViewSheet vs = (ViewSheet)elem;
                        ICollection<ElementId> listViews = vs.GetAllViewports();
                        foreach (ElementId eid in listViews)
                        {
                            Viewport vp = doc.GetElement(eid) as Viewport;
                            ElementId viewID = vp.ViewId;
                            Autodesk.Revit.DB.View v = doc.GetElement(viewID) as Autodesk.Revit.DB.View;
                            string usage = param.AsString();
                            if (string.IsNullOrWhiteSpace(usage))
                            {
                                usage = "???";
                            }
                            // Elevations and callout details are tracked separately from floor plans
                            // because they get different tag view templates further down.
                            if (v.ViewType == ViewType.Elevation || (v.ViewType == ViewType.Detail && v.IsCallout == true))
                            {
                                if(!sheetSets.Contains(usage))
                                {
                                    sheetSets.Add(usage);
                                }
                                if (!elevationListBySheetSet.ContainsKey(usage))
                                {
                                    List<ElementId> tempViewList = new List<ElementId>();
                                    tempViewList.Add(viewID);
                                    elevationListBySheetSet.Add(usage, tempViewList);
                                }
                                else
                                {
                                    List<ElementId> tempViewList = new List<ElementId>();
                                    tempViewList = elevationListBySheetSet[usage];
                                    tempViewList.Add(viewID);
                                    elevationListBySheetSet[usage] = tempViewList;
                                }
                            }
                            if (v.ViewType == ViewType.FloorPlan || (v.ViewType == ViewType.Detail && v.IsCallout == false))
                            {
                                if (!sheetSets.Contains(usage))
                                {
                                    sheetSets.Add(usage);
                                }
                                if (!floorplanListBySheetSet.ContainsKey(usage))
                                {
                                    List<ElementId> tempViewList = new List<ElementId>();
                                    tempViewList.Add(viewID);
                                    floorplanListBySheetSet.Add(usage, tempViewList);
                                }
                                else
                                {
                                    List<ElementId> tempViewList = new List<ElementId>();
                                    tempViewList = floorplanListBySheetSet[usage];
                                    tempViewList.Add(viewID);
                                    floorplanListBySheetSet[usage] = tempViewList;
                                }
                            }
                        }
                        break;
                    }
                }
            }

            using (System.Windows.Forms.Form form = new ShowHideSheetSets.ShowHideSheetSets(sheetSets, true))
            {
                var formResult = form.ShowDialog();
                if (formResult == DialogResult.OK)
                {
                    if (chosenSheetSets.Any())
                    {
                        if (doc.IsWorkshared && !doc.IsDetached && !doc.IsFamilyDocument)
                        {
                            //sync with central
                            // Set options for accessing central model
                            TransactWithCentralOptions transOpts = new TransactWithCentralOptions();
                            SynchLockCallback transCallBack = new SynchLockCallback();
                            // Override default behavior of waiting to try again if the central model is locked -
                            // fail fast instead of blocking the command on a locked central.
                            transOpts.SetLockCallback(transCallBack);

                            // Set options for synchronizing with central
                            SynchronizeWithCentralOptions syncOpts = new SynchronizeWithCentralOptions();
                            // Sync without relinquishing any checked out elements or worksets
                            RelinquishOptions relinquishOpts = new RelinquishOptions(false);
                            syncOpts.SetRelinquishOptions(relinquishOpts);
                            // Automatically save local model after sync
                            syncOpts.SaveLocalAfter = true;

                            try
                            {
                                doc.SynchronizeWithCentral(transOpts, syncOpts);
                            }
                            catch (Exception e)
                            {
                                TaskDialog.Show("ST1 Error", "Synchronize Failed: " + e.Message);
                            }

                            List<string> errorOnView = new List<string>();
                            errorOnView.Clear();
                            foreach (string st in chosenSheetSets)
                            {
                                if(collectElevations && elevationListBySheetSet.ContainsKey(st))
                                {
                                    viewList.AddRange(elevationListBySheetSet[st]);
                                }
                                if(collectFloorplans && floorplanListBySheetSet.ContainsKey(st))
                                {
                                    viewList.AddRange(floorplanListBySheetSet[st]);
                                }
                                foreach (ElementId view in viewList)
                                {
                                    Autodesk.Revit.DB.View vv = doc.GetElement(view) as Autodesk.Revit.DB.View;
                                    // Views with no view template can't be safely re-templated for tag
                                    // display later, so they're flagged here and processing is blocked
                                    // until the user assigns one.
                                    if (vv.ViewTemplateId == ElementId.InvalidElementId)
                                    {
                                        ParameterSet ps = vv.Parameters;
                                        string detailNumber = "";
                                        string sheet = "";
                                        foreach (Parameter p in ps)
                                        {
                                            if (p.Definition.Name == "Detail Number")
                                            {
                                                detailNumber = p.AsString();
                                            }
                                            if (p.Definition.Name == "Sheet Number")
                                            {
                                                sheet = p.AsString();
                                            }
                                        }
                                        errorOnView.Add(detailNumber + " / " + sheet);
                                    }
                                }
                            }
                            if (errorOnView.Any())
                            {
                                string viewsMsg = "";
                                foreach (string s in errorOnView)
                                {
                                    viewsMsg += s + System.Environment.NewLine;
                                }
                                MessageBox.Show("The following view(s) have the View Template set to <None>. Set the view template to the appropriate selection before using this command." + System.Environment.NewLine + viewsMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                            else
                            {


                                string targetName = "GAIT - Piece Tag";

                                //perform check for uneditable elements
                                Dictionary<string, List<ElementId>> tagsPerView = new Dictionary<string, List<ElementId>>();
                                Dictionary<string, List<ElementId>> tagsPerViewHide = new Dictionary<string, List<ElementId>>();
                                bool foundNotEditible = false;

                                int progressCount = viewList.Count;
                                string currentSt = "Precheck and data gathering from view {0} of " + progressCount.ToString() + "...";
                                string finalSt = "Precheck complete. Preparing to begin processing...";
                                string capText = "Precheck";
                                string user = "";

                                List<string> excludedTypes = new List<string>();
                                if (!PieceTags)
                                {
                                    excludedTypes.Add("Piece");
                                }
                                if(!GasketTags)
                                {
                                    excludedTypes.Add("Gasket");
                                }
                                //if(!GlazingTags)
                                //{
                                //    excludedTypes.Add("Glazing");
                                //}
                                //if(!CustomerTags)
                                //{
                                //    excludedTypes.Add("Customer");
                                //}
                                //if(!SubUnitTags)
                                //{
                                //    excludedTypes.Add("SubUnit");
                                //}

                                using (ProgressForm.ProgressForm pf1 = new ProgressForm.ProgressForm(capText, currentSt, progressCount, finalSt))
                                {
                                    currentPForm = pf1;

                                    foreach (ElementId eid in viewList)
                                    {
                                        pf1.Visible = true;
                                        pf1.Increment();

                                        ElementOwnerViewFilter eovf = new ElementOwnerViewFilter(eid);
                                        ICollection<Element> elementsList = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericAnnotation).OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType().WherePasses(eovf).ToElements();
                                        List<ElementId> tagIDs = new List<ElementId>();
                                        List<ElementId> hideTagIDs = new List<ElementId>();

                                        foreach (Element el in elementsList)
                                        {
                                            FamilyInstance fi = el as FamilyInstance;
                                            FamilySymbol fs = fi.Symbol;
                                            Family fam = fs.Family;
                                            string type = el.get_Parameter(BuiltInParameter.ELEM_TYPE_PARAM).AsValueString();
                                            bool includedType = true;
                                            if (excludedTypes.Contains(type))
                                            {
                                                includedType = false;
                                            }
                                            if (fam.Name == targetName && includedType)
                                            {
                                                tagIDs.Add(el.Id);
                                            }
                                            else if(fam.Name == targetName && !includedType)
                                            {
                                                hideTagIDs.Add(el.Id);
                                            }
                                        }

                                        ICollection<ElementId> totalList = tagIDs;
                                        totalList.Add(eid);
                                        ICollection<ElementId> OKtoEdit = WorksharingUtils.CheckoutElements(doc, totalList);
                                        List<ElementId> listForDictionary = new List<ElementId>();

                                        foreach (ElementId elid in totalList)
                                        {
                                            if (!OKtoEdit.Contains(elid))
                                            {
                                                foundNotEditible = true;
                                                WorksharingUtils.GetCheckoutStatus(doc, elid, out user);
                                                break;
                                            }
                                            else if (elid != eid)
                                            {
                                                listForDictionary.Add(elid);
                                            }
                                        }
                                        if (foundNotEditible)
                                        {
                                            break;
                                        }
                                        else
                                        {
                                            tagsPerView.Add(eid.ToString(), listForDictionary);
                                        }

                                        ICollection<ElementId> totalHideList = hideTagIDs;
                                        ICollection<ElementId> OKtoEditHide = WorksharingUtils.CheckoutElements(doc, totalHideList);
                                        List<ElementId> listForHideDictionary = new List<ElementId>();

                                        foreach (ElementId elid in totalHideList)
                                        {
                                            if (!OKtoEditHide.Contains(elid))
                                            {
                                                foundNotEditible = true;
                                                WorksharingUtils.GetCheckoutStatus(doc, elid, out user);
                                                break;
                                            }
                                            else if (elid != eid)
                                            {
                                                listForHideDictionary.Add(elid);
                                            }
                                        }
                                        if (foundNotEditible)
                                        {
                                            break;
                                        }
                                        else
                                        {
                                            tagsPerViewHide.Add(eid.ToString(), listForHideDictionary);
                                        }

                                    }
                                }
                                //string mess = string.Empty;
                                //foreach(string s in types)
                                //{
                                //    mess += s + System.Environment.NewLine;
                                //}
                                //mess += types.Count.ToString();
                                //MessageBox.Show(mess);

                                if (foundNotEditible)
                                {
                                    if (string.IsNullOrWhiteSpace(user))
                                    {
                                        MessageBox.Show("Elements were found that are checked out from the central model by other users. The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "ST1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Elements were found that are checked out from the central model by " + user + ". The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "ST1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                }
                                else
                                {
                                    //find Tag - Elevations view template
                                    IEnumerable<Autodesk.Revit.DB.View> views = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.View)).Cast<Autodesk.Revit.DB.View>().Where(v => v.IsTemplate);
                                    ElementId viewtempID = ElementId.InvalidElementId;
                                    ElementId FPviewtempID = ElementId.InvalidElementId;
                                    foreach (Autodesk.Revit.DB.View vw in views)
                                    {
                                        if (viewtempID != ElementId.InvalidElementId && FPviewtempID != ElementId.InvalidElementId)
                                        {
                                            break;
                                        }
                                        else if (vw.Name == "Tag - Elevations")
                                        {
                                            viewtempID = vw.Id;
                                        }
                                        else if (vw.Name == "Tag - Floor Plans")
                                        {
                                            FPviewtempID = vw.Id;
                                        }
                                    }

                                    bool continueShowTags = true;
                                    if (viewtempID == ElementId.InvalidElementId)
                                    {
                                        TaskDialog dia = new TaskDialog("Warning!");
                                        dia.MainInstruction = "Warning! Can not find view template \"Tag - Elevations\". Continue to show tags without changing view template or cancel.";
                                        dia.CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;
                                        TaskDialogResult result = dia.Show();
                                        if (result == TaskDialogResult.Cancel)
                                        {
                                            continueShowTags = false;
                                        }
                                        else if (result == TaskDialogResult.Ok)
                                        {
                                            continueShowTags = true;
                                        }
                                    }

                                    if (FPviewtempID == ElementId.InvalidElementId)
                                    {
                                        TaskDialog dia = new TaskDialog("Warning!");
                                        dia.MainInstruction = "Warning! Can not find view template \"Tag - Floor Plans\". Continue to show tags without changing view template or cancel.";
                                        dia.CommonButtons = TaskDialogCommonButtons.Ok | TaskDialogCommonButtons.Cancel;
                                        TaskDialogResult result = dia.Show();
                                        if (result == TaskDialogResult.Cancel)
                                        {
                                            continueShowTags = false;
                                        }
                                        else if (result == TaskDialogResult.Ok)
                                        {
                                            continueShowTags = true;
                                        }
                                    }


                                    //begin processing
                                    if (continueShowTags)
                                    {
                                        int eViewCount = tagsPerView.Keys.Count;
                                        string s = "Processing elevation {0} of " + eViewCount.ToString() + "...";
                                        string f = "Processing complete. Applying changes and regenerating model...";
                                        string captionText = "";

                                        captionText = "Show Tags";

                                        using (ProgressForm.ProgressForm pf = new ProgressForm.ProgressForm(captionText, s, eViewCount, f))
                                        {
                                            currentPForm = pf;

                                            using (Transaction tr = new Transaction(doc))
                                            {
                                                tr.Start("Show Tags");

                                                try
                                                {
                                                    foreach (KeyValuePair<string, List<ElementId>> kvp in tagsPerView)
                                                    {
                                                        pf.Visible = true;
                                                        pf.Increment();

                                                        long elementIDFromString = Convert.ToInt64(kvp.Key);

                                                        Autodesk.Revit.DB.View v = doc.GetElement(new ElementId(elementIDFromString)) as Autodesk.Revit.DB.View;

                                                        if (!v.IsTemplate)
                                                        {
                                                            List<string> tempData = new List<string>();
                                                            if (templateList.ContainsKey(kvp.Key))
                                                            {
                                                                tempData = templateList[kvp.Key];
                                                                if (v.ViewTemplateId == ElementId.InvalidElementId)
                                                                {
                                                                    tempData.Add(v.Name + "~" + v.Id.ToString() + "~" + "none" + "~" + v.ViewTemplateId.ToString());
                                                                }
                                                                else
                                                                {
                                                                    tempData.Add(v.Name + "~" + v.Id.ToString() + "~" + doc.GetElement(v.ViewTemplateId).Name + "~" + v.ViewTemplateId.ToString());
                                                                }
                                                                templateList[kvp.Key] = tempData;
                                                            }
                                                            else if (!doc.GetElement(v.ViewTemplateId).Name.Contains("Tag"))
                                                            {
                                                                if (v.ViewTemplateId == ElementId.InvalidElementId)
                                                                {
                                                                    tempData.Add(v.Name + "~" + v.Id.ToString() + "~" + "none" + "~" + v.ViewTemplateId.ToString());
                                                                }
                                                                else
                                                                {
                                                                    tempData.Add(v.Name + "~" + v.Id.ToString() + "~" + doc.GetElement(v.ViewTemplateId).Name + "~" + v.ViewTemplateId.ToString());
                                                                }
                                                                templateList.Add(kvp.Key, tempData);
                                                            }
                                                        }

                                                        if (viewtempID != ElementId.InvalidElementId && v.ViewTemplateId != viewtempID && ((v.ViewType == ViewType.Detail && v.IsCallout == true) || v.ViewType == ViewType.Elevation))
                                                        {
                                                            v.ViewTemplateId = viewtempID;
                                                        }
                                                        else if (viewtempID != ElementId.InvalidElementId && v.ViewTemplateId != FPviewtempID && (v.ViewType == ViewType.FloorPlan || (v.ViewType == ViewType.Detail && v.IsCallout == false)))
                                                        {
                                                            v.ViewTemplateId = FPviewtempID;
                                                        }

                                                        List<ElementId> tagListFromView = new List<ElementId>();
                                                        tagListFromView = kvp.Value;
                                                        List<ElementId> hideTagListFromView = new List<ElementId>();
                                                        hideTagListFromView = tagsPerViewHide[kvp.Key];

                                                        if (tagListFromView.Any())
                                                        {
                                                            v.UnhideElements(tagListFromView);
                                                        }
                                                        if(hideTagListFromView.Any())
                                                        {
                                                            v.HideElements(hideTagListFromView);
                                                        }
                                                    }
                                                }
                                                catch (Exception e)
                                                {
                                                    pf.Visible = false;
                                                    error = true;
                                                    MessageBox.Show(e.ToString(), "ST2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                }
                                                tr.Commit();

                                                string modelPath = null;

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

                                                string[] parsePath = modelPath.Split('\\');
                                                string projectRevitFolder = modelPath.Replace(parsePath[parsePath.Length - 1], "");
                                                string configDirectory = Path.Combine(projectRevitFolder, "GMS config");
                                                string viewTemplatesDirectory = Path.Combine(configDirectory, "ViewTemplateData");

                                                if (viewTemplatesDirectory != null && !Directory.Exists(viewTemplatesDirectory))
                                                {
                                                    try
                                                    {
                                                        Directory.CreateDirectory(viewTemplatesDirectory);
                                                    }
                                                    catch
                                                    {
                                                        MessageBox.Show("Unable to create ViewTemplateData folder.", "ST6 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                    }
                                                }


                                                if (!string.IsNullOrWhiteSpace(modelPath))
                                                {
                                                    foreach(KeyValuePair<string, List<string>> kvp in templateList)
                                                    {
                                                        List<string> parseList = new List<string>();
                                                        parseList = kvp.Value;
                                                        foreach (string str in parseList)
                                                        {
                                                            try
                                                            {
                                                                string[] parse = str.Split('~');
                                                                if (!string.IsNullOrWhiteSpace(parse[1]) && !string.IsNullOrWhiteSpace(parse[3]))
                                                                {
                                                                    string viewIDfromParse = parse[1];
                                                                    string viewTemplateIDfromParse = parse[3];

                                                                    string VTFile = Path.Combine(viewTemplatesDirectory, viewIDfromParse);

                                                                    if (File.Exists(VTFile))
                                                                    {
                                                                        File.Delete(VTFile);
                                                                    }

                                                                    StreamWriter sw = null;
                                                                    using (sw = File.AppendText(VTFile))
                                                                    {
                                                                        sw.WriteLine(viewTemplateIDfromParse);
                                                                    }
                                                                    sw.Close();
                                                                }
                                                            }
                                                            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("ShowTags", __ex); }
                                                        }
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }

            try
            {
                RelinquishOptions relop = new RelinquishOptions(true);
                TransactWithCentralOptions twc = new TransactWithCentralOptions();
                WorksharingUtils.RelinquishOwnership(doc, relop, twc);
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("ShowTags", __ex); }

            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class HideTags : IExternalCommand
    {
        public static ProgressForm.ProgressForm currentPForm = null;
        public static List<string> chosenSheetSets = new List<string>();
        public static bool error = false;
        public static Dictionary<string, List<ElementId>> elevationListBySheetSet = new Dictionary<string, List<ElementId>>();
        public static Dictionary<string, List<ElementId>> floorplanListBySheetSet = new Dictionary<string, List<ElementId>>();
        public static List<ElementId> viewList = new List<ElementId>();
        public static List<string> sheetSets = new List<string>();
        public static bool collectElevations = true;
        public static bool collectFloorplans = false;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);

            elevationListBySheetSet.Clear();
            floorplanListBySheetSet.Clear();
            viewList.Clear();
            sheetSets.Clear();
            chosenSheetSets.Clear();
            error = false;

            //gather Sheet Set Types for checkedlistbox and retain elevation views by SST
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
                        ViewSheet vs = (ViewSheet)elem;
                        ICollection<ElementId> listViews = vs.GetAllViewports();
                        foreach (ElementId eid in listViews)
                        {
                            Viewport vp = doc.GetElement(eid) as Viewport;
                            ElementId viewID = vp.ViewId;
                            Autodesk.Revit.DB.View v = doc.GetElement(viewID) as Autodesk.Revit.DB.View;
                            string usage = param.AsString();
                            if (string.IsNullOrWhiteSpace(usage))
                            {
                                usage = "???";
                            }
                            if(v.ViewType == ViewType.Elevation || (v.ViewType == ViewType.Detail && v.IsCallout == true))
                            {
                                if(!sheetSets.Contains(usage))
                                {
                                    sheetSets.Add(usage);
                                }
                                if (!elevationListBySheetSet.ContainsKey(usage))
                                {
                                    List<ElementId> tempViewList = new List<ElementId>();
                                    tempViewList.Add(viewID);
                                    elevationListBySheetSet.Add(usage, tempViewList);
                                }
                                else
                                {
                                    List<ElementId> tempViewList = new List<ElementId>();
                                    tempViewList = elevationListBySheetSet[usage];
                                    tempViewList.Add(viewID);
                                    elevationListBySheetSet[usage] = tempViewList;
                                }
                            }
                            if (v.ViewType == ViewType.FloorPlan || (v.ViewType == ViewType.Detail && v.IsCallout == false))
                            {
                                if (!sheetSets.Contains(usage))
                                {
                                    sheetSets.Add(usage);
                                }
                                if (!floorplanListBySheetSet.ContainsKey(usage))
                                {
                                    List<ElementId> tempViewList = new List<ElementId>();
                                    tempViewList.Add(viewID);
                                    floorplanListBySheetSet.Add(usage, tempViewList);
                                }
                                else
                                {
                                    List<ElementId> tempViewList = new List<ElementId>();
                                    tempViewList = floorplanListBySheetSet[usage];
                                    tempViewList.Add(viewID);
                                    floorplanListBySheetSet[usage] = tempViewList;
                                }
                            }
                        }
                        break;
                    }
                }
            }

            using (System.Windows.Forms.Form form = new ShowHideSheetSets.ShowHideSheetSets(sheetSets, false))
            {
                var formResult = form.ShowDialog();
                if (formResult == DialogResult.OK)
                {
                    if (chosenSheetSets.Any())
                    {
                        if (doc.IsWorkshared && !doc.IsDetached && !doc.IsFamilyDocument)
                        {
                            string targetName = "GAIT - Piece Tag";

                            //perform check for uneditable elements
                            Dictionary<string, List<ElementId>> tagsPerView = new Dictionary<string, List<ElementId>>();
                            bool foundNotEditible = false;

                            foreach (string st in chosenSheetSets)
                            {
                                if (collectElevations && elevationListBySheetSet.ContainsKey(st))
                                {
                                    viewList.AddRange(elevationListBySheetSet[st]);
                                }
                                if (collectFloorplans && floorplanListBySheetSet.ContainsKey(st))
                                {
                                    viewList.AddRange(floorplanListBySheetSet[st]);
                                }
                            }
                            int progressCount = viewList.Count;
                            string currentSt = "Precheck and data gathering from view {0} of " + progressCount.ToString() + "...";
                            string finalSt = "Precheck complete. Preparing to begin processing...";
                            string capText = "Precheck";
                            string user = "";

                            using (ProgressForm.ProgressForm pf1 = new ProgressForm.ProgressForm(capText, currentSt, progressCount, finalSt))
                            {
                                currentPForm = pf1;

                                foreach (ElementId eid in viewList)
                                {
                                    pf1.Visible = true;
                                    pf1.Increment();

                                    ElementOwnerViewFilter eovf = new ElementOwnerViewFilter(eid);
                                    ICollection<Element> elementsList = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_GenericAnnotation).OfClass(typeof(FamilyInstance)).WhereElementIsNotElementType().WherePasses(eovf).ToElements();
                                    List<ElementId> tagIDs = new List<ElementId>();
                                    foreach (Element el in elementsList)
                                    {
                                        FamilyInstance fi = el as FamilyInstance;
                                        FamilySymbol fs = fi.Symbol;
                                        Family fam = fs.Family;
                                        if (fam.Name == targetName)
                                        {
                                            tagIDs.Add(el.Id);
                                        }
                                    }

                                    ICollection<ElementId> totalList = tagIDs;
                                    totalList.Add(eid);
                                    ICollection<ElementId> OKtoEdit = WorksharingUtils.CheckoutElements(doc, totalList);
                                    List<ElementId> listForDictionary = new List<ElementId>();

                                    foreach (ElementId elid in totalList)
                                    {
                                        if (!OKtoEdit.Contains(elid))
                                        {
                                            foundNotEditible = true;
                                            WorksharingUtils.GetCheckoutStatus(doc, elid, out user);
                                            break;
                                        }
                                        else if (elid != eid)
                                        {
                                            listForDictionary.Add(elid);
                                        }
                                    }
                                    if (foundNotEditible)
                                    {
                                        break;
                                    }
                                    else
                                    {
                                        tagsPerView.Add(eid.ToString(), listForDictionary);
                                    }
                                    if (foundNotEditible)
                                    {
                                        break;
                                    }
                                }
                            }

                            if (foundNotEditible)
                            {
                                if (string.IsNullOrWhiteSpace(user))
                                {
                                    MessageBox.Show("Elements were found that are checked out from the central model by other users. The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "ST1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                                else
                                {
                                    MessageBox.Show("Elements were found that are checked out from the central model by " + user + ". The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "ST1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                            else
                            {
                                //begin processing
                                Dictionary<ElementId, ElementId> origTemplates = new Dictionary<ElementId, ElementId>();

                                string modelPath = null;
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

                                string[] parsePath = modelPath.Split('\\');
                                string projectRevitFolder = modelPath.Replace(parsePath[parsePath.Length - 1], "");
                                string configDirectory = Path.Combine(projectRevitFolder, "GMS config");
                                string viewTemplatesDirectory = Path.Combine(configDirectory, "ViewTemplateData");

                                if (viewTemplatesDirectory != null && !Directory.Exists(viewTemplatesDirectory))
                                {
                                    try
                                    {
                                        Directory.CreateDirectory(viewTemplatesDirectory);
                                    }
                                    catch
                                    {
                                        MessageBox.Show("Unable to create ViewTemplateData folder.", "ST6 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                }

                                if (!string.IsNullOrWhiteSpace(modelPath))
                                {
                                    foreach (ElementId vwID in viewList)
                                    {
                                        string VTFile = Path.Combine(viewTemplatesDirectory, vwID.ToString());

                                        if (File.Exists(VTFile))
                                        {
                                            var fileStream = new FileStream(VTFile, FileMode.Open, FileAccess.Read);
                                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                            {
                                                string line;
                                                while ((line = streamReader.ReadLine()) != null)
                                                {
                                                    if (!string.IsNullOrWhiteSpace(line))
                                                    {
                                                        long templateIDFromString = Convert.ToInt64(line);
                                                        origTemplates.Add(vwID, new ElementId(templateIDFromString));
                                                    }
                                                }
                                                streamReader.Close();
                                            }
                                        }
                                    }

                                    int eViewCount = tagsPerView.Keys.Count;
                                    string s = "Processing elevation {0} of " + eViewCount.ToString() + "...";
                                    string f = "Processing complete. Applying changes and regenerating model...";
                                    string captionText = "";

                                    captionText = "Hide Tags";

                                    using (ProgressForm.ProgressForm pf = new ProgressForm.ProgressForm(captionText, s, eViewCount, f))
                                    {
                                        currentPForm = pf;

                                        using (Transaction tr = new Transaction(doc))
                                        {
                                            tr.Start("Hide Tags");

                                            try
                                            {
                                                foreach (KeyValuePair<string, List<ElementId>> kvp in tagsPerView)
                                                {
                                                    pf.Visible = true;
                                                    pf.Increment();

                                                    long elementIDFromString = Convert.ToInt64(kvp.Key);

                                                    Autodesk.Revit.DB.View v = doc.GetElement(new ElementId(elementIDFromString)) as Autodesk.Revit.DB.View;

                                                    if (origTemplates.ContainsKey(v.Id) && v.ViewTemplateId != origTemplates[v.Id])
                                                    {
                                                        v.ViewTemplateId = origTemplates[v.Id];
                                                    }

                                                    List<ElementId> tagListFromView = new List<ElementId>();
                                                    tagListFromView = kvp.Value;

                                                    if (tagListFromView.Any())
                                                    {
                                                        v.HideElements(tagListFromView);
                                                    }
                                                }
                                            }
                                            catch (Exception e)
                                            {
                                                pf.Visible = false;
                                                error = true;
                                                MessageBox.Show(e.ToString(), "ST4 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            }
                                            tr.Commit();
                                        }
                                    }

                                    if (!error)
                                    {
                                        //sync with central
                                        // Set options for accessing central model
                                        TransactWithCentralOptions transOpts = new TransactWithCentralOptions();
                                        SynchLockCallback transCallBack = new SynchLockCallback();
                                        // Override default behavior of waiting to try again if the central model is locked
                                        transOpts.SetLockCallback(transCallBack);

                                        // Set options for synchronizing with central
                                        SynchronizeWithCentralOptions syncOpts = new SynchronizeWithCentralOptions();
                                        // Sync with relinquishing any checked out elements or worksets
                                        RelinquishOptions relinquishOpts = new RelinquishOptions(true);
                                        syncOpts.SetRelinquishOptions(relinquishOpts);
                                        // Automatically save local model after sync
                                        syncOpts.SaveLocalAfter = true;

                                        try
                                        {
                                            doc.SynchronizeWithCentral(transOpts, syncOpts);
                                        }
                                        catch (Exception e)
                                        {
                                            TaskDialog.Show("ST5 Error", "Synchronize Failed: " + e.Message);
                                        }
                                    }
                                }
                            }
                        }
                    }
                }
            }
            return Result.Succeeded;
        }
    }
}