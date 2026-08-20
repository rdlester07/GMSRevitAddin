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

namespace ResetOverrides
{
    /// <summary>
    /// Graphic-override command that clears all view-specific graphic overrides (halftone, line
    /// patterns, etc.) on the selected (or picked) elements in the active view, by replacing them with
    /// a fresh default <see cref="OverrideGraphicSettings"/>. Recurses into Detail Group members.
    /// Wired as a ribbon command (IExternalCommand) via its "ResetOverrides.ResetOverrides" class string.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ResetOverrides : IExternalCommand
    {
        // Pending Detail Group elements still needing their members reset (drained in Execute).
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

            try
            {
                Selection selection = uidoc.Selection;
                ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();

                if (selectedIds.Count == 0)
                {
                    eids.Clear();
                    IList<Reference> pickedElements = uidoc.Selection.PickObjects(ObjectType.Element);
                    eids = (from Reference r in pickedElements select r.ElementId).ToList();
                }
                else
                {
                    eids.Clear();
                    foreach (ElementId ei in selectedIds)
                    {
                        eids.Add(ei);
                    }
                }
            }
            catch (Exception e)
            {
                GMSRevitAddin.GmsUi.ShowError(e, "RO1 Error");
            }

            using (Transaction tr = new Transaction(doc))
            {
                tr.Start("NotByGMS");
                foreach (ElementId eid in eids)
                {
                    Element elem = doc.GetElement(eid);
                    ParameterSet ps = elem.Parameters;
                    bool detailGroup = false;
                    bool skipItem = false;

                    // Classify the element by its "Category" parameter: Detail Groups are deferred
                    // to overrideDetailGroup, everything else is reset directly.
                    foreach (Parameter p in ps)
                    {
                        if (p.Definition.Name == "Category")
                        {
                            if (p.AsValueString() == "Detail Groups")
                            {
                                detailGroup = true;
                                skipItem = true;
                                break;
                            }
                            else
                            {
                                detailGroup = false;
                                skipItem = false;
                                break;
                            }
                        }
                    }

                    if (detailGroup)
                    {
                        overrideDetailGroup(doc, elem);
                    }
                    else if (!skipItem)
                    {
                        // A default-constructed OverrideGraphicSettings has no overrides set, so
                        // assigning it wipes out any halftone/line-pattern/etc. overrides on the element.
                        OverrideGraphicSettings overrideGraphicSettings = new OverrideGraphicSettings();
                        doc.ActiveView.SetElementOverrides(eid, overrideGraphicSettings);
                    }
                }

                // Drain any Detail Groups queued by overrideDetailGroup (nested groups add themselves
                // back to this list, so the loop keeps going until all nesting levels are processed).
                while (detailGroups.Any())
                {
                    Element el = detailGroups[0];
                    overrideDetailGroup(doc, el);
                }
                tr.Commit();
            }

            return Result.Succeeded;
        }

        /// <summary>
        /// Clears the graphic overrides on every member of a Detail Group, queuing nested Detail Group
        /// members onto the static <c>detailGroups</c> list so the caller's drain loop picks them up.
        /// </summary>
        public static void overrideDetailGroup(Document doc, Element elem)
        {
            Group group = elem as Group;
            IList<ElementId> mIDs = group.GetMemberIds();
            bool skipItem = false;
            foreach (ElementId mID in mIDs)
            {
                Element mElm = doc.GetElement(mID);
                ParameterSet mps = mElm.Parameters;
                foreach (Parameter mp in mps)
                {
                    if (mp.Definition.Name == "Category")
                    {
                        string val = mp.AsValueString();
                        if (val == "Detail Groups")
                        {
                            detailGroups.Add(mElm);
                            skipItem = true;
                            break;
                        }
                        else
                        {
                            skipItem = false;
                            break;
                        }
                    }
                }

                if (!skipItem)
                {
                    // Same reset-to-default pattern as the top-level loop in Execute.
                    OverrideGraphicSettings overrideGraphicSettings = new OverrideGraphicSettings();
                    doc.ActiveView.SetElementOverrides(mID, overrideGraphicSettings);
                }
            }

            if (detailGroups.Contains(elem))
            {
                detailGroups.Remove(elem);
            }
        }
    }
}