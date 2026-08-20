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
using Application = Autodesk.Revit.ApplicationServices.Application;
using Autodesk.Revit.DB.Events;
using System.Diagnostics;
using System.Windows.Forms;
using System.Xml.Linq;
using View = Autodesk.Revit.DB.View;
using MessageBox = System.Windows.Forms.MessageBox;
using Microsoft.VisualBasic.ApplicationServices;
using System.Security.Cryptography;
using System.Windows.Controls;

namespace UpdateOrigins
{
    /// <summary>
    /// Keeps "GAIT - Piece Tag" family instances' Origin/Piece/Description parameters in sync with
    /// the unit sheet they're placed on. The live entry point is <see cref="onViewChange"/>, wired to
    /// <c>UIControlledApplication.ViewActivated</c> in GMS_tools.cs's <c>OnStartup</c>
    /// ("<c>UpdateOrigins.updateTags.onViewChange</c>") — it fires whenever the active view changes and
    /// corrects tags on any unit sheet ("U-" sheet number) being (re)opened. Everything else in this
    /// class (the DocumentChanged/Idling listener pair, the print-event handler, the export-event
    /// handler) is legacy commented-out code kept for reference; it does not run.
    /// </summary>
    public class updateTags
    {
        // Legacy fields for the (disabled) DocumentChanged + Idling listener pair below;
        // no longer populated by any live code path.
        public static Document doc = null;
        public static UIApplication uiapp = null;
        public static List<ElementId> needsUpdate = new List<ElementId>();
        //public static void onDocumentChanged(object sender, DocumentChangedEventArgs args)
        //{
        //    doc = args.GetDocument();
        //    uiapp = new UIApplication(sender as Autodesk.Revit.ApplicationServices.Application);
        //    string targetName = "GAIT - Piece Tag";
        //    if (doc != null)
        //    {
        //View activeView = doc.ActiveView;
        //Parameter sNumber = activeView.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER);
        //if (sNumber != null && sNumber.AsString().Contains("U-"))
        //{
        //    ElementClassFilter filter = new ElementClassFilter(typeof(FamilyInstance));
        //    List<ElementId> addedElems = args.GetAddedElementIds(filter).ToList();
        //    List<ElementId> moddedElems = args.GetModifiedElementIds(filter).ToList();

        //    if (addedElems.Any() || moddedElems.Any())
        //    {
        //        foreach (ElementId elem in addedElems)
        //        {
        //            Element el = doc.GetElement(elem);
        //            string elFamily = el.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
        //            if (elFamily == targetName)
        //            {
        //                if (!needsUpdate.Contains(elem))
        //                {
        //                    needsUpdate.Add(elem);
        //                }
        //            }
        //        }
        //        foreach (ElementId elem1 in moddedElems)
        //        {
        //            Element el1 = doc.GetElement(elem1);
        //            string elFamily1 = el1.get_Parameter(BuiltInParameter.ELEM_FAMILY_PARAM).AsValueString();
        //            if (elFamily1 == targetName)
        //            {
        //                if (!needsUpdate.Contains(elem1))
        //                {
        //                    needsUpdate.Add(elem1);
        //                }
        //            }
        //        }
        //        if (needsUpdate.Any())
        //        {
        //            //MessageBox.Show("start listener");
        //            uiapp.Idling += new EventHandler<IdlingEventArgs>(OnIdlingEvent);
        //        }
        //    }
        //}
        //    }
        //    else
        //    {
        //        MessageBox.Show("Error obtaining document.", "UO1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    }
        //}

        //public static void OnIdlingEvent(object sender, IdlingEventArgs eventArgs)
        //{
        //    uiapp.Idling -= OnIdlingEvent;
        //    //MessageBox.Show("end listener" + System.Environment.NewLine + needsUpdate.Count().ToString());

