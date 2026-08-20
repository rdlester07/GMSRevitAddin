using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.ApplicationServices;
using RevitWarningSuppresion;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Xml.Linq;
using UpdateOrigins;
using static Autodesk.Revit.DB.SpecTypeId;
using static System.Net.Mime.MediaTypeNames;
using TaskDialog = Autodesk.Revit.UI.TaskDialog;
using View = Autodesk.Revit.DB.View;

namespace BySetForm
{
    // ====================================================================================================
    //  BY-SET PICKER — checklist of sheet "Grouping - Usage" values used to scope a tag update / export
    //  to one or more sheet sets.
    // ----------------------------------------------------------------------------------------------------
    //  History (see CLAUDE.md / 2026-06-29): the ribbon's "Export Pieces" button now runs
    //  ExportParts.ExportPieces (whole-schedule export, no set picker) instead of this form's
    //  "Export Parts by Set" path. LaunchEPForm / ExportPartsBySet.Export / UpdateTagsBySet's use from
    //  the export path are therefore DEAD (kept in place, not wired to any live ribbon button), because
    //  ExportParts.Export always reads the entire schedule so the set picker never actually scoped the
    //  export. The "Update Tags → By Set" ribbon button still uses LaunchForm / BySetForm live, and that
    //  path is fully active.
    // ====================================================================================================

    /// <summary>
    /// Sheet-set picker dialog. Lists every distinct "Grouping - Usage" value found on project sheets as
    /// a checklist; on Start, resolves the checked sets to their placed views and runs either a tag
    /// update (title "Update Tags by Set", live) or a parts export (title "Export Parts by Set", dead —
    /// see the file-level note above). The static fields below are shared state set by the launching
    /// IExternalCommand (LaunchForm/LaunchEPForm) before the dialog is shown, since Revit API handles
    /// (Document/UIDocument/UIApplication) aren't available to the form's own constructor.
    /// </summary>
    public partial class BySetForm : System.Windows.Forms.Form
    {
        // Static state bridging the launching command (which has the Revit API handles) to this
        // dialog and to the static helper classes below (ExportPartsBySet/UpdateTagsBySet) that run
        // after the dialog closes.
        public static System.Windows.Forms.Form thisForm = null;
        public static List<string> sheetSets = new List<string>();
        public static Autodesk.Revit.DB.Document doc = null;
        public static Autodesk.Revit.UI.UIDocument uiDoc = null;
        public static Autodesk.Revit.UI.UIApplication uiApp = null;
        public static ExternalCommandData ECD = null;
        public static string mess = null;
        public static ElementSet ElSet = null;
        public static List<ElementId> views = new List<ElementId>();   // views resolved from the checked sheet sets
        public static bool exporting = false;

        public BySetForm()
        {
            InitializeComponent();
        }

        private void BySetForm_Load(object sender, EventArgs e)
        {
            GMSRevitAddin.DarkTheme.MarkPrimary(button_Start);
            GMSRevitAddin.DarkTheme.Apply(this);
            button_Start.Enabled = true;
            button_Start.Visible = true;

            // Populate the checklist from the sheet-set names gathered by the launching command.
            var items = checkedListBox1.Items;
            sheetSets.Sort();
            foreach (string sheetSet in sheetSets)
            {
                if (!string.IsNullOrWhiteSpace(sheetSet))
                {
                    items.Add(sheetSet);
                }
            }

        }

