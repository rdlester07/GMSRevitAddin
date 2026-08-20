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

namespace UnitBunkNumber
{

    /// <summary>
    /// "Unit Drawing Tools" ribbon command that sets the "Unit - Bunk" parameter on the selected
    /// curtain wall unit(s). Filters the current selection down to curtain wall panels that already
    /// have a "Unit - Mark Number", prompts for a bunk number via <c>BuildBunkSmallForm</c>, then
    /// writes that value into "Unit - Bunk" on every filtered element in a single transaction.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UnitBunkNumber : IExternalCommand
    {
        // Value entered in the BuildBunkSmallForm dialog; read back after ShowDialog() returns.
        public static string number = "";

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            UIDocument uidoc = new UIDocument(doc);

            List<ElementId> eids = new List<ElementId>();
            List<ElementId> filteredEids = new List<ElementId>();

            try
            {
                Selection selection = uidoc.Selection;
                ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
                long BIcategoryID = (long)BuiltInCategory.OST_CurtainWallPanels;


                if (selectedIds.Count == 0)
                {
                    MessageBox.Show("No curtain wall units were selected.", "UBkN1 Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
                else
                {
                    eids.Clear();
                    // Keep only curtain wall panels from the selection; anything else is ignored.
                    foreach (ElementId ei in selectedIds)
                    {
                        try
                        {
                            Element el = uidoc.Document.GetElement(ei);
                            long categoryID = el.Category.Id.Value;
                            if (categoryID == BIcategoryID)
                            {
                                eids.Add(ei);
                            }
                        }
                        catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UnitBunkNumber", __ex); }
                    }


                    if (eids.Count > 0)
                    {
                        filteredEids.Clear();

                        // Further filter to panels that already carry a "Unit - Mark Number" —
                        // i.e. panels that are actually recognized units, not stray curtain panels.
                        foreach (ElementId elid in eids)
                        {
                            string unitNumber = "";

                            Element el = doc.GetElement(elid);
                            ParameterSet paramSet = el.Parameters;
                            foreach (Parameter p in paramSet)
                            {
                                if (p.Definition.Name == "Unit - Mark Number")
                                {
                                    unitNumber = p.AsString();
                                    break;
                                }
                            }

                            if (!string.IsNullOrWhiteSpace(unitNumber))
                            {
                                filteredEids.Add(elid);
                            }
                        }

                        if (filteredEids.Any())
                        {
                            number = "";
                            // Prompt for the bunk number to apply to all filtered units.
                            using (System.Windows.Forms.Form form = new BuildBunkSmallForm.BuildBunkSmallForm(filteredEids.Count(), "Bunk"))
                            {
                                var result = form.ShowDialog();
                                if (result == DialogResult.OK)
                                {
                                    // Write the entered bunk number into "Unit - Bunk" on every unit.
                                    using (Transaction tr = new Transaction(doc, "Update Unit Bunk"))
                                    {
                                        tr.Start();
                                        foreach (ElementId elemid in filteredEids)
                                        {
                                            Element elem = doc.GetElement(elemid);
                                            ParameterSet pSet = elem.Parameters;
                                            foreach (Parameter para in pSet)
                                            {
                                                if (para.Definition.Name == "Unit - Bunk")
                                                {
                                                    para.Set(number);
                                                    break;
                                                }
                                            }
                                        }
                                        tr.Commit();
                                    }
                                }
                            }
                        }
                        else
                        {
                            MessageBox.Show("No curtain wall units were selected.", "UBkN2 Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                        }
                    }
                    else
                    {
                        MessageBox.Show("No curtain wall units were selected.", "UBkN3 Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception e)
            {
                GMSRevitAddin.GmsUi.ShowError(e, "UBkN4 Error");
            }

            // Clear the selection so the ribbon command leaves a clean state behind.
            uidoc.Selection.SetElementIds(new List<ElementId>());
            return Result.Succeeded;
        }
    }
}