        //    if (needsUpdate.Any())
        //    {
        //        List<ElementId> tagIds = new List<ElementId>();
        //        string targetName = "GAIT - Piece Tag";
        //        tagIds = needsUpdate;
        //        //MessageBox.Show(needsUpdate.Count().ToString() + System.Environment.NewLine + tagIds.Count().ToString());
        //        //foreach (ElementId elem in tagIds)
        //        //{
        //        //    needsUpdate.Remove(elem);
        //        //}
        //        //MessageBox.Show(needsUpdate.Count().ToString() + System.Environment.NewLine + tagIds.Count().ToString());

        //        bool error = false;
        //        using (Transaction tr = new Transaction(doc))
        //        {
        //            tr.Start("Tag Update");
        //            MessageBox.Show("start tx");
        //            foreach (ElementId tId in tagIds)
        //            {
        //                FamilyInstance fi = doc.GetElement(tId) as FamilyInstance;
        //                View ownerView = doc.GetElement(fi.OwnerViewId) as View;
        //                string sheetname = ownerView.get_Parameter(BuiltInParameter.VIEWPORT_SHEET_NUMBER).AsString();
        //                string detailNumber = ownerView.get_Parameter(BuiltInParameter.VIEWPORT_DETAIL_NUMBER).AsString();
        //                MessageBox.Show(sheetname + " - " + detailNumber);
        //                if (!string.IsNullOrWhiteSpace(sheetname) && !string.IsNullOrWhiteSpace(detailNumber))
        //                {
        //                    string prefix = "";
        //                    string number = "";
        //                    string type = "";

        //                    Parameter Param_OSheet = fi.LookupParameter("Origin Sheet");
        //                    if (Param_OSheet != null)
        //                    {
        //                        string _oSheet = Param_OSheet.AsString();
        //                        if (_oSheet != sheetname)
        //                        {
        //                            try
        //                            {
        //                                Param_OSheet.Set(sheetname);
        //                            }
        //                            catch
        //                            {
        //                                error = true;
        //                                MessageBox.Show("Error setting Sheet Name from Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                                break;
        //                            }
        //                        }
        //                    }
        //                    else
        //                    {
        //                        error = true;
        //                        MessageBox.Show("Error finding Origin Sheet parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                        break;
        //                    }

        //                    Parameter Param_Origin = fi.LookupParameter("Origin");
        //                    if (Param_Origin != null)
        //                    {
        //                        string _origin = Param_Origin.AsString();
        //                        try
        //                        {
        //                            if (_origin != null && _origin != sheetname)
        //                            {
        //                                Param_Origin.Set(sheetname);
        //                            }
        //                        }
        //                        catch
        //                        {
        //                            error = true;
        //                            MessageBox.Show("Error setting Origin from Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS4 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                            break;

        //                        }
        //                    }
        //                    else
        //                    {
        //                        error = true;
        //                        MessageBox.Show("Error finding Origin parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS5 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                        break;
        //                    }

        //                    if (!error)
        //                    {
        //                        Parameter Param_Prefix = fi.LookupParameter("Prefix");
        //                        if (Param_Prefix != null)
        //                        {
        //                            prefix = Param_Prefix.AsString();
        //                        }
        //                        else
        //                        {
        //                            error = true;
        //                            MessageBox.Show("Error finding Prefix parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS6 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                            break;
        //                        }

        //                        Parameter Param_Number = fi.LookupParameter("Number");
        //                        if (Param_Number != null)
        //                        {
        //                            number = Param_Number.AsString();
        //                        }
        //                        else
        //                        {
        //                            error = true;
        //                            MessageBox.Show("Error finding Number parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS7 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                            break;
        //                        }

        //                        Parameter Param_Type = fi.LookupParameter("Type");
        //                        if (Param_Type != null)
        //                        {
        //                            type = Param_Type.AsValueString();
        //                        }
        //                        else
        //                        {
        //                            error = true;
        //                            MessageBox.Show("Error finding Number parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS8 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                            break;
        //                        }
        //                    }

