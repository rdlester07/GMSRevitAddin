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
using System.Xml.Linq;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;

namespace ViewNames
{
    /// <summary>
    /// Revit <c>IUpdater</c> that keeps a viewport's VIEW_NAME in sync with the sheet/detail number
    /// and title-on-sheet whenever a watched element changes. An "Updater" in Revit-API terms is a
    /// callback that Revit invokes automatically during document regeneration whenever elements
    /// matching a registered filter/change-type are modified — no user action beyond editing the
    /// model is required (registration happens once, in <see cref="CmddetailWatcherUpdater"/>, via
    /// <c>UpdaterRegistry.RegisterUpdater</c> + <c>AddTrigger</c>). The naming convention enforced
    /// here is <c>"{SheetNumber}_{DetailNumber}_{TitleOnSheet}"</c>, with any of a fixed set of
    /// filesystem/Revit-unsafe characters stripped from the title (see <c>specialChars</c> below).
    /// </summary>
    public class detailWatcherUpdater : IUpdater
    {
        static AddInId _appId;
        static UpdaterId _updaterId;

        public detailWatcherUpdater(AddInId id)
        {
            _appId = id;
            _updaterId = new UpdaterId(_appId, Guid.NewGuid());
        }

        /// <summary>
        /// Runs once per matching element change (see class summary for the Updater trigger model).
        /// Rebuilds VIEW_NAME as "{SheetNumber}_{DetailNumber}_{TitleOnSheet}" for each modified
        /// viewport, only if the "ViewName" setting is enabled.
        /// </summary>
        public void Execute(UpdaterData data)
        {
            if (GMSRevitAddin.Properties.Settings.Default.ViewName)
            {
                Document doc = data.GetDocument();
                Autodesk.Revit.ApplicationServices.Application app = doc.Application;
                char[] specialChars = new char[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };

                foreach (ElementId id in data.GetModifiedElementIds())
                {
                    try
                    {
                        Element elem = doc.GetElement(id);
                        string text = "";
                        string sheetnum = "";
                        string detailnum = "";
                        string titleOnSheet = "";

                        string testDN = elem.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER).AsString();
                        if (!string.IsNullOrWhiteSpace(testDN))
                        {
                            detailnum = testDN;
                        }
                        string testSN = elem.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER).AsString();
                        if (!string.IsNullOrWhiteSpace(testSN))
                        {
                            sheetnum = testSN;
                        }
                        string testTN = elem.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION).AsString();
                        if (!string.IsNullOrWhiteSpace(testTN))
                        {
                            titleOnSheet = testTN;
                            if (titleOnSheet.IndexOfAny(specialChars) != -1)
                            {
                                string[] temp = titleOnSheet.Split(specialChars, StringSplitOptions.RemoveEmptyEntries);
                                titleOnSheet = String.Join(" ", temp);
                            }
                        }
                        else
                        {
                            string currentViewName = elem.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();
                            elem.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION).Set(currentViewName);
                            titleOnSheet = elem.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION).AsString();
                        }