        /// <summary>
        /// Resolves the checked sheet-set names to their placed views, then — based on the form's
        /// Text (set by the launching command) — runs either the (live) tag update or the (dead,
        /// see file-level note) by-set export.
        /// </summary>
        private void button_Start_Click(object sender, EventArgs e)
        {
            views.Clear();
            ExportPartsBySet.error = false;
            var items = checkedListBox1.CheckedIndices;
            List<string> selected = new List<string>();
            if (items.Count > 0)
            {
                selected.Clear();
                views.Clear();
                foreach (int index in items)
                {
                    if (sheetSets[index] != "???")
                    {
                        selected.Add(sheetSets[index]);
                    }
                }
            }

            List<Element> viewSheetList = new FilteredElementCollector(doc).OfCategory(BuiltInCategory.OST_Sheets).WhereElementIsNotElementType().OfClass(typeof(ViewSheet)).ToElements().ToList();

            foreach (Element viewSheetElement in viewSheetList)
            {
                ViewSheet v = viewSheetElement as ViewSheet;
                string vName = v.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();
                ISet<ElementId> viewports = v.GetAllPlacedViews();
                foreach (ElementId viewport in viewports)
                {
                    View vp = doc.GetElement(viewport) as View;
                    Parameter grouping = viewSheetElement.LookupParameter("Grouping - Usage");
                    if(grouping != null && selected.Contains(grouping.AsString()))
                    {
                        if (vName.Contains("U-") || vp.ViewType == ViewType.Elevation || vp.ViewType == ViewType.Detail)
                        {
                            views.Add(vp.Id);
                            break;
                        }
                    }
                }
            }

            if (this.Text == "Update Tags by Set")
            {
                BySetForm.thisForm.Visible = false;
                UpdateTagsBySet.UpdateTags();
                BySetForm.thisForm.Close();
            }
            else if (this.Text == "Export Parts by Set")
            {
                BySetForm.thisForm.Visible = false;
                ExportPartsBySet.Export();
                BySetForm.thisForm.Close();
            }
        }
    }

    public static class Extensions
    {
        public static List<List<T>> partition<T>(this List<T> values, int chunkSize)
        {
            return values.Select((x, i) => new { Index = i, Value = x }).GroupBy(x => x.Index / chunkSize).Select(x => x.Select(v => v.Value).ToList()).ToList();
        }
    }

