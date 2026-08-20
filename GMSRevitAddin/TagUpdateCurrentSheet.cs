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
using System.Windows.Forms;
using View = Autodesk.Revit.DB.View;
using Autodesk.Revit.DB.Events;

namespace TagUpdateCurrentSheet
{
    /// <summary>
    /// Updates piece-tag origin/mark data for every view placed on the currently active sheet — the
    /// single-sheet-scoped counterpart to the doc-wide <c>ManualUpdateOrigins</c> command. For each
    /// "GAIT - Piece Tag" family instance owned by a placed view: sets "Origin Sheet" to the sheet
    /// number; for unit drawings (sheet number contains "U-") sets "Origin"/"Piece"/"Piece Description"
    /// from the unit sheet context, otherwise derives them from the view's detail number + an
    /// "Origin Letter" parameter. Also retags "Unit / Address"/"Unit Tag" instances on
    /// elevation/detail views to the same sheet's origin. Wired as the ribbon's per-sheet tag-update
    /// command; requires the active view to be a <see cref="ViewSheet"/>.
    ///
    /// Used to also rename/refilter the sheet's piece schedule for unit drawings — removed
    /// 2026-08-18, see <c>ViewNames.cs</c>'s <c>SheetWatcherUpdater</c> for why (obsolete under the
    /// shared-schedule design <c>CreateUnitSheet.cs</c> has used since 2026-07-31).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CurrentSheetOriginUpdate : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Autodesk.Revit.ApplicationServices.Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);

            bool error = false;
            bool isUnitDrawing = false;