                        if (!string.IsNullOrWhiteSpace(sheetnum) && !string.IsNullOrWhiteSpace(detailnum) && !string.IsNullOrWhiteSpace(titleOnSheet))
                        {
                            text = sheetnum + "_" + detailnum + "_" + titleOnSheet;
                            string currentViewName = elem.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();
                            if (currentViewName != text)
                            {
                                elem.get_Parameter(BuiltInParameter.VIEW_NAME).Set(text);
                            }
                        }
                    }
                    catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("ViewNames", __ex); }
                    //catch (Exception e)
                    //{
                    //    TaskDialog.Show("Error", e.ToString());
                    //}
                }
            }
        }

        public string GetAdditionalInformation()
        {
            return "Based on the Dynamic Model Updater example found on The Building Coder, " + "https://thebuildingcoder.typepad.com/blog/2012/06/documentchanged-versus-dynamic-model-updater.html";
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.FloorsRoofsStructuralWalls;
        }

        public UpdaterId GetUpdaterId()
        {
            return _updaterId;
        }

        public string GetUpdaterName()
        {
            return "ViewNameFromSheet";
        }
    }


    public class SheetWatcherUpdater : IUpdater
    {
        static AddInId _appId;
        static UpdaterId _updaterId;
        public static System.Timers.Timer timer = null;

        public SheetWatcherUpdater(AddInId id)
        {
            _appId = id;
            _updaterId = new UpdaterId(_appId, Guid.NewGuid());
        }

        public void Execute(UpdaterData data)
        {
            if (GMSRevitAddin.Properties.Settings.Default.ViewName)
            {
                Document doc = data.GetDocument();
                Autodesk.Revit.ApplicationServices.Application app = doc.Application;
                char[] specialChars = new char[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };

                foreach (ElementId id in data.GetModifiedElementIds())
                {
                    try
                    {
                        Element elem = doc.GetElement(id);
                        ViewSheet viewSheet = elem as ViewSheet;
                        ICollection<ElementId> views = viewSheet.GetAllPlacedViews();
                        foreach (ElementId eid in views)
                        {
                            Element vpEl = doc.GetElement(eid);
                            string text = "";
                            string sheetnum = "";
                            string detailnum = "";
                            string titleOnSheet = "";

                            string testDN = vpEl.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER).AsString();
                            if (!string.IsNullOrWhiteSpace(testDN))
                            {
                                detailnum = testDN;
                            }
                            string testSN = vpEl.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER).AsString();
                            if (!string.IsNullOrWhiteSpace(testSN))
                            {
                                sheetnum = testSN;
                            }
                            string testTN = vpEl.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION).AsString();
                            if (!string.IsNullOrWhiteSpace(testTN))
                            {
                                titleOnSheet = testTN;
                                if (titleOnSheet.IndexOfAny(specialChars) != -1)
                                {
                                    string[] temp = titleOnSheet.Split(specialChars, StringSplitOptions.RemoveEmptyEntries);
                                    titleOnSheet = String.Join(" ", temp);
                                }
                            }
                            else
                            {
                                string currentViewName = vpEl.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();
                                vpEl.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION).Set(currentViewName);
                                titleOnSheet = vpEl.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION).AsString();
                            }

                            if (!string.IsNullOrWhiteSpace(sheetnum) && !string.IsNullOrWhiteSpace(detailnum) && !string.IsNullOrWhiteSpace(titleOnSheet))
                            {
                                if (!sheetnum.Contains("U-"))
                                {
                                    text = sheetnum + "_" + detailnum + "_" + titleOnSheet;
                                    string currentViewName = vpEl.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();
                                    if (currentViewName != text)
                                    {
                                        vpEl.get_Parameter(BuiltInParameter.VIEW_NAME).Set(text);
                                    }
                                }
                                else
                                {
                                    if (vpEl.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION).AsString() != sheetnum)
                                    {
                                        vpEl.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION).Set(sheetnum);
                                    }
                                    text = sheetnum + "_" + detailnum + "_" + sheetnum;
                                    string currentViewName = vpEl.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();
                                    if (currentViewName != text)
                                    {
                                        vpEl.get_Parameter(BuiltInParameter.VIEW_NAME).Set(text);
                                    }
                                    string targetName = "GAIT - Piece Tag";

                                    try
                                    {
                                        foreach (ElementId elid in views)
                                        {
                                            List<FamilyInstance> needsUpdating = new List<FamilyInstance>();

                                            List<FamilyInstance> familyInstances = new FilteredElementCollector(doc, elid).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().Where(x => x.Symbol.Family.Name.Equals(targetName)).ToList<FamilyInstance>();
                                            string sheetNumber = viewSheet.SheetNumber;
                                            foreach (FamilyInstance familyInstance in familyInstances)
                                            {
                                                string prefix = familyInstance.LookupParameter("Prefix").AsString();
                                                string number = familyInstance.LookupParameter("Number").AsString();
                                                string origin = familyInstance.LookupParameter("Origin").AsString();
                                                string type = familyInstance.LookupParameter("Type").AsValueString();
                                                string oSheet = familyInstance.LookupParameter("Origin Sheet").AsString();
                                                string piece = familyInstance.LookupParameter("Piece").AsString();
                                                string description = familyInstance.LookupParameter("Piece Description").AsString();

                                                if (prefix != null && number != null && origin != null && type != null && oSheet != null && piece != null && description != null)
                                                {
                                                    string key = "";
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
                                                    if ((origin != sheetNumber || oSheet != sheetNumber || piece != prefix + "-" + number || description != testDescription) && !needsUpdating.Contains(familyInstance) && !string.IsNullOrWhiteSpace(prefix) && !string.IsNullOrWhiteSpace(number))
                                                    {
                                                        needsUpdating.Add(familyInstance);
                                                    }
                                                }
                                            }

                                            if (needsUpdating.Any())
                                            {
                                                List<ElementId> ids = new List<ElementId>();
                                                List<string> users = new List<string>();
                                                foreach (FamilyInstance fam in needsUpdating)
                                                {
                                                    ids.Add(fam.Id);
                                                }
                                                ICollection<ElementId> OKtoEdit = WorksharingUtils.CheckoutElements(doc, ids);
                                                foreach (ElementId ii in ids)
                                                {
                                                    if (!OKtoEdit.Contains(ii))
                                                    {
                                                        string user = "";
                                                        WorksharingUtils.GetCheckoutStatus(doc, ii, out user);
                                                        if (!users.Contains(user))
                                                        {
                                                            users.Add(user);
                                                        }
                                                    }
                                                }

                                                if (users.Any())
                                                {
                                                    string uNames = "";
                                                    foreach (string uName in users)
                                                    {
                                                        uNames += uName + " ";
                                                    }
                                                    MessageBox.Show("Inccorect tag data found on sheet but unable to Update data. Tag(s) are checked out from the central model by the following user(s):" + System.Environment.NewLine +
                                                        uNames.Trim() + System.Environment.NewLine + "Tag Update on this sheet has been cancelled.", "UO1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                }
                                                else
                                                {
                                                    //correct tags
                                                    bool error = false;
                                                    foreach (FamilyInstance fi in needsUpdating)
                                                    {
                                                        string prefix = fi.LookupParameter("Prefix").AsString();
                                                        string number = fi.LookupParameter("Number").AsString();
                                                        string type = fi.LookupParameter("Type").AsValueString();

                                                        Parameter origin = fi.LookupParameter("Origin");
                                                        Parameter oSheet = fi.LookupParameter("Origin Sheet");
                                                        Parameter piece = fi.LookupParameter("Piece");
                                                        Parameter description = fi.LookupParameter("Piece Description");

                                                        if (origin != null && origin.AsString() != sheetNumber)
                                                        {
                                                            try
                                                            {
                                                                origin.Set(sheetNumber);
                                                            }
                                                            catch
                                                            {
                                                                error = true;
                                                            }
                                                        }

                                                        if (oSheet != null && oSheet.AsString() != sheetNumber)
                                                        {
                                                            try
                                                            {
                                                                oSheet.Set(sheetNumber);
                                                            }
                                                            catch
                                                            {
                                                                error = true;
                                                            }
                                                        }

                                                        string testPiece = prefix + "-" + number;
                                                        if (piece != null && piece.AsString() != testPiece)
                                                        {
                                                            try
                                                            {
                                                                piece.Set(testPiece);
                                                            }
                                                            catch
                                                            {
                                                                error = true;
                                                            }
                                                        }

                                                        string key = "";
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
                                                        if (description != null && description.AsString() != testDescription)
                                                        {
                                                            try
                                                            {
                                                                description.Set(testDescription);
                                                            }
                                                            catch
                                                            {
                                                                error = true;
                                                            }
                                                        }
                                                    }

                                                    if (error)
                                                    {
                                                        MessageBox.Show("Error updating incorrect tag data on sheet " + sheetNumber, "UO2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                    }
                                                }
                                            }
                                        }

                                        // REMOVED (2026-08-18): this used to rename the model's *shared*
                                        // "(Do Not Open) Unit Pieces" schedule to "UNIT_<sheet> PIECES" and
                                        // rebuild its single Origin-equals filter to match whichever sheet
                                        // last triggered this Updater (which fires on ANY change to an
                                        // OST_Sheets element, including a plain rename). That was the old
                                        // per-unit-schedule mechanism (one schedule per unit, self-filtered
                                        // on its own Origin) and actively fights the shared-schedule design
                                        // CreateUnitSheet.cs has used since 2026-07-31 (one constant-named
                                        // schedule placed on every unit sheet, filtered per-instance via
                                        // Revit's native "Filter by: Sheet" instead of a hand-maintained
                                        // single-value filter). Left running, any sheet edit could rename
                                        // the shared schedule and clobber its filter down to one sheet.
                                        // The identical block in UpdateOrigins.cs's onViewChange (fires on
                                        // every view activation — an even more frequent trigger) was removed
                                        // at the same time. TagUpdateCurrentSheet.cs's manual "Update Tags"
                                        // command still has this same pattern but was left alone — that's a
                                        // deliberate user action, not a background event.
                                    }
                                    catch (Exception excep)
                                    {
                                        GMSRevitAddin.GmsUi.ShowError(excep, "VN5 Error");
                                    }
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        TaskDialog.Show("VN6 Error", e.Message);
                    }
                }
            }
        }


        public string GetAdditionalInformation()
        {
            return "Based on the Dynamic Model Updater example found on The Building Coder, " + "https://thebuildingcoder.typepad.com/blog/2012/06/documentchanged-versus-dynamic-model-updater.html";
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.FloorsRoofsStructuralWalls;
        }

        public UpdaterId GetUpdaterId()
        {
            return _updaterId;
        }

        public string GetUpdaterName()
        {
            return "Sheet";
        }
    }

    public class DeletedWatcherUpdater : IUpdater
    {
        static AddInId _appId;
        static UpdaterId _updaterId;

        public DeletedWatcherUpdater(AddInId id)
        {
            _appId = id;
            _updaterId = new UpdaterId(_appId, Guid.NewGuid());
        }

        public void Execute(UpdaterData data)
        {
            if (GMSRevitAddin.Properties.Settings.Default.ViewName)
            {
                try
                {
                    Document doc = data.GetDocument();
                    Autodesk.Revit.ApplicationServices.Application app = doc.Application;
                    FilteredElementCollector fec = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views);
                    fec.WhereElementIsNotElementType();
                    List<Element> viewList = (List<Element>)fec.ToElements().ToList();
                    List<Element> viewsWithSameName = new List<Element>();
                    string finalText = "";
                    int iteration = 0;
                    char[] specialChars = new char[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };

                    foreach (Element el in viewList)
                    {
                        finalText = "";
                        iteration = 0;
                        Parameter p = el.get_Parameter(BuiltInParameter.VIEW_SHEET_VIEWPORT_INFO);
                        Parameter vDef = el.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION);
                        string vname = el.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();
                        string titleOnSheet = vDef.AsString();

                        if (string.IsNullOrWhiteSpace(titleOnSheet))
                        {
                            el.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION).Set(vname);
                        }
                        else if (titleOnSheet.IndexOfAny(specialChars) != -1)
                        {
                            string[] temp = titleOnSheet.Split(specialChars, StringSplitOptions.RemoveEmptyEntries);
                            titleOnSheet = String.Join(" ", temp);
                        }

                        if (p.AsString() == "Not in a sheet" && vname.Contains("_" + vDef.AsString()) && !vname.Contains("_REMOVED_") && !vname.Contains("{") && !vname.Contains("}"))
                        {
                            foreach (Element el1 in viewList)
                            {
                                string Rvname = el1.Name;
                                if (Rvname.Contains(vname) && Rvname.Contains("_REMOVED_"))
                                {
                                    viewsWithSameName.Add(el1);
                                }
                            }

                            List<string> temp = new List<string>();
                            string noNumber = "";

                            if (viewsWithSameName.Count > 0)
                            {
                                temp.Clear();
                                int lastNumber = 0;
                                foreach (Element el2 in viewsWithSameName)
                                {
                                    string name = el2.Name;
                                    name = name.Trim('_');
                                    string[] parse = name.Split('_');

                                    try
                                    {
                                        lastNumber = Convert.ToInt32(parse[parse.Length - 1]);
                                        if (!temp.Any())
                                        {
                                            foreach (string s in parse)
                                            {
                                                temp.Add(s);
                                            }
                                        }
                                    }
                                    catch
                                    {
                                        lastNumber = 0;
                                        if (!temp.Any())
                                        {
                                            noNumber = name;
                                        }
                                    }

                                    if (lastNumber > iteration)
                                    {
                                        iteration = lastNumber;
                                    }
                                }
                            }

                            if (viewsWithSameName.Count > 0 && iteration > 0)
                            {
                                iteration++;
                                if (temp.Any())
                                {
                                    finalText = "";
                                    temp[temp.Count - 1] = iteration.ToString();
                                    foreach (string s1 in temp)
                                    {
                                        finalText = finalText + "_" + s1;
                                    }
                                }
                                else
                                {
                                    finalText = noNumber + "_" + iteration.ToString();
                                }

                            }
                            else if (viewsWithSameName.Count > 0 && iteration == 0)
                            {
                                iteration++;
                                finalText = "_REMOVED_" + vname + "_" + iteration.ToString();

                            }
                            else
                            {
                                finalText = "_REMOVED_" + vname;
                            }

                            el.get_Parameter(BuiltInParameter.VIEW_NAME).Set(finalText);
                        }
                    }
                }
                catch (Exception e)
                {
                    TaskDialog.Show("VN7 Error", e.Message);
                }
            }
        }

        public string GetAdditionalInformation()
        {
            return "Based on the Dynamic Model Updater example found on The Building Coder, " + "https://thebuildingcoder.typepad.com/blog/2012/06/documentchanged-versus-dynamic-model-updater.html";
        }

        public ChangePriority GetChangePriority()
        {
            return ChangePriority.FloorsRoofsStructuralWalls;
        }

        public UpdaterId GetUpdaterId()
        {
            return _updaterId;
        }

        public string GetUpdaterName()
        {
            return "test";
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmddetailWatcherUpdater : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            return Execute(commandData.Application, doc);
        }
        public Result Execute(UIApplication uiapp, Document doc)
        {
            Autodesk.Revit.ApplicationServices.Application app = uiapp.Application;
            try
            {
                ElementCategoryFilter f = new ElementCategoryFilter(BuiltInCategory.OST_Viewports);
                ElementCategoryFilter f1 = new ElementCategoryFilter(BuiltInCategory.OST_Sheets);

                detailWatcherUpdater Dupdater = new detailWatcherUpdater(app.ActiveAddInId);
                UpdaterRegistry.RegisterUpdater(Dupdater, true);
                UpdaterRegistry.AddTrigger(Dupdater.GetUpdaterId(), f, Element.GetChangeTypeAny());

                SheetWatcherUpdater D1updater = new SheetWatcherUpdater(app.ActiveAddInId);
                UpdaterRegistry.RegisterUpdater(D1updater, true);
                UpdaterRegistry.AddTrigger(D1updater.GetUpdaterId(), f1, Element.GetChangeTypeAny());

                DeletedWatcherUpdater tupdater = new DeletedWatcherUpdater(app.ActiveAddInId);
                UpdaterRegistry.RegisterUpdater(tupdater, true);
                UpdaterRegistry.AddTrigger(tupdater.GetUpdaterId(), f, Element.GetChangeTypeElementDeletion());
                UpdaterRegistry.AddTrigger(tupdater.GetUpdaterId(), f1, Element.GetChangeTypeElementDeletion());


                return Result.Succeeded;
            }
            catch (Exception e)
            {
                TaskDialog.Show("VN8 Error", e.Message);
                return Result.Failed;
            }
        }
    }

    public class RevitStartup : IExternalApplication
    {
        public static bool running = false;

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            //application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;  NOT NEEDED AS CALLED FROM GMS_tools
            return Result.Succeeded;

        }

        public static void ControlledApplication_DocumentOpened(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs e)
        {
            if (!running)
            {
                running = true;
                var command = new CmddetailWatcherUpdater();
                command.Execute(new UIApplication(sender as Autodesk.Revit.ApplicationServices.Application), e.Document);
            }
        }
    }

    public static class ManualUpdate
    {
        public static void Update(Document doc)
        {
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            FilteredElementCollector fec = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Views);
            fec.WhereElementIsNotElementType();
            List<Element> viewList = (List<Element>)fec.ToElements().ToList();
            List<Element> errors = new List<Element>();
            List<string> errMsg = new List<string>();
            char[] specialChars = new char[] { '\\', ':', '{', '}', '[', ']', '|', ';', '<', '>', '?', '`', '~' };

            foreach (Element elem in viewList)
            {
                Parameter p = elem.get_Parameter(BuiltInParameter.VIEW_SHEET_VIEWPORT_INFO);
                if (p.AsString() != "Not in a sheet")
                {
                    try
                    {
                        string text = "";
                        string sheetnum = "";
                        string detailnum = "";
                        string titleOnSheet = "";

                        string testDN = elem.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER).AsString();
                        if (!string.IsNullOrWhiteSpace(testDN))
                        {
                            detailnum = testDN;
                        }
                        string testSN = elem.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER).AsString();
                        if (!string.IsNullOrWhiteSpace(testSN))
                        {
                            sheetnum = testSN;
                        }
                        string testTN = elem.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION).AsString();
                        if (!string.IsNullOrWhiteSpace(testTN))
                        {
                            titleOnSheet = testTN;
                            if (titleOnSheet.IndexOfAny(specialChars) != -1)
                            {
                                string[] temp = titleOnSheet.Split(specialChars, StringSplitOptions.RemoveEmptyEntries);
                                titleOnSheet = String.Join(" ", temp);
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(sheetnum) && !string.IsNullOrWhiteSpace(detailnum) && string.IsNullOrWhiteSpace(titleOnSheet))
                        {
                            using (Transaction tr1 = new Transaction(doc, "Update Title on Sheet"))
                            {
                                tr1.Start();
                                string currentViewName = elem.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();
                                elem.get_Parameter(BuiltInParameter.VIEW_DESCRIPTION).Set(currentViewName);
                                tr1.Commit();
                            }
                        }

                        if (!string.IsNullOrWhiteSpace(sheetnum) && !string.IsNullOrWhiteSpace(detailnum) && !string.IsNullOrWhiteSpace(titleOnSheet))
                        {
                            text = sheetnum + "_" + detailnum + "_" + titleOnSheet;

                            string currentViewName = elem.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();

                            if (currentViewName != text)
                            {
                                using (Transaction tr = new Transaction(doc, "Update View Name"))
                                {
                                    tr.Start();
                                    elem.get_Parameter(BuiltInParameter.VIEW_NAME).Set(text);
                                    tr.Commit();
                                }
                            }
                        }
                    }
                    catch (Exception e)
                    {
                        errors.Add(elem);
                        errMsg.Add(e.Message);
                    }
                }
            }

            if (errors.Any())
            {
                string text = "The following view(s) caused an unknown error and were not updated:";
                if (errors.Count == errMsg.Count)
                {
                    int ite = 0;
                    foreach (Element el in errors)
                    {
                        string vname = el.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();
                        text = text + System.Environment.NewLine + vname;
                        text = text + System.Environment.NewLine + errMsg[ite];
                        text = text + System.Environment.NewLine;
                        ite++;
                    }
                }
                else
                {
                    foreach (Element el in errors)
                    {
                        string vname = el.get_Parameter(BuiltInParameter.VIEW_NAME).AsString();
                        text = text + System.Environment.NewLine + vname;
                    }
                }
                MessageBox.Show(text, "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else
            {
                MessageBox.Show("View name manual Update complete.");
            }
        }
    }
}