    public class ExportPartsBySet
    {
        public static bool error = false;
        public static bool cancelled = false;
        public static void Export()
        {
            BySetForm.exporting = true;
            UpdateTagsBySet.UpdateTags();
            if (!error && !cancelled)
            {
                var command = new ExportParts.Export();
                command.Execute(BySetForm.ECD, ref BySetForm.mess, BySetForm.ElSet);
            }
            else if(!cancelled)
            {
                MessageBox.Show("Piece Export has been cancelled due to issues during tag update." + System.Environment.NewLine + "Correct tags and restart Export Pieces to update the parts manager.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            BySetForm.exporting = false;
        }
    }

    public class toUpdate
    {
        public ElementId elementId { get; set; }
        public string param { get; set; }
        public string value { get; set; }
    }

    public class UpdateTagsBySet
    {
        public static void UpdateTags()
        {
            Document doc = BySetForm.doc;
            string targetName = "GAIT - Piece Tag";
            bool error = false;
            bool foundNotEditible = false;
            List<string> users = new List<string>();
            List<ElementId> tagIDs = new List<ElementId>();
            List<ElementId> panelTagIDs = new List<ElementId>();


            List<FamilyInstance> familyInstances = new FilteredElementCollector(doc).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().Where(x => x.Symbol.Family.Name.Equals(targetName)).ToList<FamilyInstance>();
            ElementCategoryFilter _cfilter = new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallPanelTags);
            FilteredElementCollector _col = new FilteredElementCollector(doc);
            _col.WherePasses(_cfilter);
            IList<Element> panelTagList = _col.ToElements();
            int totalCount = familyInstances.Count() + panelTagList.Count();
            using (ProgressForm.ProgressForm pgfm = new ProgressForm.ProgressForm("Update Tags by Set", null, totalCount + 2, string.Empty))
            {
                pgfm.IncrementWithText("Gathering tags in selected set(s).");
                int inc = 0;

                foreach (FamilyInstance fi in familyInstances)
                {
                    inc++;
                    pgfm.IncrementWithText("Obtaining location of tag " + inc.ToString() + " of " + totalCount.ToString());

                    if (BySetForm.views.Contains(fi.OwnerViewId))
                    {
                        tagIDs.Add(fi.Id);
                    }
                }
                foreach (Element el in panelTagList)
                {
                    inc++;
                    pgfm.IncrementWithText("Obtaining location of tag " + inc.ToString() + " of " + totalCount.ToString());

                    if (BySetForm.views.Contains(el.OwnerViewId))
                    {
                        panelTagIDs.Add(el.Id);
                    }
                }
                pgfm.IncrementWithText("Preparing to gather tag data.");
            }

            List<toUpdate> updateList = new List<toUpdate>();
            totalCount = tagIDs.Count() + panelTagIDs.Count();
            using (ProgressForm.ProgressForm pf = new ProgressForm.ProgressForm("Update Tags by Set", null, totalCount, string.Empty))
            {
                pf.IncrementWithText("Gathering tag data.");
                int inc = 0;

                foreach (ElementId eid in tagIDs)
                {
                    inc++;
                    pf.IncrementWithText("Verifying tag data of tag " + inc.ToString() + " of " + totalCount.ToString());
                    FamilyInstance fi = doc.GetElement(eid) as FamilyInstance;
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

                        string OLetter = string.Empty;
                        string prefix = string.Empty;
                        string number = string.Empty;
                        string type = string.Empty;


                        Parameter Param_OSheet = fi.LookupParameter("Origin Sheet");
                        if (Param_OSheet != null)
                        {
                            string _oSheet = Param_OSheet.AsString();
                            if (_oSheet != sheetname)
                            {
                                toUpdate tu = new toUpdate();
                                tu.elementId = eid;
                                tu.param = "Origin Sheet";
                                tu.value = sheetname;
                                updateList.Add(tu);
                            }
                        }
                        else
                        {
                            error = true;
                            if (BySetForm.exporting)
                            {
                                ExportPartsBySet.error = true;
                            }
                            //pgf.Close();
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
                                if (BySetForm.exporting)
                                {
                                    ExportPartsBySet.error = true;
                                }
                                //pgf.Close();
                                MessageBox.Show("Error finding Origin Letter parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO3 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                break;
                            }
                        }

                        Parameter Param_Origin = fi.LookupParameter("Origin");
                        if (Param_Origin != null)
                        {
                            string _origin = Param_Origin.AsString();
                            if (isUnitDrawing)
                            {
                                if (_origin != null && _origin != sheetname)
                                {
                                    toUpdate tu = new toUpdate();
                                    tu.elementId = eid;
                                    tu.param = "Origin";
                                    tu.value = sheetname;
                                    updateList.Add(tu);
                                }
                            }
                            else
                            {
                                if (_origin != null && _origin != detailNumber + OLetter)
                                {
                                    toUpdate tu = new toUpdate();
                                    tu.elementId = eid;
                                    tu.param = "Origin";
                                    tu.value = detailNumber + OLetter;
                                    updateList.Add(tu);
                                }
                            }
                        }
                        else
                        {
                            error = true;
                            if (BySetForm.exporting)
                            {
                                ExportPartsBySet.error = true;
                            }
                            //pgf.Close();
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
                                if (BySetForm.exporting)
                                {
                                    ExportPartsBySet.error = true;
                                }
                                //pgf.Close();
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
                                if (BySetForm.exporting)
                                {
                                    ExportPartsBySet.error = true;
                                }
                                //pgf.Close();
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
                                if (BySetForm.exporting)
                                {
                                    ExportPartsBySet.error = true;
                                }
                                //pgf.Close();
                                MessageBox.Show("Error finding Number parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO8 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                break;
                            }
                        }

                        if (!error)
                        {
                            Parameter Param_Piece = fi.LookupParameter("Piece");
                            if (Param_Piece != null)
                            {
                                string test = "";
                                if (isUnitDrawing)
                                {
                                    test = prefix + "-" + number;
                                }
                                if (Param_Piece.AsString() != test)
                                {
                                    toUpdate tu = new toUpdate();
                                    tu.elementId = eid;
                                    tu.param = "Piece";
                                    tu.value = test;
                                    updateList.Add(tu);
                                }
                            }
                            else
                            {
                                error = true;
                                if (BySetForm.exporting)
                                {
                                    ExportPartsBySet.error = true;
                                }
                                //pgf.Close();
                                MessageBox.Show("Error finding Piece parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO10 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                break;
                            }

                            if (!error)
                            {
                                Parameter Param_Description = fi.LookupParameter("Piece Description");
                                if (Param_Description != null)
                                {
                                    string description = "";
                                    if (isUnitDrawing)
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

                                        description = PieceDescriptions.PieceDescriptions.pieceDefinition(key);
                                    }
                                    if (Param_Description.AsString() != description)
                                    {
                                        toUpdate tu = new toUpdate();
                                        tu.elementId = eid;
                                        tu.param = "Piece Description";
                                        tu.value = description;
                                        updateList.Add(tu);
                                    }
                                }
                                else
                                {
                                    error = true;
                                    if (BySetForm.exporting)
                                    {
                                        ExportPartsBySet.error = true;
                                    }
                                    //pgf.Close();
                                    MessageBox.Show("Error finding Piece Description parameter in Family Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO12 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    break;
                                }
                            }
                        }
                    }
                    //else
                    //{
                    //    MessageBox.Show(sheetname + System.Environment.NewLine + detailNumber + System.Environment.NewLine + fi.Id + System.Environment.NewLine + fi.OwnerViewId);
                    //}
                }

                if (!error)
                {
                    foreach (ElementId eid in panelTagIDs)
                    {
                        inc++;
                        pf.IncrementWithText("Verifying tag data of tag " + inc.ToString() + " of " + totalCount.ToString());
                        Element el = doc.GetElement(eid);
                        if (el.Name.ToString() == "Unit / Address" || el.Name.ToString() == "Unit Tag")
                        {
                            View ownerView = doc.GetElement(el.OwnerViewId) as View;
                            if (ownerView != null && (ownerView.ViewType == ViewType.Elevation || ownerView.ViewType == ViewType.Detail))
                            {
                                string sheetname = ownerView.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER).AsString();
                                string detailNumber = ownerView.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER).AsString();
                                string uOLetter = "";
                                if (!string.IsNullOrWhiteSpace(sheetname) && !string.IsNullOrWhiteSpace(detailNumber))
                                {
                                    IndependentTag uTag = el as IndependentTag;

                                    LinkElementId linkelementID = uTag.GetTaggedElementIds().FirstOrDefault();
                                    if (linkelementID == null) { GMSRevitAddin.GmsLog.Warn("BySetForm: tag has no tagged element; skipped"); continue; }

                                    Element unit = doc.GetElement(linkelementID.HostElementId);

                                    Parameter Param_oSheet = unit.LookupParameter("Origin Sheet");
                                    if (Param_oSheet != null)
                                    {
                                        if (Param_oSheet.AsString() != sheetname)
                                        {
                                            toUpdate tu = new toUpdate();
                                            tu.elementId = unit.Id;
                                            tu.param = "Origin Sheet";
                                            tu.value = sheetname;
                                            updateList.Add(tu);
                                        }
                                    }
                                    else
                                    {
                                        error = true;
                                        if (BySetForm.exporting)
                                        {
                                            ExportPartsBySet.error = true;
                                        }
                                        //pgf.Close();
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
                                            if (BySetForm.exporting)
                                            {
                                                ExportPartsBySet.error = true;
                                            }
                                            //pgf.Close();
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
                                                toUpdate tu = new toUpdate();
                                                tu.elementId = unit.Id;
                                                tu.param = "Origin";
                                                tu.value = detailNumber + uOLetter;
                                                updateList.Add(tu);
                                            }
                                        }
                                        else
                                        {
                                            error = true;
                                            if (BySetForm.exporting)
                                            {
                                                ExportPartsBySet.error = true;
                                            }
                                            //pgf.Close();
                                            MessageBox.Show("Error finding Origin parameter in Unit Tag Instance." + System.Environment.NewLine + "Update Origins canceled.", "MUO17 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            break;
                                        }
                                    }
                                }
                            }
                            else
                            {
                                MessageBox.Show("OwnerView not an elevation");
                            }
                        }
                    }
                }
                pf.IncrementWithText("Verification complete.");
            }

