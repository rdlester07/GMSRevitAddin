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

namespace ShowTagsPerView
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
    /// Per-view/per-sheet variant of the "Show Tags" command (the sibling <c>ShowTags.cs</c> file's
    /// <c>ShowTags</c> class in the <c>ShowTags</c> namespace operates on the whole model instead —
    /// the class-name collision across the two namespaces is intentional per this codebase's naming
    /// convention). Scoped to the active sheet (or the active view, if the active view isn't a sheet):
    /// checks out every "GAIT - Piece Tag" annotation in the elevation/detail/floor-plan viewports
    /// placed on that sheet, then swaps each view's template to "Tag - Elevations" or
    /// "Tag - Floor Plans" (recording the prior template id to a per-view file so
    /// <see cref="HideTags"/> can restore it later) and un-hides the tags.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ShowTags : IExternalCommand
    {
        public static Dictionary<ElementId, List<ElementId>> idDictionary = new Dictionary<ElementId, List<ElementId>>();
        public static bool foundNotEditible = false;
        public static string user = "";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            bool passed = false;

            foundNotEditible = false;
            user = "";

            // Only meaningful for a workshared central model (checkout/relinquish semantics apply).
            if (doc.IsWorkshared && !doc.IsDetached && !doc.IsFamilyDocument)
            {
                Autodesk.Revit.DB.View activeView = uiApp.ActiveUIDocument.ActiveView;

                //find Tag - Elevations view template
                IEnumerable<Autodesk.Revit.DB.View> viewTemps = new FilteredElementCollector(doc).OfClass(typeof(Autodesk.Revit.DB.View)).Cast<Autodesk.Revit.DB.View>().Where(v => v.IsTemplate);
                ElementId viewtempID = ElementId.InvalidElementId;
                ElementId FPviewtempID = ElementId.InvalidElementId;
                foreach (Autodesk.Revit.DB.View vw in viewTemps)
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

                if (viewtempID == ElementId.InvalidElementId)
                {
                    MessageBox.Show("Unable to locate Tag view template.", "STPV Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else if (FPviewtempID == ElementId.InvalidElementId)
                {
                    MessageBox.Show("Unable to locate Floor Plan Tag view template.", "STPV Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    if (activeView != null)
                    {
                        idDictionary.Clear();
                        try
                        {
                            // Primary path: active view is a sheet — collect eligible viewports on it.
                            // If the cast/collection fails (active view isn't a sheet), the outer catch
                            // below falls back to treating the active view itself as the single target.
                            ViewSheet vs = doc.GetElement(activeView.Id) as ViewSheet;
                            ICollection<ElementId> views = vs.GetAllViewports();
                            List<ElementId> listViews = new List<ElementId>();
                            List<ElementId> noVT = new List<ElementId>();   // views with template = <None>, can't be tag-templated safely

                            if (views.Any())
                            {
                                foreach (ElementId elmid in views)
                                {
                                    Viewport vp = doc.GetElement(elmid) as Viewport;
                                    ElementId viewID = vp.ViewId;
                                    Autodesk.Revit.DB.View view = doc.GetElement(viewID) as Autodesk.Revit.DB.View;

                                    if (view.ViewType == ViewType.Elevation || view.ViewType == ViewType.Detail || view.ViewType == ViewType.FloorPlan)
                                    {
                                        listViews.Add(viewID);
                                        if (view.ViewTemplateId == ElementId.InvalidElementId)
                                        {
                                            noVT.Add(view.Id);
                                        }
                                    }
                                }

                                if (noVT.Any())
                                {
                                    string viewsMsg = "";
                                    foreach (ElementId elementId in noVT)
                                    {
                                        Autodesk.Revit.DB.View view = doc.GetElement(elementId) as Autodesk.Revit.DB.View;
                                        ParameterSet pset = view.Parameters;
                                        string detailNumber = "";
                                        string sheet = "";
                                        foreach (Parameter p in pset)
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
                                        viewsMsg += detailNumber + " / " + sheet + System.Environment.NewLine;
                                    }
                                    MessageBox.Show("The following view(s) have the View Template set to <None>. Set the view template to the appropriate selection before using this command." + System.Environment.NewLine + viewsMsg, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                                else
                                {
                                    passed = collectTags(listViews, doc);

                                    if (foundNotEditible)
                                    {
                                        if (string.IsNullOrWhiteSpace(user))
                                        {
                                            MessageBox.Show("Elements were found that are checked out from the central model by other users. The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                        else
                                        {
                                            MessageBox.Show("Elements were found that are checked out from the central model by " + user + ". The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                    }
                                    else if (!passed)
                                    {
                                        List<ElementId> listSingleView = new List<ElementId>();
                                        listSingleView.Add(doc.ActiveView.Id);
                                        passed = collectTags(listSingleView, doc);

                                        if (foundNotEditible)
                                        {
                                            if (string.IsNullOrWhiteSpace(user))
                                            {
                                                MessageBox.Show("Elements were found that are checked out from the central model by other users. The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            }
                                            else
                                            {
                                                MessageBox.Show("Elements were found that are checked out from the central model by " + user + ". The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            }
                                        }
                                        else if (!passed)
                                        {
                                            MessageBox.Show("Error obtaining elements from active view.", "STPV3 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                        else
                                        {
                                            showTags(doc, viewtempID, FPviewtempID);
                                        }
                                    }
                                    else
                                    {
                                        showTags(doc, viewtempID, FPviewtempID);
                                    }
                                }
                            }
                            else
                            {
                                try
                                {
                                    List<ElementId> listSingleView = new List<ElementId>();
                                    if (doc.ActiveView.ViewType == ViewType.Elevation || doc.ActiveView.ViewType == ViewType.Detail || doc.ActiveView.ViewType == ViewType.FloorPlan)
                                    {
                                        if (doc.ActiveView.ViewTemplateId == ElementId.InvalidElementId)
                                        {
                                            string detailNumber = "";
                                            string sheet = "";
                                            Autodesk.Revit.DB.View view = doc.GetElement(doc.ActiveView.Id) as Autodesk.Revit.DB.View;
                                            ParameterSet pset = view.Parameters;
                                            foreach (Parameter p in pset)
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
                                            MessageBox.Show("The following view has the View Template set to <None>. Set the view template to the appropriate selection before using this command." + System.Environment.NewLine + detailNumber + " / " + sheet, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                        else
                                        {
                                            listSingleView.Add(doc.ActiveView.Id);
                                        }
                                    }
                                    if (listSingleView.Any())
                                    {
                                        passed = collectTags(listSingleView, doc);

                                        if (foundNotEditible)
                                        {
                                            if (string.IsNullOrWhiteSpace(user))
                                            {
                                                MessageBox.Show("Elements were found that are checked out from the central model by other users. The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            }
                                            else
                                            {
                                                MessageBox.Show("Elements were found that are checked out from the central model by " + user + ". The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            }
                                        }
                                        else if (!passed)
                                        {
                                            MessageBox.Show("Error obtaining elements from active view.", "STPV4 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                        else
                                        {
                                            showTags(doc, viewtempID, FPviewtempID);
                                        }

                                    }
                                }
                                catch
                                {
                                    MessageBox.Show("Error obtaining elements from active view.", "STPV5 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                        }
                        catch
                        {
                            try
                            {
                                List<ElementId> listSingleView = new List<ElementId>();
                                if (doc.ActiveView.ViewType == ViewType.Elevation || doc.ActiveView.ViewType == ViewType.Detail || doc.ActiveView.ViewType == ViewType.FloorPlan)
                                {
                                    if (doc.ActiveView.ViewTemplateId == ElementId.InvalidElementId)
                                    {
                                        string detailNumber = "";
                                        string sheet = "";
                                        Autodesk.Revit.DB.View view = doc.GetElement(doc.ActiveView.Id) as Autodesk.Revit.DB.View;
                                        ParameterSet pset = view.Parameters;
                                        foreach (Parameter p in pset)
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
                                        MessageBox.Show("The following view has the View Template set to <None>. Set the view template to the appropriate selection before using this command." + System.Environment.NewLine + detailNumber, "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                    else
                                    {
                                        listSingleView.Add(doc.ActiveView.Id);
                                    }
                                }
                                if (listSingleView.Any())
                                {
                                    passed = collectTags(listSingleView, doc);

                                    if (foundNotEditible)
                                    {
                                        if (string.IsNullOrWhiteSpace(user))
                                        {
                                            MessageBox.Show("Elements were found that are checked out from the central model by other users. The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                        else
                                        {
                                            MessageBox.Show("Elements were found that are checked out from the central model by " + user + ". The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                    }
                                    else if (!passed)
                                    {
                                        MessageBox.Show("Error obtaining elements from active view.", "STPV6 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                    else
                                    {
                                        showTags(doc, viewtempID, FPviewtempID);
                                    }
                                }
                            }
                            catch
                            {
                                MessageBox.Show("Error obtaining elements from active view.", "STPV7 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Active view is null", "STPV1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
            }
            return Result.Succeeded;
        }

        /// <summary>
        /// For each view in <paramref name="viewList"/>, finds every "GAIT - Piece Tag" family
        /// instance owned by that view, then attempts to check out the view itself plus all of its
        /// tags (<see cref="WorksharingUtils.CheckoutElements"/>). If anything fails checkout,
        /// <see cref="foundNotEditible"/>/<see cref="user"/> are set (so the caller can report who
        /// holds it) and collection stops immediately. On success, stores each view's tag ids in
        /// <see cref="idDictionary"/> for <see cref="showTags"/> to act on. Returns false if nothing
        /// could be collected (empty input, or a checkout failure).
        /// </summary>
        public bool collectTags(List<ElementId> viewList, Document doc)
        {
            string targetName = "GAIT - Piece Tag";

            if (viewList.Any())
            {
                foreach (ElementId eid in viewList)
                {
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
                    totalList.Add(eid);   // check out the view element itself too (its template gets changed)
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
                        if (idDictionary.ContainsKey(eid))
                        {
                            idDictionary[eid] = listForDictionary;
                        }
                        else
                        {
                            idDictionary.Add(eid, listForDictionary);
                        }
                    }
                }

                if (foundNotEditible || !idDictionary.Any())
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// For every view collected into <see cref="idDictionary"/>: unhides its tags, records the
        /// view's current (non-tag) template id to a per-view file under the project's
        /// "GMS config\ViewTemplateData" folder (so <see cref="HideTags.hideTags"/> can restore it
        /// later), then switches the view's template to the Floor-Plan-Tag or Elevation-Tag template
        /// depending on view type. Each view is updated in its own transaction.
        /// </summary>
        public void showTags(Document doc, ElementId viewTemp, ElementId FPviewTemp)
        {
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

            foreach (KeyValuePair<ElementId, List<ElementId>> kvp in idDictionary)
            {
                Autodesk.Revit.DB.View v = doc.GetElement(kvp.Key) as Autodesk.Revit.DB.View;
                using (Transaction tr = new Transaction(doc))
                {
                    tr.Start("Show Tags in View");
                    List<ElementId> tagListFromView = kvp.Value;
                    if (tagListFromView.Any())
                    {
                        v.UnhideElements(tagListFromView);
                    }

                    // Only record the prior template if it isn't already a "Tag" template — otherwise
                    // re-running Show Tags on an already-tag-templated view would overwrite the
                    // saved original template with another tag template.
                    if (!string.IsNullOrWhiteSpace(modelPath) && !doc.GetElement(v.ViewTemplateId).Name.Contains("Tag"))
                    {
                        string VTFile = Path.Combine(viewTemplatesDirectory, v.Id.ToString());

                        if (File.Exists(VTFile))
                        {
                            File.Delete(VTFile);
                        }

                        StreamWriter sw = null;
                        using (sw = File.AppendText(VTFile))
                        {
                            sw.WriteLine(v.ViewTemplateId.ToString());
                        }
                        sw.Close();
                    }

                    // Floor plans (and non-callout details) get the Floor-Plan tag template; anything
                    // else (elevations, callouts) gets the Elevations tag template.
                    if (v.ViewType == ViewType.FloorPlan || (v.ViewType == ViewType.Detail && v.IsCallout == false))
                    {
                        v.ViewTemplateId = FPviewTemp;
                    }
                    else
                    {
                        v.ViewTemplateId = viewTemp;
                    }

                    tr.Commit();
                }
            }
        }
    }

    /// <summary>
    /// The inverse of <see cref="ShowTags"/>: re-hides the "GAIT - Piece Tag" annotations on the
    /// active sheet's eligible viewports and restores each view's template from the id previously
    /// saved by <see cref="ShowTags.showTags"/> (read back from the per-view file under
    /// "GMS config\ViewTemplateData"). If no saved file exists for a view, its template is left as-is.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class HideTags : IExternalCommand
    {
        public static Dictionary<ElementId, List<ElementId>> idDictionary = new Dictionary<ElementId, List<ElementId>>();
        public static bool foundNotEditible = false;
        public static string user = "";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            bool passed = false;

            foundNotEditible = false;
            user = "";

            if (doc.IsWorkshared && !doc.IsDetached && !doc.IsFamilyDocument)
            {
                Autodesk.Revit.DB.View activeView = uiApp.ActiveUIDocument.ActiveView;

                if (activeView != null)
                {
                    idDictionary.Clear();
                    try
                    {
                        ViewSheet vs = doc.GetElement(activeView.Id) as ViewSheet;
                        ICollection<ElementId> views = vs.GetAllViewports();
                        List<ElementId> listViews = new List<ElementId>(); ;

                        if (views.Any())
                        {
                            foreach (ElementId elmid in views)
                            {
                                Viewport vp = doc.GetElement(elmid) as Viewport;
                                ElementId viewID = vp.ViewId;
                                Autodesk.Revit.DB.View view = doc.GetElement(viewID) as Autodesk.Revit.DB.View;
                                if (view.ViewType == ViewType.Elevation || view.ViewType == ViewType.Detail || view.ViewType == ViewType.FloorPlan)
                                {
                                    listViews.Add(viewID);
                                }
                            }

                            passed = collectTags(listViews, doc);

                            if (foundNotEditible)
                            {
                                if (string.IsNullOrWhiteSpace(user))
                                {
                                    MessageBox.Show("Elements were found that are checked out from the central model by other users. The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                                else
                                {
                                    MessageBox.Show("Elements were found that are checked out from the central model by " + user + ". The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                            }
                            else if (!passed)
                            {
                                List<ElementId> listSingleView = new List<ElementId>();
                                listSingleView.Add(doc.ActiveView.Id);
                                passed = collectTags(listSingleView, doc);

                                if (foundNotEditible)
                                {
                                    if (string.IsNullOrWhiteSpace(user))
                                    {
                                        MessageBox.Show("Elements were found that are checked out from the central model by other users. The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Elements were found that are checked out from the central model by " + user + ". The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                }
                                else if (!passed)
                                {
                                    MessageBox.Show("Error obtaining elements from active view.", "STPV3 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                                else
                                {
                                    hideTags(doc);
                                }
                            }
                            else
                            {
                                hideTags(doc);
                            }
                        }
                        else
                        {
                            try
                            {
                                List<ElementId> listSingleView = new List<ElementId>();
                                if (doc.ActiveView.ViewType == ViewType.Elevation || doc.ActiveView.ViewType == ViewType.Detail || doc.ActiveView.ViewType == ViewType.FloorPlan)
                                {
                                    listSingleView.Add(doc.ActiveView.Id);
                                }
                                if (listSingleView.Any())
                                {
                                    passed = collectTags(listSingleView, doc);

                                    if (foundNotEditible)
                                    {
                                        if (string.IsNullOrWhiteSpace(user))
                                        {
                                            MessageBox.Show("Elements were found that are checked out from the central model by other users. The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                        else
                                        {
                                            MessageBox.Show("Elements were found that are checked out from the central model by " + user + ". The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        }
                                    }
                                    else if (!passed)
                                    {
                                        MessageBox.Show("Error obtaining elements from active view.", "STPV4 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                    else
                                    {
                                        hideTags(doc);
                                    }
                                }
                            }
                            catch
                            {
                                MessageBox.Show("Error obtaining elements from active view.", "STPV5 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                            }
                        }
                    }
                    catch
                    {
                        try
                        {
                            List<ElementId> listSingleView = new List<ElementId>();
                            if (doc.ActiveView.ViewType == ViewType.Elevation || doc.ActiveView.ViewType == ViewType.Detail || doc.ActiveView.ViewType == ViewType.FloorPlan)
                            {
                                listSingleView.Add(doc.ActiveView.Id);
                            }
                            if (listSingleView.Any())
                            {
                                passed = collectTags(listSingleView, doc);

                                if (foundNotEditible)
                                {
                                    if (string.IsNullOrWhiteSpace(user))
                                    {
                                        MessageBox.Show("Elements were found that are checked out from the central model by other users. The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                    else
                                    {
                                        MessageBox.Show("Elements were found that are checked out from the central model by " + user + ". The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "STPV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                }
                                else if (!passed)
                                {
                                    MessageBox.Show("Error obtaining elements from active view.", "STPV6 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                                else
                                {
                                    hideTags(doc);
                                }
                            }
                        }
                        catch
                        {
                            MessageBox.Show("Error obtaining elements from active view.", "STPV7 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }

                    }
                }
                else
                {
                    MessageBox.Show("Active view is null", "STPV1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            return Result.Succeeded;
        }


        /// <summary>Same collection/checkout logic as <see cref="ShowTags.collectTags"/> — finds each
        /// view's "GAIT - Piece Tag" instances and checks out the view + its tags, populating
        /// <see cref="idDictionary"/> for <see cref="hideTags"/>.</summary>
        public bool collectTags(List<ElementId> viewList, Document doc)
        {
            string targetName = "GAIT - Piece Tag";

            if (viewList.Any())
            {
                foreach (ElementId eid in viewList)
                {
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
                        if (idDictionary.ContainsKey(eid))
                        {
                            idDictionary[eid] = listForDictionary;
                        }
                        else
                        {
                            idDictionary.Add(eid, listForDictionary);
                        }
                    }
                }

                if (foundNotEditible || !idDictionary.Any())
                {
                    return false;
                }
                else
                {
                    return true;
                }
            }
            else
            {
                return false;
            }
        }

        /// <summary>
        /// Hides each collected view's tags immediately, then — for views with a saved template file
        /// under "GMS config\ViewTemplateData" — reads back the original (pre-Show-Tags) template id
        /// and restores it. Hiding happens in its own first pass/transaction set so tags disappear
        /// even for views that don't have (or fail to read) a saved template file.
        /// </summary>
        public void hideTags(Document doc)
        {
            foreach (KeyValuePair<ElementId, List<ElementId>> kvp in idDictionary)
            {
                Autodesk.Revit.DB.View v = doc.GetElement(kvp.Key) as Autodesk.Revit.DB.View;
                using (Transaction tr = new Transaction(doc))
                {
                    tr.Start("Hide Tags in View");
                    List<ElementId> tagListFromView = kvp.Value;
                    if (tagListFromView.Any())
                    {
                        v.HideElements(tagListFromView);
                    }

                    tr.Commit();
                }
            }

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

            foreach (KeyValuePair<ElementId, List<ElementId>> kvp in idDictionary)
            {
                Autodesk.Revit.DB.View v = doc.GetElement(kvp.Key) as Autodesk.Revit.DB.View;
                using (Transaction tr = new Transaction(doc))
                {
                    tr.Start("Hide Tags in View");
                    List<ElementId> tagListFromView = kvp.Value;
                    if (tagListFromView.Any())
                    {
                        v.HideElements(tagListFromView);
                    }

                    if (!string.IsNullOrWhiteSpace(modelPath))
                    {
                        // The saved file (written by ShowTags.showTags) holds the view's pre-Show-Tags
                        // template id as a single line of text; restore it if present.
                        string VTFile = Path.Combine(viewTemplatesDirectory, v.Id.ToString());

                        if (File.Exists(VTFile))
                        {
                            var fileStream = new FileStream(VTFile, FileMode.Open, FileAccess.Read);
                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                            {
                                string line;
                                while ((line = streamReader.ReadLine()) != null)
                                {
                                    try
                                    {
                                        if (!string.IsNullOrWhiteSpace(line))
                                        {
                                            long viewTemp = Convert.ToInt64(line);
                                            v.ViewTemplateId = new ElementId(viewTemp);
                                        }
                                    }
                                    catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("ShowTagsPerView", __ex); }
                                }
                                streamReader.Close();
                            }
                        }
                    }
                    tr.Commit();
                }
            }
        }
    }
}