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
using System.IO;
using System.Windows.Forms;
using Application = Autodesk.Revit.ApplicationServices.Application;
using View = Autodesk.Revit.DB.View;

namespace TagParameterUpdater
{

    /// <summary>
    /// A Revit <see cref="IUpdater"/> that keeps curtain-wall-panel tag/unit parameters in sync
    /// whenever their triggering parameters change. An <see cref="IUpdater"/> is registered once
    /// (see <see cref="CmdTagParameterUpdater"/> below, via <c>UpdaterRegistry.RegisterUpdater</c> +
    /// <c>AddTrigger</c>) and thereafter Revit calls <see cref="Execute"/> automatically — inside the
    /// same transaction — any time one of the registered trigger conditions (specific parameter
    /// changes / element additions on specific categories) occurs; there is no manual invocation.
    /// Handles two cases: (1) a "Generic Annotations" element (the GAIT piece tag) whose Origin/Piece/
    /// Description parameters need to be derived from its owning view's sheet, and (2) a "Curtain
    /// Panels" element (a unit) whose Origin/Origin Sheet need to be derived from the elevation/detail
    /// view that its "Unit / Address"/"Unit Tag" annotation tag lives on.
    /// </summary>
    public class TagParameterUpdater : IUpdater
    {
        static AddInId _appId;
        static UpdaterId _updaterId;

        public TagParameterUpdater(AddInId id)
        {
            _appId = id;

            _updaterId = new UpdaterId(_appId, Guid.NewGuid());
        }

        /// <summary>
        /// Called by Revit for every element whose registered trigger fired in the current transaction
        /// (see <see cref="UpdaterRegistry"/> registration in <see cref="CmdTagParameterUpdater.Execute(UIApplication, Document)"/>).
        /// Branches on the modified element's category: re-derives tag parameters for a piece tag
        /// ("Generic Annotations") or propagates Origin/Origin Sheet from a unit's placed tag view
        /// ("Curtain Panels").
        /// </summary>
        public void Execute(UpdaterData data)
        {
            Document doc = data.GetDocument();
            Application app = doc.Application;

            ElementCategoryFilter cfilter = new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallPanelTags);
            FilteredElementCollector col = new FilteredElementCollector(doc);
            col.WherePasses(cfilter);
            IList<Element> elementList = col.ToElements();