            if (!error && updateList.Any())
            {
                List<ElementId> errorList = new List<ElementId>();
                using (ProgressForm.ProgressForm pf = new ProgressForm.ProgressForm("Update Tags by Set", null, updateList.Count() + 3 + (updateList.Count() / 100), string.Empty))
                {
                    pf.IncrementWithText("Checking status of elements requiring update.");
                    List<ElementId> list = new List<ElementId>();
                    pf.IncrementWithText("Generating list.");
                    foreach (toUpdate toUp in updateList)
                    {
                        list.Add(toUp.elementId);
                    }
                    List<ElementId> OKtoEdit = new List<ElementId>();
                    List<List<ElementId>> partitions = list.partition(100);
                    pf.IncrementWithText("Obtaining status of elements.");
                    int inc = 0;
                    foreach (List<ElementId> partition in partitions)
                    {
                        inc++;
                        pf.IncrementWithText("Obtaining status of element batch " + inc + " of " + partitions.Count().ToString());
                        OKtoEdit.AddRange(WorksharingUtils.CheckoutElements(doc, partition));
                    }
                    inc = 0;
                    foreach (ElementId eid in list)
                    {
                        inc++;
                        pf.IncrementWithText("Checking status of element " + inc.ToString() + " of " + list.Count().ToString());

                        if (!OKtoEdit.Contains(eid))
                        {
                            foundNotEditible = true;
                            errorList.Add(eid);
                        }
                    }
                }

                if (foundNotEditible)
                {
                    List<string> sheets = new List<string>();
                    List<string> userList = new List<string>();
                    string sheet = "";
                    string byUser = "";
                    foreach (ElementId elid in errorList)
                    {
                        sheet = "";
                        try
                        {
                            Element el = doc.GetElement(elid);
                            View vw = doc.GetElement(el.OwnerViewId) as View;
                            sheet = vw.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER).AsString();
                            WorksharingUtils.GetCheckoutStatus(doc, elid, out byUser);
                        }
                        catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("BySetForm", __ex); }
                        if (!string.IsNullOrWhiteSpace(sheet) && !sheets.Contains(sheet))
                        {
                            sheets.Add(sheet);
                        }
                        if (!string.IsNullOrWhiteSpace(byUser) && !userList.Contains(byUser))
                        {
                            userList.Add(byUser);
                        }
                    }