        //                    if (!error)
        //                    {
        //                        Parameter Param_Piece = fi.LookupParameter("Piece");
        //                        if (Param_Piece != null)
        //                        {
        //                            try
        //                            {
        //                                string test = prefix + "-" + number;
        //                                if (Param_Piece.AsValueString() != test)
        //                                {
        //                                    Param_Piece.Set(test);
        //                                }
        //                            }
        //                            catch
        //                            {
        //                                error = true;
        //                                MessageBox.Show("Error setting Piece parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS9 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                                break;
        //                            }

        //                        }
        //                        else
        //                        {
        //                            error = true;
        //                            MessageBox.Show("Error finding Piece parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS10 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                            break;
        //                        }

        //                        if (!error)
        //                        {
        //                            Parameter Param_Description = fi.LookupParameter("Piece Description");
        //                            if (Param_Description != null)
        //                            {
        //                                try
        //                                {
        //                                    string key = "";
        //                                    switch (type)
        //                                    {
        //                                        case "Customer":
        //                                            key = "CU";
        //                                            break;
        //                                        case "Gasket":
        //                                            key = "GK";
        //                                            break;
        //                                        case "Glazing":
        //                                            key = "GL";
        //                                            break;
        //                                        case "SubUnit":
        //                                            key = "SU";
        //                                            break;
        //                                        default:
        //                                            key = prefix;
        //                                            break;
        //                                    }

        //                                    string description = PieceDescriptions.PieceDescriptions.pieceDefinition(key);
        //                                    if (Param_Description.AsString() != description)
        //                                    {
        //                                        Param_Description.Set(description);
        //                                    }
        //                                }
        //                                catch
        //                                {
        //                                    error = true;
        //                                    MessageBox.Show("Error setting Piece Description parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS11 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                                    break;
        //                                }
        //                            }
        //                            else
        //                            {
        //                                error = true;
        //                                MessageBox.Show("Error finding Piece Description parameter in Family Instance." + System.Environment.NewLine + "Tag Update canceled.", "TUCS12 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                                break;
        //                            }
        //                        }
        //                    }
        //                }

        //                if (!error)
        //                {
        //                    View view = doc.GetElement(tId) as View;
        //                    ViewSheet viewSheet = doc.GetElement(view.OwnerViewId) as ViewSheet;
        //                    FilteredElementCollector collector = new FilteredElementCollector(doc, viewSheet.Id);
        //                    var scheduleSheetInstances = collector.OfClass(typeof(ScheduleSheetInstance)).ToElements().OfType<ScheduleSheetInstance>();
        //                    foreach (var scheduleSheetInstance in scheduleSheetInstances)
        //                    {
        //                        var scheduleId = scheduleSheetInstance.ScheduleId;
        //                        if (scheduleId != ElementId.InvalidElementId)
        //                        {
        //                            ViewSchedule schedule = doc.GetElement(scheduleId) as ViewSchedule;
        //                            if (schedule != null)
        //                            {
        //                                try
        //                                {
        //                                    ElementId elid = schedule.Definition.FamilyId;
        //                                    Element el = doc.GetElement(elid);
        //                                    if (el.Name.Contains(targetName))
        //                                    {
        //                                        if (schedule.Name != ("UNIT_" + viewSheet.SheetNumber + " PIECES"))
        //                                        {
        //                                            schedule.Name = ("UNIT_" + viewSheet.SheetNumber + " PIECES");
        //                                        }
        //                                        IList<ScheduleFilter> filters = schedule.Definition.GetFilters();
        //                                        int filterPosition = 0;
        //                                        bool found = false;
        //                                        foreach (ScheduleFilter cFilter in filters)
        //                                        {
        //                                            if (cFilter.IsStringValue && cFilter.GetStringValue() == viewSheet.SheetNumber)
        //                                            {
        //                                                found = true;
        //                                                break;
        //                                            }
        //                                            else
        //                                            {
        //                                                try
        //                                                {
        //                                                    schedule.Definition.RemoveFilter(filterPosition);
        //                                                }
        //                                                catch
        //                                                {
        //                                                    MessageBox.Show("Unable to remove incorrect filter from schedule.", "TUCS18 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                                                }
        //                                            }
        //                                        }
        //                                        if (!found)
        //                                        {
        //                                            try
        //                                            {