            foreach (ElementId id in data.GetModifiedElementIds())
            {
                try
                {
                    Element elem = doc.GetElement(id);
                    if (elem.Category.Name == "Generic Annotations")
                    {
                        // uOrigin/uOSheet: where the piece was originally tagged — a sheet number
                        // ("U-..." sheets) or a "<detailNumber><OriginLetter>" detail reference.
                        // uOLetter: the letter suffix used to build a detail-view style Origin.
                        // prefix/number: the piece mark components (joined below into "Piece").
                        // type: drives the Piece Description lookup key.
                        // detailNumber/sheet: the tag's owning viewport's detail number / sheet number.
                        // piece/description: the tag's current "Prefix-Number" and description text.
                        string uOrigin = "";
                        string uOSheet = "";
                        string uOLetter = "";
                        string prefix = "";
                        string number = "";
                        string type = "";
                        string detailNumber = "";
                        string sheet = "";
                        string piece = "";
                        string description = "";

                        View ownerview = doc.GetElement(elem.OwnerViewId) as View;
                        detailNumber = ownerview.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER).AsString();
                        sheet = ownerview.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER).AsString();

                        Parameter param_uOrigin = elem.LookupParameter("Origin");
                        if (param_uOrigin != null)
                        {
                            uOrigin = param_uOrigin.AsString();
                        }
                        Parameter param_uOSheet = elem.LookupParameter("Origin Sheet");
                        if (param_uOSheet != null)
                        {
                            uOSheet = param_uOSheet.AsString();
                        }
                        Parameter param_uOLetter = elem.LookupParameter("Origin Letter");
                        if (param_uOLetter != null)
                        {
                            uOLetter = param_uOLetter.AsString();
                        }
                        Parameter param_uPrefix = elem.LookupParameter("Prefix");
                        if (param_uPrefix != null)
                        {
                            prefix = param_uPrefix.AsString();
                        }
                        Parameter param_uNumber = elem.LookupParameter("Number");
                        if (param_uNumber != null)
                        {
                            number = param_uNumber.AsString();
                        }
                        Parameter param_uType = elem.LookupParameter("Type");
                        if (param_uType != null)
                        {
                            type = param_uType.AsValueString();
                        }
                        Parameter param_uPiece = elem.LookupParameter("Piece");
                        if (param_uPiece != null)
                        {
                            piece = param_uPiece.AsString();
                        }
                        Parameter param_uDesc = elem.LookupParameter("Piece Description");
                        if (param_uDesc != null)
                        {
                            description = param_uDesc.AsString();
                        }
                        // On a unit sheet ("U-..."): Origin/Origin Sheet both become the sheet number,
                        // Piece becomes "Prefix-Number", and Description is derived from Type (or
                        // Prefix, for standard piece types) via the shared piece-definition lookup.
                        if (sheet.Contains("U-"))
                        {
                            if (param_uOrigin != null && !string.IsNullOrWhiteSpace(sheet) && uOrigin != sheet)
                            {
                                param_uOrigin.Set(sheet);
                            }
                            if(param_uOSheet != null && !string.IsNullOrWhiteSpace(sheet) && uOSheet != sheet)
                            {
                                param_uOSheet.Set(sheet);
                            }
                            if (param_uPiece != null && !string.IsNullOrWhiteSpace(prefix) && !string.IsNullOrWhiteSpace(number) && piece != prefix + "-" + number)
                            {
                                param_uPiece.Set(prefix + "-" + number);
                            }
                            if (param_uDesc != null && !string.IsNullOrWhiteSpace(type))
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
                                string tempDesc = PieceDescriptions.PieceDescriptions.pieceDefinition(key);
                                if (description != tempDesc)
                                {
                                    param_uDesc.Set(tempDesc);
                                }
                            }
                        }
                        // On a non-unit (detail) view: Origin instead becomes "<detailNumber><OriginLetter>"
                        // (a detail reference rather than a sheet number), Origin Sheet still tracks the
                        // sheet the detail is placed on, and Piece/Description are cleared — they only
                        // apply to unit-sheet piece tags.
                        else
                        {
                            if (param_uOrigin != null && !string.IsNullOrWhiteSpace(detailNumber) && !string.IsNullOrWhiteSpace(uOLetter) && uOrigin != (detailNumber + uOLetter))
                            {
                                param_uOrigin.Set(detailNumber + uOLetter);
                            }
                            if(param_uOSheet != null && !string.IsNullOrWhiteSpace(sheet) && uOSheet != sheet)
                            {
                                param_uOSheet.Set(sheet);
                            }
                            if(param_uPiece != null && !string.IsNullOrWhiteSpace(piece))
                            {
                                param_uPiece.Set("");
                            }
                            if (param_uDesc != null && !string.IsNullOrWhiteSpace(description))
                            {
                                param_uDesc.Set("");
                            }
                        }
                    }
                    // A unit (Curtain Panel) changed — find its "Unit / Address"/"Unit Tag" annotation
                    // tag among the pre-collected CurtainWallPanelTags elements, and if that tag sits
                    // on an Elevation or Detail view, derive the unit's Origin ("<detailNumber><OriginLetter>")
                    // and Origin Sheet (the tag's viewport sheet number) from that view.
                    else if (elem.Category.Name == "Curtain Panels")
                    {
                        foreach (Element uEle in elementList)
                        {
                            try
                            {
                                if (uEle.Name.ToString() == "Unit / Address" || uEle.Name.ToString() == "Unit Tag")
                                {
                                    IndependentTag uTag = uEle as IndependentTag;

                                    LinkElementId linkelementID = uTag.GetTaggedElementIds().FirstOrDefault();
                                    if (linkelementID == null) { GMSRevitAddin.GmsLog.Warn("TagParameterUpdater: tag has no tagged element; skipped"); continue; }

                                    Element unit = doc.GetElement(linkelementID.HostElementId);

                                    if (unit.Id == elem.Id)
                                    {
                                        View uOwnerview = doc.GetElement(uEle.OwnerViewId) as View;
                                        if (uOwnerview.ViewType == ViewType.Elevation || uOwnerview.ViewType == ViewType.Detail)
                                        {
                                            string sheetname = uOwnerview.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER).AsString();
                                            string detailNumber = uOwnerview.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER).AsString();
                                            string uOLetter = "";
                                            string uOrigin = "";
                                            string uOSheet = "";

                                            Parameter param_OLetter = unit.LookupParameter("Origin Letter");
                                            if (param_OLetter != null)
                                            {
                                                uOLetter= param_OLetter.AsString();
                                            }
                                            Parameter param_Origin = unit.LookupParameter("Origin");
                                            if(param_Origin != null)
                                            {
                                                uOrigin= param_Origin.AsString();
                                                if (!string.IsNullOrWhiteSpace(uOLetter) && uOrigin != (detailNumber + uOLetter))
                                                {
                                                    param_Origin.Set(detailNumber + uOLetter);
                                                }
                                            }
                                            Parameter param_OSheet = unit.LookupParameter("Origin Sheet");
                                            if (param_OSheet != null)
                                            {
                                                uOSheet= param_OSheet.AsString();
                                                if(!string.IsNullOrWhiteSpace(sheetname) && uOSheet != sheetname)
                                                {
                                                    param_OSheet.Set(sheetname);
                                                }
                                            }
                                            break;
                                        }
                                    }
                                }
                            }
                            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("TagParameterUpdater", __ex); }
                        }
                    }
                }
                //catch (Exception e)
                catch
                {
                    //System.Windows.Forms.MessageBox.Show(e.Message, "OLU1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
            return "TagParameterUpdater";
        }
    }


    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CmdTagParameterUpdater : IExternalCommand
    {
        //public void addParam(Document doc, Application app, string pName, BuiltInParameterGroup pGroup, bool vis)
        public void addParam(Document doc, Application app, string pName, ForgeTypeId pGroup, bool vis)
        {
            using (Transaction tr = new Transaction(doc))
            {
                tr.Start("Create Origin Params");

                string oriFile = app.SharedParametersFilename;
                string tempFile = Path.GetTempFileName() + ".txt";
                using (File.Create(tempFile)) { }
                app.SharedParametersFilename = tempFile;

                var defOptions = new ExternalDefinitionCreationOptions(pName, SpecTypeId.String.Text)
                {
                    Visible = vis
                };
                ExternalDefinition def = app.OpenSharedParameterFile().Groups.Create("TemporaryDefinitionGroup").Definitions.Create(defOptions) as ExternalDefinition;

                app.SharedParametersFilename = oriFile;
                File.Delete(tempFile);

                Category cwPanel = doc.Settings.Categories.get_Item(BuiltInCategory.OST_CurtainWallPanels);
                CategorySet cats = app.Create.NewCategorySet();
                cats.Insert(cwPanel);
                Autodesk.Revit.DB.Binding binding = app.Create.NewTypeBinding(cats);
                binding = app.Create.NewInstanceBinding(cats);

                BindingMap map = (new UIApplication(app)).ActiveUIDocument.Document.ParameterBindings;
                map.Insert(def, binding, pGroup);

                tr.Commit();
            }
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            return Execute(commandData.Application, doc);
        }
        public Result Execute(UIApplication uiapp, Document doc)
        {
            Application app = uiapp.Application;

            bool originP = false;
            bool originLetterP = false;
            bool originSheetP = false;

            ElementCategoryFilter cfilter = new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallPanels);
            FilteredElementCollector col = new FilteredElementCollector(doc);
            col.WherePasses(cfilter);
            col.WhereElementIsNotElementType();
            IList<Element> elementList = col.ToElements();

            if (elementList.Any())
            {
                foreach (Element el in elementList)
                {
                    Parameter param_Origin = el.LookupParameter("Origin");
                    if (param_Origin != null)
                    {
                        originP = true;
                    }
                    Parameter param_OriginSheet = el.LookupParameter("Origin Sheet");
                    if (param_OriginSheet != null)
                    {
                        originSheetP = true;
                    }
                    Parameter param_OriginLetter = el.LookupParameter("Origin Letter");
                    if (param_OriginLetter != null)
                    {
                        originLetterP = true;
                    }

                    if (originP && originLetterP && originSheetP)
                    {
                        break;
                    }
                }

                //create Origin, Origin Letter and Origin Sheet params and bind to Curtainwall Panels
                //https://forums.autodesk.com/t5/revit-api-forum/create-project-parameter-not-shared-parameter/td-p/5150182
                if (!originP || !originLetterP || !originSheetP)
                {
                    if (doc.IsWorkshared && !doc.IsDetached && !doc.IsFamilyDocument)
                    {
                        try
                        {
                            if (!originP)
                            {
                                //addParam(doc, app, "Origin", BuiltInParameterGroup.PG_DATA, false);
                                addParam(doc, app, "Origin", GroupTypeId.Data, false);
                            }
                            if (!originSheetP)
                            {
                                //addParam(doc, app, "Origin Sheet", BuiltInParameterGroup.PG_DATA, false);
                                addParam(doc, app, "Origin Sheet", GroupTypeId.Data, false);
                            }
                            if (!originLetterP)
                            {
                                //addParam(doc, app, "Origin Letter", BuiltInParameterGroup.PG_CONSTRUCTION, true);
                                addParam(doc, app, "Origin Letter", GroupTypeId.Construction, true);
                            }
                        }
                        catch
                        {
                            System.Windows.Forms.MessageBox.Show("Error creating Origin Parameters", "OLU2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                    }
                }
            }

            ElementId paramID = ElementId.InvalidElementId;
            ElementId paramID1 = ElementId.InvalidElementId;
            ElementId testID = ElementId.InvalidElementId;
            ElementId paramPrefixId = ElementId.InvalidElementId;
            ElementId paramNumberId = ElementId.InvalidElementId;

            try
            {
                string targetName = "GAIT - Piece Tag";
                List<FamilyInstance> familyInstances = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().Where(x => x.Symbol.Family.Name.Equals(targetName)).ToList<FamilyInstance>();

                foreach (FamilyInstance fi in familyInstances)
                {
                    paramID = ElementId.InvalidElementId;
                    paramPrefixId = ElementId.InvalidElementId;
                    paramNumberId = ElementId.InvalidElementId;
                    Parameter param_OLetter = fi.LookupParameter("Origin Letter");
                    if(param_OLetter != null)
                    {
                        paramID = param_OLetter.Id;
                    }
                    Parameter param_Prefix = fi.LookupParameter("Prefix");
                    if(param_Prefix != null)
                    {
                        paramPrefixId= param_Prefix.Id;
                    }
                    Parameter param_Number = fi.LookupParameter("Number");
                    if(param_Number != null)
                    {
                        paramNumberId= param_Number.Id;
                    }
                    if (paramID != testID && paramPrefixId != testID && paramNumberId != testID)
                    {
                        break;
                    }
                }
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("TagParameterUpdater", __ex); }

            try
            {
                ElementCategoryFilter cfilter1 = new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallPanels);
                FilteredElementCollector col1 = new FilteredElementCollector(doc);
                col1.WherePasses(cfilter1);
                IList<Element> elementList1 = col1.ToElements();

                paramID1 = ElementId.InvalidElementId;

                foreach (Element el in elementList1)
                {
                    Parameter param_OLetterPanel = el.LookupParameter("Origin Letter");
                    if (param_OLetterPanel != null)
                    {
                        paramID1= param_OLetterPanel.Id;
                        break;
                    }
                }
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("TagParameterUpdater", __ex); }

            // Register updater to react to OLetter Parameter change
            try
            {
                ElementCategoryFilter f = new ElementCategoryFilter(BuiltInCategory.OST_GenericAnnotation);
                ElementCategoryFilter f1 = new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallPanels);

                TagParameterUpdater Oupdater = new TagParameterUpdater(app.ActiveAddInId);
                UpdaterRegistry.RegisterUpdater(Oupdater, true);
                UpdaterRegistry.AddTrigger(Oupdater.GetUpdaterId(), f, Element.GetChangeTypeParameter(paramID));
                UpdaterRegistry.AddTrigger(Oupdater.GetUpdaterId(), f1, Element.GetChangeTypeParameter(paramID1));
                UpdaterRegistry.AddTrigger(Oupdater.GetUpdaterId(), f, Element.GetChangeTypeParameter(paramPrefixId));
                UpdaterRegistry.AddTrigger(Oupdater.GetUpdaterId(), f, Element.GetChangeTypeParameter(paramNumberId));
                UpdaterRegistry.AddTrigger(Oupdater.GetUpdaterId(), f, Element.GetChangeTypeElementAddition());

                return Result.Succeeded;
            }
            catch (Exception e)
            {
                Autodesk.Revit.UI.TaskDialog.Show("OLU3 Error", e.Message);
                return Result.Failed;
            }
        }
    }

    public class RevitStartup : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            //application.ControlledApplication.DocumentOpened += ControlledApplication_DocumentOpened;  not needed as this is called from GMS_tools
            return Result.Succeeded;

        }

        public static void ControlledApplication_DocumentOpened(object sender, Autodesk.Revit.DB.Events.DocumentOpenedEventArgs e)
        {
            var command = new CmdTagParameterUpdater();
            command.Execute(new UIApplication(sender as Application), e.Document);
        }
    }
}