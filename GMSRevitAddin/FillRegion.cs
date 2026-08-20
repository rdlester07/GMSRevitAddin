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

namespace FillRegion
{
    /// <summary>
    /// Diagnostic/dev command for a drafting view: lets the user pick the elements forming a fill
    /// region's perimeter and a reference point, then reports (via message boxes) the picked
    /// elements plus any elements whose bounding box intersects a short probe line drawn at that
    /// point — used to inspect what elements a fill region's perimeter picker would see there.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class FillRegion : IExternalCommand
    {

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            UIDocument uidoc = new UIDocument(doc);

            // Only runs in a drafting view.
            if (doc.ActiveView.ViewType == ViewType.DraftingView)
            {
                using (Transaction tr = new Transaction(doc))
                {
                    tr.Start("FillRegion");
                    string text = "";

                    // Let the user pick the elements that define the fill region's perimeter; list them.
                    IList<Reference> objs = uidoc.Selection.PickObjects(ObjectType.Element, "Select elements that create perimeter of filled region.");
                    foreach(Reference r in objs)
                    {
                        Element ee = doc.GetElement(r.ElementId);
                        text += ee.Name + " " + ee.Id + System.Environment.NewLine;
                    }
                    text += System.Environment.NewLine;
                    text += System.Environment.NewLine;

                    // Pick a point and draw a short (10-unit) vertical probe detail line from it.
                    XYZ point = uiapp.ActiveUIDocument.Selection.PickPoint(ObjectSnapTypes.None);
                    MessageBox.Show(point.X + "," + point.Y + "," + point.Z);
                    XYZ point2 = new XYZ(point.X, point.Y - 10, point.Z);
                    Line refLine = Line.CreateBound(point, point2);
                    DetailCurve dc = doc.Create.NewDetailCurve(doc.ActiveView, refLine);

                    // Find every element (excluding the probe line itself) whose bounding box intersects
                    // the probe line's bounding box, and report them.
                    BoundingBoxXYZ bb = dc.get_BoundingBox(doc.ActiveView);
                    Outline outline = new Outline(bb.Min, bb.Max);
                    BoundingBoxIntersectsFilter bbfilter = new BoundingBoxIntersectsFilter(outline);
                    FilteredElementCollector fec = new FilteredElementCollector(doc, doc.ActiveView.Id);
                    ICollection<ElementId> excludes = new List<ElementId>();
                    excludes.Add(dc.Id);
                    fec.Excluding(excludes).WherePasses(bbfilter);

                    foreach (Element el in fec)
                    {
                        text += el.Name + " " + el.Id + System.Environment.NewLine;
                    }
                    MessageBox.Show(text);


                    tr.Commit();
                }
            }
            return Result.Succeeded;
        }

    }
}