                    string message = "Warning! Elements were found that need updating but are checked out from the central model by another user.";
                    if (userList.Any())
                    {
                        message += System.Environment.NewLine + "The following users must sync before these elements can be updated: " + System.Environment.NewLine;
                        foreach (string s in userList)
                        {
                            message += s + ", ";
                        }
                        message.Trim();
                        message.Remove(message.Length - 1);
                    }
                    if (sheets.Any())
                    {
                        message += System.Environment.NewLine + "The following sheets contain the elements that must be synced before these elements can be updated: " + System.Environment.NewLine;
                        foreach (string s in sheets)
                        {
                            message += s + ", ";
                        }
                        message.Trim();
                        message.Remove(message.Length - 1);
                    }
                    message += System.Environment.NewLine + "Do you wish to proceed with updating elements not locked or cancel this process?";
                    if (BySetForm.exporting)
                    {
                        message += System.Environment.NewLine + "Export Parts has been cancelled due to uneditable elements.";
                    }

                    TaskDialog dia = new TaskDialog("Warning!");
                    dia.MainInstruction = message;
                    dia.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Cancel tag update.");
                    dia.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Proceed to update tags not locked by other users.");
                    TaskDialogResult result = dia.Show();
                    if (result == TaskDialogResult.CommandLink1)
                    {
                        if (BySetForm.exporting)
                        {
                            ExportPartsBySet.cancelled = true;
                        }
                    }
                    else if (result == TaskDialogResult.CommandLink2)
                    {
                        if (BySetForm.exporting)
                        {
                            ExportPartsBySet.cancelled = true;
                        }

                        using (ProgressForm.ProgressForm pf = new ProgressForm.ProgressForm("Update Tags by Set", null, updateList.Count() + 2, string.Empty))
                        {
                            pf.IncrementWithText("Updating tag data.");

                            using (Transaction tr = new Transaction(doc))
                            {
                                int inc = 0;
                                tr.Start("Origin Update");
                                foreach (toUpdate tu in updateList)
                                {
                                    inc++;
                                    pf.IncrementWithText("Updating tag " + inc.ToString() + " of " + updateList.Count().ToString());
                                    if (!errorList.Contains(tu.elementId))
                                    {
                                        try
                                        {
                                            Element elem = doc.GetElement(tu.elementId);
                                            if (elem != null)
                                            {
                                                Parameter param = elem.LookupParameter(tu.param);
                                                if (param != null)
                                                {
                                                    param.Set(tu.value);
                                                }
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            error = true;
                                            GMSRevitAddin.GmsUi.ShowError("Error updating tag. Process cancelled." + System.Environment.NewLine + ex.Message, "BSF1 Error", ex);
                                            break;
                                        }
                                    }
                                }
                                tr.Commit();
                            }
                            pf.IncrementWithText("Tag Update Complete");
                        }
                    }
                }
                else
                {
                    using (ProgressForm.ProgressForm pf = new ProgressForm.ProgressForm("Update Tags by Set", null, updateList.Count() + 2, string.Empty))
                    {
                        pf.IncrementWithText("Updating tag data.");

                        using (Transaction tr = new Transaction(doc))
                        {
                            int inc = 0;
                            tr.Start("Origin Update");
                            foreach (toUpdate tu in updateList)
                            {
                                inc++;
                                pf.IncrementWithText("Updating tag " + inc.ToString() + " of " + updateList.Count().ToString());
                                if (!errorList.Contains(tu.elementId))
                                {
                                    try
                                    {
                                        Element elem = doc.GetElement(tu.elementId);
                                        if (elem != null)
                                        {
                                            Parameter param = elem.LookupParameter(tu.param);
                                            if (param != null)
                                            {
                                                param.Set(tu.value);
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        error = true;
                                        GMSRevitAddin.GmsUi.ShowError("Error updating tag. Process cancelled." + System.Environment.NewLine + ex.Message, "BSF2 Error", ex);
                                        break;
                                    }
                                }
                            }
                            tr.Commit();
                        }
                        pf.IncrementWithText("Tag Update Complete");
                    }
                }
            }
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchForm : IExternalCommand
    {
        public static ExternalCommandData ECD = null;
        public static string mess = null;
        public static ElementSet ElSet = null;
        public static Document doc = null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            BySetForm.sheetSets.Clear();
            ECD = commandData;
            mess = message;
            ElSet = elements;
            UIApplication uiApp = ECD.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            UIDocument uidoc = uiApp.ActiveUIDocument;
            BySetForm.doc = doc;
            BySetForm.uiDoc = uidoc;
            BySetForm.uiApp = uiApp;
            BySetForm.ECD = ECD;
            BySetForm.mess = mess;
            BySetForm.ElSet = ElSet;

            FilteredElementCollector collector = new FilteredElementCollector(doc);
            collector = collector.OfCategory(BuiltInCategory.OST_Sheets).WhereElementIsNotElementType().OfClass(typeof(ViewSheet));

            var sheets = collector.ToElements();

            foreach (Element elem in sheets)
            {
                Parameter param_Usage = elem.LookupParameter("Grouping - Usage");
                if(param_Usage != null)
                {
                    if (!string.IsNullOrWhiteSpace(param_Usage.AsString()) && !BySetForm.sheetSets.Contains(param_Usage.AsString()))
                    {
                        BySetForm.sheetSets.Add(param_Usage.AsString());
                    }
                }
            }

            System.Windows.Forms.Form form = new BySetForm();
            BySetForm.thisForm = form;
            form.Text = "Update Tags by Set";
            form.ShowDialog();
            return Result.Succeeded;
        }
    }

    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchEPForm : IExternalCommand
    {
        public static ExternalCommandData ECD = null;
        public static string mess = null;
        public static ElementSet ElSet = null;
        public static Document doc = null;

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            BySetForm.sheetSets.Clear();
            ECD = commandData;
            mess = message;
            ElSet = elements;
            UIApplication uiApp = ECD.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            UIDocument uidoc = uiApp.ActiveUIDocument;
            BySetForm.doc = doc;
            BySetForm.uiDoc = uidoc;
            BySetForm.uiApp = uiApp;
            BySetForm.ECD = ECD;
            BySetForm.mess = mess;
            BySetForm.ElSet = ElSet;
            ExportParts.Export.contExport = true;

            ElementId gpID = ElementId.InvalidElementId;
            gpID = GlobalParametersManager.FindByName(doc, "Parts Manager Location");
            ExportParts.Export.partsManagerLocation = "Not found";

            if (gpID != null && gpID != ElementId.InvalidElementId)
            {
                GlobalParameter gp = doc.GetElement(gpID) as GlobalParameter;
                StringParameterValue spv = gp.GetValue() as StringParameterValue;
                string text = spv.Value;
                if (text != "Not assigned" && !string.IsNullOrWhiteSpace(text))
                {
                    ExportParts.Export.partsManagerLocation = text;
                }
                else
                {
                    ExportParts.Export.partsManagerLocation = "Not assigned";
                }
            }

            ProjectInfo projectInfo = doc.ProjectInformation;
            string projectNumberFromDocument = projectInfo.Number.Trim('#');
            string parse = ExportParts.Export.partsManagerLocation.Split('(').Last();
            string projectNumberFromPartsManagerLocation = parse.Split(')').First();
            if (string.IsNullOrWhiteSpace(ExportParts.Export.partsManagerLocation) || ExportParts.Export.partsManagerLocation == "Not assigned" || ExportParts.Export.partsManagerLocation == "Not found")
            {
                TaskDialog dia = new TaskDialog("Warning!");
                dia.MainInstruction = "Warning! Project Parts Manager Location not set. Go to the GMS menu > Settings and set Project Parts Manager Location." + System.Environment.NewLine + System.Environment.NewLine + "Do you want to proceed with collection and schedule generation without exporting to a Parts Manager?";
                dia.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.Cancel;
                TaskDialogResult result = dia.Show();
                if (result == TaskDialogResult.Cancel)
                {
                    ExportParts.Export.contExport = false;
                }
            }
            else if (!File.Exists(ExportParts.Export.partsManagerLocation))
            {
                TaskDialog dia = new TaskDialog("Warning!");
                dia.MainInstruction = "Warning! Location of Project Parts Manager currently set (" + ExportParts.Export.partsManagerLocation + ") is invalid. File not found. Go to the GMS menu > Settings and set Project Parts Manager Location." + System.Environment.NewLine + System.Environment.NewLine + "Do you want to proceed with collection and schedule generation without exporting to a Parts Manager?";
                dia.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.Cancel;
                TaskDialogResult result = dia.Show();
                if (result == TaskDialogResult.Cancel)
                {
                    ExportParts.Export.contExport = false;
                }
            }
            else if (projectNumberFromDocument != projectNumberFromPartsManagerLocation)
            {
                TaskDialog dia = new TaskDialog("Warning!");
                dia.MainInstruction = "Warning! Project number from Parts Manager location does not match Project number in Revit Project Information." + System.Environment.NewLine + "Parts Manager Location: " + ExportParts.Export.partsManagerLocation + System.Environment.NewLine + "Project Number from Revit Project Information: " + projectNumberFromDocument + System.Environment.NewLine + "Project file location: " + ModelPathUtils.ConvertModelPathToUserVisiblePath(doc.GetWorksharingCentralModelPath()) + System.Environment.NewLine + System.Environment.NewLine + "Do you want to proceed with exporting to the set Parts Manager?";
                dia.CommonButtons = TaskDialogCommonButtons.Yes | TaskDialogCommonButtons.Cancel;
                TaskDialogResult result = dia.Show();
                if (result == TaskDialogResult.Cancel)
                {
                    ExportParts.Export.contExport = false;
                }
            }

            if (ExportParts.Export.contExport)
            {
                FilteredElementCollector collector = new FilteredElementCollector(doc);
                collector = collector.OfCategory(BuiltInCategory.OST_Sheets).WhereElementIsNotElementType().OfClass(typeof(ViewSheet));

                var sheets = collector.ToElements();

                foreach (Element elem in sheets)
                {
                    Parameter param_Usage = elem.LookupParameter("Grouping - Usage");
                    if (param_Usage != null)
                    {
                        if (!string.IsNullOrWhiteSpace(param_Usage.AsString()) && !BySetForm.sheetSets.Contains(param_Usage.AsString()))
                        {
                            BySetForm.sheetSets.Add(param_Usage.AsString());
                        }
                    }
                }

                System.Windows.Forms.Form form = new BySetForm();
                BySetForm.thisForm = form;
                form.Text = "Export Parts by Set";
                form.ShowDialog();
            }
            return Result.Succeeded;
        }
    }
}