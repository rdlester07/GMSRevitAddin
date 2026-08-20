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

namespace DimensionNotes
{

    /// <summary>
    /// Sets or clears the note text ("Below" text, and optionally a "not to scale" value override)
    /// on the currently selected dimension(s), via the <c>DimensionNoteForm</c> dialog. Supports
    /// both single dimensions and stringed (segmented) dimensions.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class DimNotes : IExternalCommand
    {
        // Set by DimensionNoteForm before it closes; read back here to apply to the selected dimensions.
        public static string NoteText = "";
        public static bool isNotToScale = false;
        public static string NotToScaleDimension = "";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            UIDocument uidoc = new UIDocument(doc);

            List<ElementId> eids = new List<ElementId>();

            using (Transaction tr = new Transaction(doc))
            {
                tr.Start("Dimension Note");

                try
                {
                    Selection selection = uidoc.Selection;
                    ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
                    long BICategoryID = (long)BuiltInCategory.OST_Dimensions;

                    if (selectedIds.Count == 0)
                    {
                        MessageBox.Show("No objects were selected.", "DN1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                    else
                    {
                        eids.Clear();
                        // Filter the selection down to just Dimension elements.
                        foreach (ElementId eid in selectedIds)
                        {
                            try
                            {
                                Element temp = doc.GetElement(eid);
                                if (temp.Category.Id.Value == BICategoryID)
                                {
                                    eids.Add(eid);
                                }
                            }
                            catch
                            {
                                //MessageBox.Show("Error " + eid);
                            }
                        }

                        if (eids.Count == 0)
                        {
                            MessageBox.Show("No dimensions were selected.", "DN2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                        }
                        else
                        {

                            NoteText = "";
                            isNotToScale = false;
                            NotToScaleDimension = "";

                            using (System.Windows.Forms.Form form = new DimensionNotesForm.DimensionNoteForm())
                            {
                                DialogResult dr = new DialogResult();
                                dr = form.ShowDialog();
                                if (dr == DialogResult.OK)
                                {
                                    // Apply the note text (and, for single dimensions, the "not to scale" value override)
                                    // set by the dialog to every selected dimension.
                                    foreach (ElementId dimID in eids)
                                    {
                                        Dimension dim = doc.GetElement(dimID) as Dimension;
                                        if (dim.Segments.Size == 0)
                                        {
                                            if (isNotToScale)
                                            {
                                                dim.ValueOverride = NotToScaleDimension;
                                            }

                                            dim.Below = NoteText;
                                        }
                                        else
                                        {
                                            if (isNotToScale)
                                            {
                                                MessageBox.Show("\"Not To Scale\" can only be applied to a single dimension, not a stringed dimension. Skipping...", "DN3 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            }
                                            else
                                            {
                                                foreach (DimensionSegment dimSeg in dim.Segments)
                                                {
                                                    dimSeg.Below = NoteText;
                                                }
                                            }
                                        }
                                    }
                                }
                                else if (dr == DialogResult.Ignore)
                                {
                                    // "Ignore" means clear all note/override text on the selected dimensions.
                                    foreach (ElementId dimID in eids)
                                    {
                                        Dimension dim = doc.GetElement(dimID) as Dimension;
                                        if (dim.Segments.Size == 0)
                                        {
                                            dim.ValueOverride = "";
                                            dim.Above = "";
                                            dim.Below = "";
                                            dim.Prefix = "";
                                            dim.Suffix = "";
                                        }
                                        else
                                        {
                                            foreach (DimensionSegment dimSeg in dim.Segments)
                                            {
                                                dimSeg.Above = "";
                                                dimSeg.Below = "";
                                                dimSeg.Prefix = "";
                                                dimSeg.Suffix = "";
                                            }
                                        }
                                    }
                                }
                            }
                        }
                    }
                    tr.Commit();
                }
                catch (Exception ex)
                {
                    GMSRevitAddin.GmsUi.ShowError(ex, "DN4 Error");
                }

                return Result.Succeeded;
            }
        }
    }
}