        //                                                int fieldcount = schedule.Definition.GetFieldCount();
        //                                                for (int i = 0; i < fieldcount; i++)
        //                                                {
        //                                                    if (schedule.Definition.GetField(i).GetName() == "Origin")
        //                                                    {
        //                                                        ScheduleFieldId ori = schedule.Definition.GetField(i).FieldId;
        //                                                        ScheduleFilter schFilter = new ScheduleFilter(ori, ScheduleFilterType.Equal, viewSheet.SheetNumber);
        //                                                        schedule.Definition.AddFilter(schFilter);
        //                                                    }
        //                                                }
        //                                            }
        //                                            catch
        //                                            {
        //                                                MessageBox.Show("Unable to add filter to schedule.", "TUCS19 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                                            }
        //                                        }
        //                                    }
        //                                }
        //                                catch
        //                                {
        //                                    MessageBox.Show("Unable to Update schedule name and/or filter.", "TUCS20 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //                                }
        //                            }
        //                        }
        //                    }
        //                }
        //            }
        //            tr.Commit();
        //        }
        //    }
        //}

        /// <summary>
        /// Registered on <c>UIControlledApplication.ViewActivated</c> (see GMS_tools.cs
        /// <c>OnStartup</c>) — fires every time the active view changes. When the newly activated
        /// view is a unit sheet (sheet number contains "U-"), walks every "GAIT - Piece Tag" family
        /// instance placed on that sheet's views and corrects any tag whose Origin/Origin Sheet/Piece/
        /// Piece Description parameters have drifted from what the sheet says they should be (e.g. the
        /// tag was copied from another sheet and never re-stamped). Silently no-ops for non-unit sheets
        /// and swallows all exceptions so a bad tag never blocks navigation.
        /// </summary>
        public static void onViewChange(object sender, ViewActivatedEventArgs e)
        {
            Document doc = e.Document;

            if (e.CurrentActiveView is ViewSheet)
            {
                string name = e.CurrentActiveView.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();
                if (name.Contains("U-"))
                {
                    ViewSheet view = e.CurrentActiveView as ViewSheet;
                    string targetName = "GAIT - Piece Tag";

                    try
                    {
                        // Every view (viewport) placed on this sheet — piece tags can live on any of them.
                        ISet<ElementId> views = view.GetAllPlacedViews();
                        foreach (ElementId eid in views)
                        {
                            List<FamilyInstance> familyInstances = new FilteredElementCollector(doc, eid).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().Where(x => x.Symbol.Family.Name.Equals(targetName)).ToList<FamilyInstance>();

                            List<FamilyInstance> needsUpdating = new List<FamilyInstance>();
                            foreach (FamilyInstance familyInstance in familyInstances)
                            {
                                // Origin/Origin Sheet: which sheet the piece was originally tagged/detailed on.
                                // Prefix/Number: the piece mark components; Piece is their "Prefix-Number" join.
                                // Type: drives the description lookup key below. Piece Description: the
                                // human-readable text derived from Type (or Prefix, for standard piece types).
                                string prefix = familyInstance.LookupParameter("Prefix").AsString();
                                string number = familyInstance.LookupParameter("Number").AsString();
                                string origin = familyInstance.LookupParameter("Origin").AsString();
                                string type = familyInstance.LookupParameter("Type").AsValueString();
                                string oSheet = familyInstance.LookupParameter("Origin Sheet").AsString();
                                string piece = familyInstance.LookupParameter("Piece").AsString();
                                string description = familyInstance.LookupParameter("Piece Description").AsString();

                                if (prefix != null && number != null && origin != null && type != null && oSheet != null && piece != null && description != null)
                                {
                                    // Map the tag's Type to the piece-description lookup key; unknown/standard
                                    // types fall back to using the Prefix itself as the key.
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

                                    // Flag the tag if any of its stored values disagree with what this sheet
                                    // implies (Origin/Origin Sheet should equal the current sheet number,
                                    // Piece should equal "Prefix-Number", Description should match the key lookup).
                                    string testDescription = PieceDescriptions.PieceDescriptions.pieceDefinition(key);
                                    if ((origin != name || oSheet != name || piece != prefix + "-" + number || description != testDescription) && !needsUpdating.Contains(familyInstance) && !string.IsNullOrWhiteSpace(prefix) && !string.IsNullOrWhiteSpace(number))
                                    {
                                        needsUpdating.Add(familyInstance);
                                    }
                                }
                            }

                            if (needsUpdating.Any())
                            {
                                // Before editing, verify none of the flagged tags are checked out by another
                                // user in the central model — a worksharing edit would otherwise fail mid-loop.
                                List<ElementId> ids = new List<ElementId>();
                                List<string> users = new List<string>();
                                foreach (FamilyInstance fam in needsUpdating)
                                {
                                    ids.Add(fam.Id);
                                }
                                ICollection<ElementId> OKtoEdit = WorksharingUtils.CheckoutElements(doc, ids);
                                foreach (ElementId id in ids)
                                {
                                    if (!OKtoEdit.Contains(id))
                                    {
                                        string user = "";
                                        WorksharingUtils.GetCheckoutStatus(doc, id, out user);
                                        if (!users.Contains(user))
                                        {
                                            users.Add(user);
                                        }
                                    }
                                }

                                if (users.Any())
                                {
                                    string uNames = "";
                                    foreach (string uName in users)
                                    {
                                        uNames += uName + " ";
                                    }
                                    MessageBox.Show("Inccorect tag data found on sheet but unable to Update data. Tag(s) are checked out from the central model by the following user(s):" + System.Environment.NewLine +
                                        uNames.Trim() + System.Environment.NewLine + "Tag Update on this sheet has been cancelled.", "UO1 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                }
                                else
                                {
                                    // correct tags — re-stamp Origin/Origin Sheet/Piece/Piece Description on
                                    // every flagged instance to match the sheet that's now active.
                                    bool error = false;
                                    using (Transaction tr = new Transaction(doc))
                                    {
                                        tr.Start("Update Origins");
                                        foreach (FamilyInstance fi in needsUpdating)
                                        {
                                            string prefix = fi.LookupParameter("Prefix").AsString();
                                            string number = fi.LookupParameter("Number").AsString();
                                            string type = fi.LookupParameter("Type").AsValueString();

                                            Parameter origin = fi.LookupParameter("Origin");
                                            Parameter oSheet = fi.LookupParameter("Origin Sheet");
                                            Parameter piece = fi.LookupParameter("Piece");
                                            Parameter description = fi.LookupParameter("Piece Description");

                                            if (origin != null && origin.AsString() != name)
                                            {
                                                try
                                                {
                                                    origin.Set(name);
                                                }
                                                catch
                                                {
                                                    error = true;
                                                }
                                            }

                                            if (oSheet != null && oSheet.AsString() != name)
                                            {
                                                try
                                                {
                                                    oSheet.Set(name);
                                                }
                                                catch
                                                {
                                                    error = true;
                                                }
                                            }

                                            string testPiece = prefix + "-" + number;
                                            if (piece != null && piece.AsString() != testPiece)
                                            {
                                                try
                                                {
                                                    piece.Set(testPiece);
                                                }
                                                catch
                                                {
                                                    error = true;
                                                }
                                            }

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
                                            string testDescription = PieceDescriptions.PieceDescriptions.pieceDefinition(key);
                                            if (description != null && description.AsString() != testDescription)
                                            {
                                                try
                                                {
                                                    description.Set(testDescription);
                                                }
                                                catch
                                                {
                                                    error = true;
                                                }
                                            }
                                        }
                                        tr.Commit();
                                    }

                                    if (error)
                                    {
                                        MessageBox.Show("Error updating incorrect tag data on sheet " + name, "UO2 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                                    }
                                }

                                // REMOVED (2026-08-18): this used to rename the model's *shared*
                                // "(Do Not Open) Unit Pieces" schedule to "UNIT_<sheet> PIECES" and rebuild
                                // its single Origin-equals filter to match whichever sheet was just
                                // activated — onViewChange fires on EVERY view activation, so this ran
                                // extremely often. That was the old per-unit-schedule mechanism (one
                                // schedule per unit, self-filtered on its own Origin) and actively fights
                                // the shared-schedule design CreateUnitSheet.cs has used since 2026-07-31
                                // (one constant-named schedule placed on every unit sheet, filtered
                                // per-instance via Revit's native "Filter by: Sheet" instead of a
                                // hand-maintained single-value filter). Left running, simply switching to a
                                // view would rename the shared schedule and clobber its filter down to one
                                // sheet. The identical block in ViewNames.cs's SheetWatcherUpdater (fires on
                                // any OST_Sheets element change) was removed at the same time.
                                // TagUpdateCurrentSheet.cs's manual "Update Tags" command still has this
                                // same pattern but was left alone — that's a deliberate user action, not a
                                // background event.
                            }
                        }
                    }
                    catch //(Exception ee)
                    {
                        //MessageBox.Show(ee.Message);
                    }
                }
            }
        }

        //public static void onPrint(object sender, DocumentPrintingEventArgs eventArgs)
        //{

        //    //
        //    //not working. internal error on run. does not Update schedule on sheet even though tags are updated.
        //    //


        //    //Document doc = eventArgs.Document;
        //    //List<ElementId> views = eventArgs.GetViewElementIds().ToList();
        //    //foreach (ElementId viewId in views)
        //    //{
        //    //    ViewSheet v = doc.GetElement(viewId) as ViewSheet;
        //    //    string name = v.get_Parameter(BuiltInParameter.SHEET_NUMBER).AsString();
        //    //    //MessageBox.Show(name);
        //    //    if (name.Contains("U-"))
        //    //    {
        //    //        string targetName = "GAIT - Piece Tag";

        //    //        try
        //    //        {
        //    //            ISet<ElementId> vprts = v.GetAllPlacedViews();
        //    //            foreach (ElementId eid in vprts)
        //    //            {
        //    //                List<FamilyInstance> familyInstances = new FilteredElementCollector(doc, eid).OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>().Where(x => x.Symbol.Family.Name.Equals(targetName)).ToList<FamilyInstance>();

        //    //                List<FamilyInstance> needsUpdating = new List<FamilyInstance>();
        //    //                foreach (FamilyInstance familyInstance in familyInstances)
        //    //                {
        //    //                    string prefix = familyInstance.LookupParameter("Prefix").AsString();
        //    //                    string number = familyInstance.LookupParameter("Number").AsString();
        //    //                    string origin = familyInstance.LookupParameter("Origin").AsString();
        //    //                    string type = familyInstance.LookupParameter("Type").AsValueString();
        //    //                    string oSheet = familyInstance.LookupParameter("Origin Sheet").AsString();
        //    //                    string piece = familyInstance.LookupParameter("Piece").AsString();
        //    //                    string description = familyInstance.LookupParameter("Piece Description").AsString();

        //    //                    if (prefix != null && number != null && origin != null && type != null && oSheet != null && piece != null && description != null)
        //    //                    {
        //    //                        string key = "";
        //    //                        switch (type)
        //    //                        {
        //    //                            case "Customer":
        //    //                                key = "CU";
        //    //                                break;
        //    //                            case "Gasket":
        //    //                                key = "GK";
        //    //                                break;
        //    //                            case "Glazing":
        //    //                                key = "GL";
        //    //                                break;
        //    //                            case "SubUnit":
        //    //                                key = "SU";
        //    //                                break;
        //    //                            default:
        //    //                                key = prefix;
        //    //                                break;
        //    //                        }

        //    //                        string testDescription = PieceDescriptions.PieceDescriptions.pieceDefinition(key);
        //    //                        if ((origin != name || oSheet != name || piece != prefix + "-" + number || description != testDescription) && !needsUpdating.Contains(familyInstance) && !string.IsNullOrWhiteSpace(prefix) && !string.IsNullOrWhiteSpace(number))
        //    //                        {
        //    //                            needsUpdating.Add(familyInstance);
        //    //                        }
        //    //                    }
        //    //                }

        //    //                //MessageBox.Show(needsUpdating.Count().ToString());

        //    //                if (needsUpdating.Any())
        //    //                {
        //    //                    List<ElementId> ids = new List<ElementId>();
        //    //                    List<string> users = new List<string>();
        //    //                    foreach (FamilyInstance fam in needsUpdating)
        //    //                    {
        //    //                        ids.Add(fam.Id);
        //    //                    }
        //    //                    ICollection<ElementId> OKtoEdit = WorksharingUtils.CheckoutElements(doc, ids);
        //    //                    foreach (ElementId id in ids)
        //    //                    {
        //    //                        if (!OKtoEdit.Contains(id))
        //    //                        {
        //    //                            string user = "";
        //    //                            WorksharingUtils.GetCheckoutStatus(doc, eid, out user);
        //    //                            if (!users.Contains(user))
        //    //                            {
        //    //                                users.Add(user);
        //    //                            }
        //    //                        }
        //    //                    }

        //    //                    if (users.Any())
        //    //                    {
        //    //                        string uNames = "";
        //    //                        foreach (string uName in users)
        //    //                        {
        //    //                            uNames += uName + " ";
        //    //                        }
        //    //                        MessageBox.Show("Inccorect tag data found on sheet " + name + " but unable to Update data. Tag(s) are checked out from the central model by the following user(s):" + System.Environment.NewLine +
        //    //                            uNames.Trim() + System.Environment.NewLine + "Tag Update on this sheet has been cancelled.", "UO3 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    //                    }
        //    //                    else
        //    //                    {
        //    //                        //correct tags
        //    //                        bool error = false;
        //    //                        using (Transaction tr = new Transaction(doc))
        //    //                        {
        //    //                            tr.Start("Update Origins");
        //    //                            foreach (FamilyInstance fi in needsUpdating)
        //    //                            {
        //    //                                string prefix = fi.LookupParameter("Prefix").AsString();
        //    //                                string number = fi.LookupParameter("Number").AsString();
        //    //                                string type = fi.LookupParameter("Type").AsValueString();

        //    //                                Parameter origin = fi.LookupParameter("Origin");
        //    //                                Parameter oSheet = fi.LookupParameter("Origin Sheet");
        //    //                                Parameter piece = fi.LookupParameter("Piece");
        //    //                                Parameter description = fi.LookupParameter("Piece Description");

        //    //                                if (origin != null && origin.AsString() != name)
        //    //                                {
        //    //                                    try
        //    //                                    {
        //    //                                        origin.Set(name);
        //    //                                    }
        //    //                                    catch
        //    //                                    {
        //    //                                        error = true;
        //    //                                    }
        //    //                                }

        //    //                                if (oSheet != null && oSheet.AsString() != name)
        //    //                                {
        //    //                                    try
        //    //                                    {
        //    //                                        oSheet.Set(name);
        //    //                                    }
        //    //                                    catch
        //    //                                    {
        //    //                                        error = true;
        //    //                                    }
        //    //                                }

        //    //                                string testPiece = prefix + "-" + number;
        //    //                                if (piece != null && piece.AsString() != testPiece)
        //    //                                {
        //    //                                    try
        //    //                                    {
        //    //                                        piece.Set(testPiece);
        //    //                                    }
        //    //                                    catch
        //    //                                    {
        //    //                                        error = true;
        //    //                                    }
        //    //                                }

        //    //                                string key = "";
        //    //                                switch (type)
        //    //                                {
        //    //                                    case "Customer":
        //    //                                        key = "CU";
        //    //                                        break;
        //    //                                    case "Gasket":
        //    //                                        key = "GK";
        //    //                                        break;
        //    //                                    case "Glazing":
        //    //                                        key = "GL";
        //    //                                        break;
        //    //                                    case "SubUnit":
        //    //                                        key = "SU";
        //    //                                        break;
        //    //                                    default:
        //    //                                        key = prefix;
        //    //                                        break;
        //    //                                }
        //    //                                string testDescription = PieceDescriptions.PieceDescriptions.pieceDefinition(key);
        //    //                                if (description != null && description.AsString() != testDescription)
        //    //                                {
        //    //                                    try
        //    //                                    {
        //    //                                        description.Set(testDescription);
        //    //                                    }
        //    //                                    catch
        //    //                                    {
        //    //                                        error = true;
        //    //                                    }
        //    //                                }
        //    //                            }

        //    //                            //FilteredElementCollector collector = new FilteredElementCollector(doc, viewId);
        //    //                            //var scheduleSheetInstances = collector.OfClass(typeof(ScheduleSheetInstance)).ToElements().OfType<ScheduleSheetInstance>();
        //    //                            //foreach (var scheduleSheetInstance in scheduleSheetInstances)
        //    //                            //{
        //    //                            //    var scheduleId = scheduleSheetInstance.ScheduleId;
        //    //                            //    if (scheduleId != ElementId.InvalidElementId)
        //    //                            //    {
        //    //                            //        ViewSchedule schedule = doc.GetElement(scheduleId) as ViewSchedule;
        //    //                            //        if (schedule != null)
        //    //                            //        {
        //    //                            //            schedule.RefreshData();
        //    //                            //        }
        //    //                            //    }
        //    //                            //}
        //    //                            tr.Commit();
        //    //                        }

        //    //                        if (error)
        //    //                        {
        //    //                            MessageBox.Show("Error updating incorrect tag data on sheet " + name, "UO4 Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
        //    //                        }
        //    //                    }
        //    //                }
        //    //            }
        //    //        }
        //    //        catch (Exception ee)
        //    //        {
        //    //            MessageBox.Show(ee.Message);
        //    //        }
        //    //    }
        //    //}
        //}


        //public static void onExport(object sender, FileExportingEventArgs eA)
        //{
        //    //Document doc = eA.Document;
        //    //if(eA.Format == ImportExportFileFormat.PDF || eA.Format == ImportExportFileFormat.DWG)
        //    //{
        //    //    TaskDialog dia = new TaskDialog("Warning!");
        //    //    dia.MainInstruction = "Warning! A schedule with the name " + "\"UNIT_" + newUnitNumber + " PIECES\"" + " already exists. Select";
        //    //    dia.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Overwrite existing schedule.");
        //    //    dia.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Use existing schedule.");
        //    //    dia.AddCommandLink(TaskDialogCommandLinkId.CommandLink3, "Cancel New Unit Sheet creation.");
        //    //    TaskDialogResult result = dia.Show();
        //    //    if (result == TaskDialogResult.CommandLink1)
        //    //    {
        //    //        //delete existing and create new
        //    //        using (Transaction tDelete = new Transaction(doc, "Delete Existing Schedule"))
        //    //        {
        //    //            tDelete.Start();
        //    //            ICollection<ElementId> deletedIDSet = doc.Delete(el.Id);
        //    //            tDelete.Commit();
        //    //        }
        //    //        newSchedule = true;
        //    //    }
        //    //    else if (result == TaskDialogResult.CommandLink2)
        //    //    {
        //    //        //use existing
        //    //        ViewSchedule existingVS = el as ViewSchedule;
        //    //        scheduleID = existingVS.Id;
        //    //        newSchedule = false;
        //    //    }
        //    //    else if (result == TaskDialogResult.CommandLink3)
        //    //    {
        //    //        //cancel
        //    //        error = true;
        //    //        newSchedule = false;
        //    //    }

        //        //taskdialog Update tags?
        //        //if yes -> Update all unit tags
        //        //MessageBox.Show("pdf");
        //}
    }
}
