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
using View = Autodesk.Revit.DB.View;

namespace TagUnits
{
    /// <summary>
    /// Tag-placement command: tags selected curtain wall panels (units) with the "Unit Tag"/"Unit /
    /// Address" family in the active view, skipping panels already tagged. In floor plans the tag
    /// leader is offset from the panel's facing side (scaled by the view's drawing scale); in
    /// elevation/detail views the tag is placed at the panel's bounding-box centroid with no leader.
    /// Also sets that tag family as the document's default Curtain Wall Panel Tag type if it isn't
    /// already. Wired as a ribbon command (IExternalCommand) via its "TagUnits.TagUnits" class string.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class TagUnits : IExternalCommand
    {
        public static List<Element> detailGroups = new List<Element>();


        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            detailGroups.Clear();
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            UIDocument uidoc = new UIDocument(doc);

            List<ElementId> eids = new List<ElementId>();


            // Find the preferred curtain wall panel tag family type: prefer "Unit Tag", fall back to
            // "Unit / Address" if that's the only one loaded.
            FilteredElementCollector col1 = new FilteredElementCollector(doc);
            col1.OfCategory(BuiltInCategory.OST_CurtainWallPanelTags);
            col1.OfClass(typeof(FamilySymbol));
            IList<Element> elementList1 = col1.ToElements();
            ElementId tagID = ElementId.InvalidElementId;

            foreach(Element elm in elementList1)
            {
                if(elm.Name == "Unit Tag")
                {
                    tagID = elm.Id;
                    break;
                }
                else if(elm.Name == "Unit / Address")
                {
                    tagID = elm.Id;
                }
            }

            ElementId defaultID = doc.GetDefaultFamilyTypeId(Category.GetCategory(doc, BuiltInCategory.OST_CurtainWallPanelTags).Id);

            if (tagID == ElementId.InvalidElementId)
            {
                MessageBox.Show("Unable to locate correct unit tag symbol. Load correct GAIT Annotation.", "Warning", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            }
            else if (defaultID != tagID)
            {
                // Make the resolved tag family/type the document default so subsequent manual tagging
                // (outside this command) also uses it.
                using (Transaction tr = new Transaction(doc))
                {
                    tr.Start("Change Default Panel Tag");
                    doc.SetDefaultFamilyTypeId(Category.GetCategory(doc, BuiltInCategory.OST_CurtainWallPanelTags).Id, tagID);
                    tr.Commit();
                }
            }

            try
            {
                Selection selection = uidoc.Selection;
                ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
                long BIcategoryID = (long)BuiltInCategory.OST_CurtainWallPanels;


                if (selectedIds.Count == 0)
                {
                    MessageBox.Show("No curtain wall panels were selected.", "TU1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    eids.Clear();
                    View actView = doc.ActiveView;
                    ViewType vType = actView.ViewType;
                    List<ElementId> existingTaggedPanels = new List<ElementId>();

                    // Gather every panel tag already placed in this view so already-tagged panels are
                    // excluded from re-tagging below.
                    ElementCategoryFilter cfilter = new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallPanelTags);
                    FilteredElementCollector col = new FilteredElementCollector(doc);
                    col.WherePasses(cfilter);
                    IList<Element> elementList = col.ToElements();


                    using (Transaction tr = new Transaction(doc))
                    {
                        tr.Start("Collect Existing Tags");
                        existingTaggedPanels.Clear();
                        foreach (Element uEle in elementList)
                        {
                            try
                            {
                                if (uEle.OwnerViewId == actView.Id)
                                {
                                    IndependentTag uTag = uEle as IndependentTag;

                                    //LinkElementId linkelementID = uTag.TaggedElementId;
                                    LinkElementId linkelementID = uTag.GetTaggedElementIds().FirstOrDefault();
                                    if (linkelementID == null) { GMSRevitAddin.GmsLog.Warn("TagUnits: tag has no tagged element; skipped"); continue; }
                                    existingTaggedPanels.Add(linkelementID.HostElementId);
                                }

                            }
                            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("TagUnits", __ex); }
                        }
                        tr.Commit();
                    }

                    if(vType == ViewType.FloorPlan)
                    {
                        // Leader offset scales with the view's drawing scale so the tag leader reads
                        // consistently on paper regardless of zoom/scale (1.5" leader + 0.75" margin,
                        // converted from paper inches to model feet via the view scale).
                        int scale = actView.Scale; // 1:x
                        double leaderlength = 1.5; //inches
                        double offset = (scale * (leaderlength + 0.75)) / 12;

                        foreach (ElementId ei in selectedIds)
                        {
                            try
                            {
                                Element el = uidoc.Document.GetElement(ei);
                                long categoryID = el.Category.Id.Value;
                                if (categoryID == BIcategoryID && !existingTaggedPanels.Contains(ei))
                                {
                                    eids.Add(ei);
                                }
                            }
                            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("TagUnits", __ex); }
                        }

                        if (eids.Count > 0)
                        {
                            using (Transaction tr = new Transaction(doc))
                            {
                                tr.Start("Tag Units");

                                foreach (ElementId elid in eids)
                                {
                                    Element unit = uidoc.Document.GetElement(elid);
                                    BoundingBoxXYZ bb = unit.get_BoundingBox(actView);
                                    XYZ min = bb.Min; //lower left rear of box
                                    XYZ max = bb.Max; //upper right front of box

                                    FamilyInstance fm = unit as FamilyInstance;
                                    XYZ facing = fm.FacingOrientation;
                                    bool flipped = fm.FacingFlipped;

                                    XYZ midpoint = new XYZ((max.X + min.X) / 2, (max.Y + min.Y) / 2, 1);

                                    Reference refe = new Reference(unit);

                                    // Choose the leader direction from the panel's orientation: for a
                                    // horizontal panel, offset vertically (based on facing/flip); for a
                                    // vertical panel, offset horizontally. Keeps the leader pointing
                                    // away from the panel's face rather than through it.
                                    if ((max.X - min.X) > (max.Y - min.Y)) //horizontal
                                    {
                                        if(!flipped && facing.Y >= 0 || flipped && facing.Y < 0) //facing south
                                        {
                                            IndependentTag n = IndependentTag.Create(doc, actView.Id, refe, true, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Vertical, midpoint + new XYZ(0, -1, 0));
                                        }
                                        else //facing north
                                        {
                                            IndependentTag n = IndependentTag.Create(doc, actView.Id, refe, true, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Vertical, midpoint + new XYZ(0, 1, 0));
                                        }
                                    }
                                    else //vertical
                                    {
                                        if(!flipped && facing.X >= 0 || flipped && facing.X < 0) //facing west
                                        {
                                            IndependentTag n = IndependentTag.Create(doc, actView.Id, refe, true, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, midpoint + new XYZ(-1, 0, 0));
                                        }
                                        else //facing east
                                        {
                                            IndependentTag n = IndependentTag.Create(doc, actView.Id, refe, true, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Horizontal, midpoint + new XYZ(1, 0, 0));
                                        }
                                    }
                                    
                                }
                                tr.Commit();
                            }

                        }
                    }
                    else if(vType == ViewType.Elevation || vType == ViewType.Detail)
                    {
                        foreach (ElementId ei in selectedIds)
                        {
                            try
                            {
                                Element el = uidoc.Document.GetElement(ei);
                                long categoryID = el.Category.Id.Value;
                                if (categoryID == BIcategoryID && !existingTaggedPanels.Contains(ei))
                                {
                                    eids.Add(ei);
                                }
                            }
                            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("TagUnits", __ex); }
                        }

                        if (eids.Count > 0)
                        {
                            using (Transaction tr = new Transaction(doc))
                            {
                                tr.Start("Tag Units");

                                foreach (ElementId elid in eids)
                                {
                                    Element unit = uidoc.Document.GetElement(elid);
                                    BoundingBoxXYZ bb = unit.get_BoundingBox(actView);
                                    XYZ min = bb.Min; //lower left rear of box
                                    XYZ max = bb.Max; //upper right front of box

                                    FamilyInstance fm = unit as FamilyInstance;
                                    XYZ facing = fm.FacingOrientation;
                                    bool flipped = fm.FacingFlipped;

                                    XYZ midpoint = new XYZ((max.X + min.X) / 2, (max.Y + min.Y) / 2, (max.Z + min.Z) / 2);

                                    Reference refe = new Reference(unit);
                                    IndependentTag n = IndependentTag.Create(doc, actView.Id, refe, false, TagMode.TM_ADDBY_CATEGORY, TagOrientation.Vertical, midpoint);
                                }
                                tr.Commit();
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("Active view is not a floor plan or elevation. Tagging cancled.", "TU2 Error", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
            }
            catch (Exception e)
            {
                GMSRevitAddin.GmsUi.ShowError(e, "TU3 Error");
            }

            uidoc.Selection.SetElementIds(new List<ElementId>());
            return Result.Succeeded;
        }
    }
}
