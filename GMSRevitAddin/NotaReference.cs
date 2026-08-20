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

namespace NotaReference
{
    /// <summary>
    /// Keeps reference-plane "Is Reference" settings in sync with their subcategory inside family
    /// documents: planes on "Control Line (Not a Reference)" are forced to Not a Reference, planes on
    /// "Work Points - Module" to Weak Reference, and planes on "Work Points - Unit" to Strong Reference.
    /// Event-driven — <see cref="ControlledApplication_DocumentChanged"/> is registered application-wide
    /// from <c>GMS_tools.OnStartup</c> and arms a one-shot Idling handler (<see cref="OnIdling"/>) whenever
    /// a reference plane is added/modified in a family document, since parameter writes can't happen
    /// from inside the DocumentChanged callback itself. <see cref="enabled"/> gates the whole feature and
    /// is persisted to <c>Properties.Settings.Default.UnitLines</c> via <see cref="updatedbool"/>.
    /// </summary>
    public class RevitStartup : IExternalApplication
    {
        public static Document doc = null;
        public static UIApplication uiapp = null;

        public static bool enabled = true;
        /// <summary>Toggles the feature on/off and persists the choice to <c>Properties.Settings.Default.UnitLines</c>.</summary>
        public static void updatedbool(bool en)
        {
            enabled = en;
            GMSRevitAddin.Properties.Settings.Default.UnitLines = RevitStartup.enabled;
            GMSRevitAddin.Properties.Settings.Default.Save();
        }

        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            //application.ControlledApplication.DocumentChanged += ControlledApplication_DocumentChanged;  NOT NEEDED AS WE START THE EVENT LISTENER IN GMS_tools.cs

            return Result.Succeeded;
        }

        /// <summary>
        /// Fires on every document change. When enabled and a reference plane was added or modified
        /// in a family document, arms the one-shot Idling handler (<see cref="OnIdling"/>) that applies
        /// the subcategory-to-reference-type fixups (Revit model edits can't be made directly from
        /// inside this event).
        /// </summary>
        public static void ControlledApplication_DocumentChanged(object sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
        {
            if (enabled)
            {
                Document docu = e.GetDocument();
                doc = docu;
                UIApplication uiappl = new UIApplication(sender as Autodesk.Revit.ApplicationServices.Application);
                uiapp = uiappl;
                ElementClassFilter filter = new ElementClassFilter(typeof(ReferencePlane));
                ICollection<ElementId> modElems = e.GetModifiedElementIds(filter);
                ICollection<ElementId> addElems = e.GetAddedElementIds(filter);
                if (modElems.Any() || addElems.Any())
                {
                    if (docu.IsFamilyDocument)
                    {
                        // One-shot: only family documents can have this issue, and we defer the actual
                        // parameter writes to the next Idling tick.
                        uiappl.Idling += new EventHandler<IdlingEventArgs>(OnIdling);
                    }
                }
            }
        }

        /// <summary>
        /// Runs once per triggering document change: unsubscribes itself, then walks every reference
        /// plane in the document and sets its "Is Reference" value to match its subcategory (Not a
        /// Reference / Weak Reference / Strong Reference), each in its own transaction.
        /// </summary>
        public static void OnIdling(object sender, IdlingEventArgs e)
        {
            // One-shot: detach immediately so this only runs once per triggering change.
            uiapp.Idling -= OnIdling;

            if (!doc.IsReadOnly)
            {
                FilteredElementCollector fec = new FilteredElementCollector(doc).OfClass(typeof(ReferencePlane));
                List<Element> refPlanes = (List<Element>)fec.ToElements().ToList();

                foreach (ReferencePlane refplane in refPlanes)
                {
                    Parameter isRef = refplane.get_Parameter(BuiltInParameter.ELEM_REFERENCE_NAME);
                    Parameter subCat = refplane.get_Parameter(BuiltInParameter.CLINE_SUBCATEGORY);

                    if (subCat.AsValueString() == "Control Line (Not a Reference)" && isRef.AsInteger() != 12 && !isRef.IsReadOnly)
                    {
                        using (Transaction tr = new Transaction(doc))
                        {
                            try
                            {
                                tr.Start("Update to Not a Reference");
                                isRef.Set(12);
                                tr.Commit();
                            }
                            catch (Exception exc)
                            {
                                GMSRevitAddin.GmsUi.ShowError(exc, "NAR1 Error");
                            }
                        }
                    }
                    else if (subCat.AsValueString() == "Work Points - Module" && isRef.AsInteger() != 14 && !isRef.IsReadOnly)
                    {
                        using (Transaction tr = new Transaction(doc))
                        {
                            try
                            {
                                tr.Start("Update to Weak Reference");
                                isRef.Set(14);
                                tr.Commit();
                            }
                            catch (Exception exc)
                            {
                                GMSRevitAddin.GmsUi.ShowError(exc, "NAR2 Error");
                            }
                        }
                    }
                    else if (subCat.AsValueString() == "Work Points - Unit" && isRef.AsInteger() != 13 && !isRef.IsReadOnly)
                    {
                        using (Transaction tr = new Transaction(doc))
                        {
                            try
                            {
                                tr.Start("Update to Strong Reference");
                                isRef.Set(13);
                                tr.Commit();
                            }
                            catch (Exception exc)
                            {
                                GMSRevitAddin.GmsUi.ShowError(exc, "NAR3 Error");
                            }
                        }
                    }
                }
            }
        }
    }
}