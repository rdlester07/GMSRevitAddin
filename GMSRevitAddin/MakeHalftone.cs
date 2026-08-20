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

namespace MakeHalftone
{
    /// <summary>
    /// Graphic-override command: sets the "Halftone" override on the selected (or picked) elements
    /// in the active view, recursing into Detail Group members so every member gets the override too.
    /// Skips annotation-ish categories (Text Notes, Dimensions, Generic Annotations, Detail Item Tags).
    /// Wired as a ribbon command (IExternalCommand) via its "MakeHalftone.MakeHalftone" class string.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class MakeHalftone : IExternalCommand
    {
        // Pending Detail Group elements still needing their members halftoned (drained in Execute).
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
                GMSRevitAddin.GmsUi.ShowError(e, "MH1 Error");
            }

            using (Transaction tr = new Transaction(doc))
            {
                tr.Start("NotByGMS");
                // Look up the "GMS - Dashed - Medium" line pattern purely to confirm it's loaded in
                // the document before proceeding (the pattern itself isn't applied by this command).
                FilteredElementCollector fec = new FilteredElementCollector(doc).OfClass(typeof(LinePatternElement));
                List<ElementId> lineIDs = fec.Cast<LinePatternElement>().Where(x => x.GetLinePattern().Name.Equals("GMS - Dashed - Medium")).Select(x => x.Id).ToList();
                if (lineIDs.Any())
                {
                    ElementId dashed = lineIDs[0];
                    foreach (ElementId eid in eids)
                    {
                        Element elem = doc.GetElement(eid);
                        ParameterSet ps = elem.Parameters;
                        bool detailGroup = false;
                        bool skipItem = false;

                        // Classify the element by its "Category" parameter: skip annotation-ish
                        // categories outright, defer Detail Groups to overrideDetailGroup, and
                        // halftone everything else directly.
                        foreach (Parameter p in ps)
                        {
                            if (p.Definition.Name == "Category")
                            {
                                string val = p.AsValueString();
                                if (val == "Text Notes" || val == "Dimensions" || val == "Generic Annotations" || val == "Detail Item Tags")
                                {
                                    detailGroup = false;
                                    skipItem = true;
                                    break;
                                }
                                else if (val == "Detail Groups")
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
                            // Read-modify-write the view's per-element graphic overrides, turning on Halftone.
                            OverrideGraphicSettings overrideGraphicSettings = doc.ActiveView.GetElementOverrides(eid);
                            overrideGraphicSettings = overrideGraphicSettings.SetHalftone(true);
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
                }
                else
                {
                    MessageBox.Show("Unable to determine LinePattern ID of \"GMS - Dashed - Medium\"", "MH2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                tr.Commit();
            }

            return Result.Succeeded;
        }

        /// <summary>
        /// Applies the Halftone override to every member of a Detail Group, skipping annotation-ish
        /// categories and queuing nested Detail Group members onto the static <c>detailGroups</c> list
        /// so the caller's drain loop picks them up.
        /// </summary>
        public static void overrideDetailGroup(Document doc, Element elem)
        {
            Group group = elem as Group;
            IList<ElementId> mIDs = group.GetMemberIds();
            foreach (ElementId mID in mIDs)
            {
                Element mElm = doc.GetElement(mID);
                ParameterSet mps = mElm.Parameters;
                bool skipItem = false;

                foreach (Parameter mp in mps)
                {
                    if (mp.Definition.Name == "Category")
                    {
                        string val = mp.AsValueString();
                        if (val == "Text Notes" || val == "Dimensions" || val == "Generic Annotations" || val == "Detail Item Tags")
                        {
                            skipItem = true;
                            break;
                        }
                        else if (val == "Detail Groups")
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
                    // Same read-modify-write pattern as the top-level loop in Execute.
                    OverrideGraphicSettings overrideGraphicSettings = doc.ActiveView.GetElementOverrides(mID);
                    overrideGraphicSettings = overrideGraphicSettings.SetHalftone(true);
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