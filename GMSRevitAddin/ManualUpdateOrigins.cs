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
using System.Windows;
using System.Linq.Expressions;
using Application = Autodesk.Revit.ApplicationServices.Application;
using Microsoft.VisualBasic.ApplicationServices;
using System.Windows.Forms;
using MessageBox = System.Windows.Forms.MessageBox;
using View = Autodesk.Revit.DB.View;
using System.Collections;

namespace ManualUpdateOrigins
{
    /// <summary>Small LINQ helpers used to batch element-checkout calls.</summary>
    public static class Extensions
    {
        /// <summary>Splits a list into consecutive chunks of at most <paramref name="chunkSize"/> items.
        /// Used here to batch <c>WorksharingUtils.CheckoutElements</c> calls instead of checking out
        /// every tag/unit-tag element in one call.</summary>
        public static List<List<T>> partition<T>(this List<T> values, int chunkSize)
        {
            return values.Select((x, i) => new { Index = i, Value = x}).GroupBy(x => x.Index / chunkSize).Select(x => x.Select(v => v.Value).ToList()).ToList();
        }
    }

    /// <summary>
    /// The manual/on-demand "Update Origins" command. Doc-wide equivalent of
    /// <see cref="UpdateOrigins.updateTags.onViewChange"/> (the automatic, event-driven counterpart that
    /// re-syncs origin data only for the sheet just activated): this command re-syncs every
    /// "GAIT - Piece Tag" family instance and every curtain-wall-panel unit tag in the whole model in one
    /// pass, regardless of which sheet is active. Run from the ribbon when a bulk/manual re-sync is
    /// needed (e.g. after sheet renumbering) rather than relying on view-activation to catch each sheet.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class ManualUpdate : IExternalCommand
    {
        /// <summary>
        /// Entry point for the "Update Origins" ribbon command. First verifies every candidate tag
        /// element is checked out/editable (aborting with a report of the blocking users if not), then
        /// in one transaction re-derives each "GAIT - Piece Tag" instance's Origin/Origin Sheet/Piece/
        /// Piece Description from its owning viewport's sheet number + detail number, and in a second
        /// transaction does the equivalent origin sync for "Unit / Address"/"Unit Tag" curtain-wall-panel
        /// tags against the unit element they tag.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);
            bool error = false;
            bool foundNotEditible = false;
            string user = "";
            int fiCount = 0;
            int utCount = 0;
            List<string> users = new List<string>();
            string targetName = "GAIT - Piece Tag";

            List<FamilyInstance> familyInstances = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().Where(x => x.Symbol.Family.Name.Equals(targetName)).ToList<FamilyInstance>();
            ElementCategoryFilter _cfilter = new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallPanelTags);
            FilteredElementCollector _col = new FilteredElementCollector(doc);
            _col.WherePasses(_cfilter);
            IList<Element> _elementList = _col.ToElements();
            using (ProgressForm.ProgressForm pgf1 = new ProgressForm.ProgressForm("Update Origins", null, (familyInstances.Count() + _elementList.Count())/100 + 1, string.Empty))
            {
                pgf1.IncrementWithText("Gathering required parameter IDs.");

                // Combine piece-tag family instances and curtain-wall-panel unit tags into one list of
                // IDs so checkout status can be verified for all of them before anything is edited.
                List<ElementId> tagIDs = new List<ElementId>();
                fiCount = familyInstances.Count();
                foreach (Element el in familyInstances)
                {
                    tagIDs.Add(el.Id);
                }
                utCount = _elementList.Count();
                foreach (Element el in _elementList)
                {
                    tagIDs.Add(el.Id);
                }

                // Checkout is requested in batches of 100 (partition) rather than all at once or one at a
                // time, to keep each CheckoutElements call reasonably sized for large models.
                List<ElementId> OKtoEdit = new List<ElementId>();
                List<List<ElementId>> partitions = tagIDs.partition(100);
                foreach (List<ElementId> partition in partitions)
                {
                    pgf1.IncrementWithText("Checking element status.");
                    OKtoEdit.AddRange(WorksharingUtils.CheckoutElements(doc, partition));
                }

                // Any element not successfully checked out is owned by another user on the central
                // model; collect who so the cancellation message can name them.
                foreach (ElementId eid in tagIDs)
                {
                    if (!OKtoEdit.Contains(eid))
                    {
                        foundNotEditible = true;
                        WorksharingUtils.GetCheckoutStatus(doc, eid, out user);
                        if (!users.Contains(user))
                        {
                            users.Add(user);
                        }
                    }
                }
            }
            if (foundNotEditible)
            {
                if (!users.Any())
                {
                    MessageBox.Show("Elements were found that are checked out from the central model by other users. The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine + "Process canceled.", "ST1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                else
                {
                    string userText = string.Empty;
                    foreach (string u in users)
                    {
                        userText += u + " ";
                    }
                    MessageBox.Show("Elements were found that are checked out from the central model by the following user(s):" + System.Environment.NewLine + userText.Trim() +
                        System.Environment.NewLine + "The other user(s) must resync to relinquish these elements before you can use this command." + System.Environment.NewLine +
                        "Process canceled.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                return Result.Cancelled;
            }
            else
            {
                int increment = 0;
                using (ProgressForm.ProgressForm pgf = new ProgressForm.ProgressForm("Update Origins", null, 4 + familyInstances.Count(), string.Empty))
                {
                    pgf.IncrementWithText("Gathering required parameter IDs.");
                    using (Transaction tr = new Transaction(doc))
                    {
                        tr.Start("Origin Update");

                        foreach (FamilyInstance fi in familyInstances)
                        {
                            increment++;
                            pgf.IncrementWithText("Processing tag " + increment.ToString() + " of " + fiCount.ToString());
                            View ownerview = doc.GetElement(fi.OwnerViewId) as View;
                            string sheetname = ownerview.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER).AsString();
                            string detailNumber = ownerview.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER).AsString();
                            bool isUnitDrawing = false;
                            if (!string.IsNullOrWhiteSpace(sheetname) && !string.IsNullOrWhiteSpace(detailNumber))
                            {
                                if (sheetname.Contains("U-"))
                                {
                                    isUnitDrawing = true;
                                }

                                string OLetter = "";
                                string prefix = "";
                                string number = "";
                                string type = "";

                                Parameter Param_OSheet = fi.LookupParameter("Origin Sheet");
                                if (Param_OSheet != null)
                                {
                                    string _oSheet = Param_OSheet.AsString();
                                    if (_oSheet != sheetname)
                                    {
                                        try
                                        {
                                            Param_OSheet.Set(sheetname);
                                        }
                                        catch
                                        {
                                            error = true;
                                            pgf.Close();
                                            MessageBox.Show("Error setting Sheet Name from Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            break;
                                        }
                                    }
                                }
                                else
                                {
                                    error = true;
                                    pgf.Close();
                                    MessageBox.Show("Error finding Origin Sheet parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    break;
                                }

                                if (!isUnitDrawing)
                                {
                                    Parameter Param_oLetter = fi.LookupParameter("Origin Letter");
                                    if (Param_oLetter != null)
                                    {
                                        OLetter = Param_oLetter.AsString();
                                    }
                                    else
                                    {
                                        error = true;
                                        pgf.Close();
                                        MessageBox.Show("Error finding Origin Letter parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO3 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        break;
                                    }
                                }

                                Parameter Param_Origin = fi.LookupParameter("Origin");
                                if (Param_Origin != null)
                                {
                                    string _origin = Param_Origin.AsString();
                                    try
                                    {
                                        if (isUnitDrawing)
                                        {
                                            if (_origin != null && _origin != sheetname)
                                            {
                                                Param_Origin.Set(sheetname);
                                            }
                                        }
                                        else
                                        {
                                            Param_Origin.Set(detailNumber + OLetter);
                                        }
                                    }
                                    catch
                                    {
                                        error = true;
                                        pgf.Close();
                                        MessageBox.Show("Error setting Origin from Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO4 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        break;

                                    }
                                }
                                else
                                {
                                    error = true;
                                    pgf.Close();
                                    MessageBox.Show("Error finding Origin parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO5 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    break;
                                }

                                if (!error)
                                {
                                    Parameter Param_Prefix = fi.LookupParameter("Prefix");
                                    if (Param_Prefix != null)
                                    {
                                        prefix = Param_Prefix.AsString();
                                    }
                                    else
                                    {
                                        error = true;
                                        pgf.Close();
                                        MessageBox.Show("Error finding Prefix parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO6 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        break;
                                    }

                                    Parameter Param_Number = fi.LookupParameter("Number");
                                    if (Param_Number != null)
                                    {
                                        number = Param_Number.AsString();
                                    }
                                    else
                                    {
                                        error = true;
                                        pgf.Close();
                                        MessageBox.Show("Error finding Number parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO7 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        break;
                                    }

                                    Parameter Param_Type = fi.LookupParameter("Type");
                                    if (Param_Type != null)
                                    {
                                        type = Param_Type.AsValueString();
                                    }
                                    else
                                    {
                                        error = true;
                                        pgf.Close();
                                        MessageBox.Show("Error finding Number parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO8 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        break;
                                    }
                                }

                                if (!error)
                                {
                                    if (isUnitDrawing)
                                    {
                                    Parameter Param_Piece = fi.LookupParameter("Piece");
                                    if (Param_Piece != null)
                                    {
                                        try
                                        {
                                            string test = prefix + "-" + number;
                                            if (Param_Piece.AsString() != test)
                                            {
                                                Param_Piece.Set(test);
                                            }
                                        }
                                        catch
                                        {
                                            error = true;
                                            pgf.Close();
                                            MessageBox.Show("Error setting Piece parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO9 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            break;
                                        }

                                    }
                                    else
                                    {
                                        error = true;
                                        pgf.Close();
                                        MessageBox.Show("Error finding Piece parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO10 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                        break;
                                    }

                                        if (!error)
                                        {
                                            Parameter Param_Description = fi.LookupParameter("Piece Description");
                                            if (Param_Description != null)
                                            {
                                                try
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

                                                    string description = PieceDescriptions.PieceDescriptions.pieceDefinition(key);
                                                    if (Param_Description.AsString() != description)
                                                    {
                                                        Param_Description.Set(description);
                                                    }
                                                }
                                                catch
                                                {
                                                    error = true;
                                                    pgf.Close();
                                                    MessageBox.Show("Error setting Piece Description parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO11 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                error = true;
                                                pgf.Close();
                                                MessageBox.Show("Error finding Piece Description parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO12 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        Parameter Param_Piece = fi.LookupParameter("Piece");
                                        if (Param_Piece != null)
                                        {
                                            try
                                            {
                                                if (Param_Piece.AsString() != "")
                                                {
                                                    Param_Piece.Set("");
                                                }
                                            }
                                            catch
                                            {
                                                error = true;
                                                pgf.Close();
                                                MessageBox.Show("Error setting Piece parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO9A Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                break;
                                            }

                                        }
                                        else
                                        {
                                            error = true;
                                            pgf.Close();
                                            MessageBox.Show("Error finding Piece parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO10A Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            break;
                                        }

                                        if (!error)
                                        {
                                            Parameter Param_Description = fi.LookupParameter("Piece Description");
                                            if (Param_Description != null)
                                            {
                                                try
                                                {
                                                    if (Param_Description.AsString() != "")
                                                    {
                                                        Param_Description.Set("");
                                                    }
                                                }
                                                catch
                                                {
                                                    error = true;
                                                    pgf.Close();
                                                    MessageBox.Show("Error setting Piece Description parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO11A Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                error = true;
                                                pgf.Close();
                                                MessageBox.Show("Error finding Piece Description parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO12A Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                break;
                                            }
                                        }

                                    }
                                }
                            }
                            //else
                            //{
                            //    MessageBox.Show(sheetname + System.Environment.NewLine + detailNumber + System.Environment.NewLine + fi.Id + System.Environment.NewLine + fi.OwnerViewId);
                            //}
                        }
                        tr.Commit();
                    }

                    if (!error)
                    {
                        // *** For use in collecting a count of tagged unit numbers. 
                        pgf.IncrementWithText("Beginning collection of tagged unit numbers.");
                        ElementCategoryFilter cfilter = new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallPanelTags);
                        FilteredElementCollector col = new FilteredElementCollector(doc);
                        col.WherePasses(cfilter);
                        IList<Element> elementList = col.ToElements();

                        int incr = 0;

                        using (Transaction tr = new Transaction(doc))
                        {
                            tr.Start("Unit Update");

                            foreach (Element uEle in elementList)
                            {
                                incr++;
                                pgf.IncrementWithText("Processing unit tag " + incr.ToString() + " of " + elementList.ToString());

                                if (uEle.Name.ToString() == "Unit / Address" || uEle.Name.ToString() == "Unit Tag")
                                {
                                    View uOwnerview = doc.GetElement(uEle.OwnerViewId) as View;
                                    if (uOwnerview != null && (uOwnerview.ViewType == ViewType.Elevation || uOwnerview.ViewType == ViewType.Detail))
                                    {
                                        string sheetname = uOwnerview.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER).AsString();
                                        string detailNumber = uOwnerview.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER).AsString();
                                        string uOLetter = "";
                                        if (!string.IsNullOrWhiteSpace(sheetname) && !string.IsNullOrWhiteSpace(detailNumber))
                                        {
                                            IndependentTag uTag = uEle as IndependentTag;

                                            LinkElementId linkelementID = uTag.GetTaggedElementIds().FirstOrDefault();
                                            if (linkelementID == null) { GMSRevitAddin.GmsLog.Warn("ManualUpdateOrigins: tag has no tagged element; skipped"); continue; }

                                            Element unit = doc.GetElement(linkelementID.HostElementId);

                                            Parameter Param_oSheet = unit.LookupParameter("Origin Sheet");
                                            if (Param_oSheet != null)
                                            {
                                                try
                                                {
                                                    if (Param_oSheet.AsString() != sheetname)
                                                    {
                                                        Param_oSheet.Set(sheetname);
                                                    }
                                                }
                                                catch
                                                {
                                                    error = true;
                                                    pgf.Close();
                                                    MessageBox.Show("Error setting Origin Sheet parameter in Unit Tag Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO13 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                error = true;
                                                pgf.Close();
                                                MessageBox.Show("Error finding Origin Sheet parameter in Unit Tag Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO14 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                break;
                                            }

                                            if (!error)
                                            {
                                                Parameter Param_OLetter = unit.LookupParameter("Origin Letter");
                                                if (Param_OLetter != null)
                                                {
                                                    uOLetter = Param_OLetter.AsString();
                                                }
                                                else
                                                {
                                                    error = true;
                                                    pgf.Close();
                                                    MessageBox.Show("Error finding Origin Letter parameter in Unit Tag Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO15 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                    break;
                                                }
                                            }

                                            if (!error)
                                            {
                                                Parameter Param_uOrigin = unit.LookupParameter("Origin");
                                                if (Param_uOrigin != null)
                                                {
                                                    string OTest = detailNumber + uOLetter;
                                                    if (Param_uOrigin.AsString() != OTest)
                                                    {
                                                        try
                                                        {
                                                            Param_uOrigin.Set(OTest);
                                                        }
                                                        catch
                                                        {
                                                            error = true;
                                                            pgf.Close();
                                                            MessageBox.Show("Error setting Origin parameter in Unit Tag Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO16 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                            break;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    error = true;
                                                    pgf.Close();
                                                    MessageBox.Show("Error finding Origin parameter in Unit Tag Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO17 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                    break;
                                                }
                                            }
                                        }
                                    }
                                }
                            }
                            tr.Commit();
                        }

                        try
                            {
                            pgf.IncrementWithText("Processed 100% of unit tags.");
                        }
                        catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("ManualUpdateOrigins", __ex); }
                    }
                    return Result.Succeeded;
                }
            }
        }
    }
}