            if (doc.ActiveView is Autodesk.Revit.DB.ViewSheet)
            {
                ViewSheet viewSheet = doc.ActiveView as ViewSheet;

                string targetName = "GAIT - Piece Tag";

                try
                {
                    // Each view placed on this sheet is processed in its own transaction so a failure
                    // on one view's tags doesn't block updating the others.
                    ISet<ElementId> views = viewSheet.GetAllPlacedViews();
                    foreach (ElementId eid in views)
                    {
                        List<FamilyInstance> familyInstances = new FilteredElementCollector(doc, eid).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().Where(x => x.Symbol.Family.Name.Equals(targetName)).ToList<FamilyInstance>();
                        using (Transaction tr = new Transaction(doc))
                        {
                            tr.Start("Origin Update per View");
                            foreach (FamilyInstance fi in familyInstances)
                            {
                                View ownerView = doc.GetElement(fi.OwnerViewId) as View;
                                string sheetname = ownerView.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER).AsString();
                                string detailNumber = ownerView.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER).AsString();

                                if (!string.IsNullOrWhiteSpace(sheetname) && !string.IsNullOrWhiteSpace(detailNumber))
                                {
                                    // Sheet numbers containing "U-" are unit drawings; these get their
                                    // own Origin/Piece/Description rules below (vs. the generic
                                    // detail-number + Origin Letter rule for non-unit sheets).
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
                                                MessageBox.Show("Error setting Sheet Name from Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                break;
                                            }
                                        }
                                    }
                                    else
                                    {
                                        error = true;
                                        MessageBox.Show("Error finding Origin Sheet parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                                            MessageBox.Show("Error finding Origin Letter parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS3 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            break;
                                        }
                                    }

                                    Parameter Param_Origin = fi.LookupParameter("Origin");
                                    if (Param_Origin != null)
                                    {
                                        string _origin = Param_Origin.AsString();
                                        try
                                        {
                                            // Unit drawings: Origin is the sheet number itself.
                                            // Non-unit sheets: Origin is "<detail number><origin letter>".
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
                                            MessageBox.Show("Error setting Origin from Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS4 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            break;

                                        }
                                    }
                                    else
                                    {
                                        error = true;
                                        MessageBox.Show("Error finding Origin parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS5 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                                            MessageBox.Show("Error finding Prefix parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS6 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                                            MessageBox.Show("Error finding Number parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS7 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                                            MessageBox.Show("Error finding Number parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS8 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            break;
                                        }
                                    }

                                    if (!error)
                                    {
                                        Parameter Param_Piece = fi.LookupParameter("Piece");
                                        if (Param_Piece != null)
                                        {
                                            try
                                            {
                                                // Unit drawings: "Piece" is "<prefix>-<number>". Non-unit
                                                // sheets don't use Piece — clear it if previously set.
                                                if (isUnitDrawing)
                                                {
                                                    string test = prefix + "-" + number;
                                                    if (Param_Piece.AsString() != test)
                                                    {
                                                        Param_Piece.Set(test);
                                                    }
                                                }
                                                else
                                                {
                                                    if (!string.IsNullOrWhiteSpace(Param_Piece.AsString()))
                                                    {
                                                        Param_Piece.Set("");
                                                    }
                                                }
                                            }
                                            catch
                                            {
                                                error = true;
                                                MessageBox.Show("Error setting Piece parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS9 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                break;
                                            }

                                        }
                                        else
                                        {
                                            error = true;
                                            MessageBox.Show("Error finding Piece parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS10 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                            break;
                                        }

                                        if (!error)
                                        {
                                            Parameter Param_Description = fi.LookupParameter("Piece Description");
                                            if (Param_Description != null)
                                            {
                                                try
                                                {
                                                    if (isUnitDrawing)
                                                    {
                                                        // Piece "Type" maps to a fixed lookup key for special
                                                        // piece categories; anything else falls back to using
                                                        // the piece's Prefix as the description lookup key.
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
                                                    else
                                                    {
                                                        if (!string.IsNullOrWhiteSpace(Param_Description.AsString()))
                                                        {
                                                            Param_Description.Set("");
                                                        }
                                                    }
                                                }
                                                catch
                                                {
                                                    error = true;
                                                    MessageBox.Show("Error setting Piece Description parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS11 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                    break;
                                                }
                                            }
                                            else
                                            {
                                                error = true;
                                                MessageBox.Show("Error finding Piece Description parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS12 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                break;
                                            }
                                        }
                                    }
                                }
                            }
                            tr.Commit();
                        }
                        if (!error)
                        {
                            // *** For use in collecting a count of tagged unit numbers.
                            // Retags "Unit / Address"/"Unit Tag" instances (curtain wall panel tags) on
                            // elevation/detail views owned by this sheet, propagating the same
                            // sheet/detail-derived Origin Sheet/Origin values onto the tagged unit's
                            // own parameters (not the piece tag's — a different family/category).
                            ElementCategoryFilter cfilter = new ElementCategoryFilter(BuiltInCategory.OST_CurtainWallPanelTags);
                            FilteredElementCollector col = new FilteredElementCollector(doc, eid);
                            col.WherePasses(cfilter);
                            IList<Element> elementList = col.ToElements();

                            int incr = 0;

                            using (Transaction tr1 = new Transaction(doc))
                            {
                                tr1.Start("Unit Update");

                                foreach (Element uEle in elementList)
                                {
                                    incr++;
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

                                                // A tag with no tagged element (e.g. an orphaned tag) can't
                                                // be resolved back to a unit panel — skip it rather than
                                                // throwing on a null host element id.
                                                LinkElementId linkelementID = uTag.GetTaggedElementIds().FirstOrDefault();
                                                if (linkelementID == null) { GMSRevitAddin.GmsLog.Warn("TagUpdateCurrentSheet: tag has no tagged element; skipped"); continue; }

                                                Element unit = doc.GetElement(linkelementID.HostElementId);

                                                Parameter Param_oSheet = unit.LookupParameter("Origin Sheet");
                                                if (Param_oSheet != null)
                                                {
                                                    if (Param_oSheet.AsString() != sheetname)
                                                    {
                                                        try
                                                        {
                                                            Param_oSheet.Set(sheetname);
                                                        }
                                                        catch
                                                        {
                                                            error = true;
                                                            MessageBox.Show("Error setting Origin Sheet parameter in Unit Tag Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS13 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                            break;
                                                        }
                                                    }
                                                }
                                                else
                                                {
                                                    error = true;
                                                    MessageBox.Show("Error finding Origin Sheet parameter in Unit Tag Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS14 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
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
                                                        MessageBox.Show("Error finding Origin Letter parameter in Unit Tag Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS15 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                        break;
                                                    }
                                                }

                                                if (!error)
                                                {
                                                    Parameter Param_uOrigin = unit.LookupParameter("Origin");
                                                    if (Param_uOrigin != null)
                                                    {
                                                        if (Param_uOrigin.AsString() != detailNumber + uOLetter)
                                                        {
                                                            try
                                                            {
                                                                Param_uOrigin.Set(detailNumber + uOLetter);
                                                            }
                                                            catch
                                                            {
                                                                error = true;
                                                                MessageBox.Show("Error setting Origin parameter in Unit Tag Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS16 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                                break;
                                                            }
                                                        }
                                                    }
                                                    else
                                                    {
                                                        error = true;
                                                        MessageBox.Show("Error finding Origin parameter in Unit Tag Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS17 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                                        break;
                                                    }
                                                }
                                            }
                                        }
                                    }
                                }
                                tr1.Commit();
                            }
                        }

                        // REMOVED (2026-08-18): this used to rename the model's *shared*
                        // "(Do Not Open) Unit Pieces" schedule to "UNIT_<sheet> PIECES" and rebuild its
                        // single Origin-equals filter to match this sheet, every time this command ran
                        // on a unit drawing. That was the old per-unit-schedule mechanism (one schedule
                        // per unit, self-filtered on its own Origin) and actively fights the
                        // shared-schedule design CreateUnitSheet.cs has used since 2026-07-31 (one
                        // constant-named schedule placed on every unit sheet, filtered per-instance via
                        // Revit's native "Filter by: Sheet" instead of a hand-maintained single-value
                        // filter). The identical blocks in ViewNames.cs's SheetWatcherUpdater and
                        // UpdateOrigins.cs's onViewChange (both live, automatically-triggered event
                        // handlers) were removed at the same time — this was the third and last copy,
                        // reachable only via this ribbon command rather than a background event.
                    }
                }
                catch (Exception excep)
                {
                    GMSRevitAddin.GmsUi.ShowError(excep, "TUCS Error");
                }
            }
            else
            {
                MessageBox.Show("Active view is not a sheet. If the current view is a sheet, deactive the viewport and try again.", "TUCS21 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }

            return Result.Succeeded;
        }
    }
}