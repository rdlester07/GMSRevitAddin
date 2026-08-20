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

namespace UnitLevelNumber
{

    /// <summary>
    /// "Unit Drawing Tools" ribbon command that sets the "Unit - Level" parameter on the selected
    /// curtain wall unit(s). Unlike UnitBuildNumber/UnitBunkNumber this does not pre-filter by
    /// "Unit - Mark Number" — it operates on every selected curtain wall panel directly. Prompts for
    /// a level number via <c>BuildBunkSmallForm</c>, then writes it into "Unit - Level" on each
    /// element inside a single transaction, aborting the loop if any element lacks the parameter.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UnitLevelNumber : IExternalCommand
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

            try
            {
                Selection selection = uidoc.Selection;
                ICollection<ElementId> selectedIds = uidoc.Selection.GetElementIds();
                long BIcategoryID = (long)BuiltInCategory.OST_CurtainWallPanels;


                if (selectedIds.Count == 0)
                {
                    MessageBox.Show("No curtain wall units were selected.", "ULV1 Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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
                        catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("UnitLevelNumber", __ex); }
                    }


                    if (eids.Count > 0)
                    {
                        number = "";
                        // Prompt for the level number to apply to all selected curtain wall panels.
                        using (System.Windows.Forms.Form form = new BuildBunkSmallForm.BuildBunkSmallForm(eids.Count(), "Level"))
                        {
                            var result = form.ShowDialog();
                            if (result == DialogResult.OK)
                            {
                                // Write the entered level number into "Unit - Level" on each panel.
                                using (Transaction tr = new Transaction(doc, "Update Unit Level"))
                                {
                                    tr.Start();
                                    foreach (ElementId elemid in eids)
                                    {
                                        bool foundParam = false;
                                        Element elem = doc.GetElement(elemid);
                                        ParameterSet pSet = elem.Parameters;
                                        foreach (Parameter para in pSet)
                                        {
                                            if (para.Definition.Name == "Unit - Level")
                                            {
                                                para.Set(number);
                                                foundParam = true;
                                                break;
                                            }
                                        }
                                        if (!foundParam)
                                        {
                                            // Missing param on this element — stop rather than leave a
                                            // partially-updated set of units.
                                            MessageBox.Show("The \"Unit - Level\" parameter was not found for the selected curtain wall unit(s). Operation has been stopped.", "ULV2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            break;
                                        }
                                    }
                                    tr.Commit();
                                }
                            }
                        }
                    }
                    else
                    {
                        MessageBox.Show("No curtain wall units were selected.", "ULV3 Error", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    }
                }
            }
            catch (Exception e)
            {
                GMSRevitAddin.GmsUi.ShowError(e, "ULN4 Error");
            }

            // Clear the selection so the ribbon command leaves a clean state behind.
            uidoc.Selection.SetElementIds(new List<ElementId>());
            return Result.Succeeded;
        }
    }
}