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

namespace AddUnitSchedule
{
    // ---------------------------------------------------------------------------------------
    // RETIRED 2026-07-31 — "Add Unit Schedule".
    //
    // This command built a per-unit "GAIT - Piece Tag" note-block schedule and placed it on the
    // active unit sheet. Unit sheets now use the single shared "(Do Not Open) Unit Pieces"
    // schedule that lives in the model, placed by CreateUnitSheet — nothing generates a per-unit
    // schedule any more.
    //
    // It was already unreachable before being retired: the summary below claimed it was wired in
    // GMS_tools.cs, but no PushButtonData referenced "AddUnitSchedule.AddSchedule", so the ribbon
    // button had been removed at some point and the comment went stale.
    //
    // Kept (commented out) rather than deleted, matching HelpMenu.Help and the BySetForm export
    // path. Deleting the file would also be safe: the csproj uses implicit compile globbing.
    // ---------------------------------------------------------------------------------------

    ///// <summary>
    ///// Creates and places a "Pieces" note-block schedule (based on the "GAIT - Piece Tag" family)
    ///// on the currently active unit drawing sheet, filtered to that sheet's tags via an "Origin"
    ///// schedule filter. Wired in GMS_tools.cs as the "Add Unit Schedule" command.
    ///// </summary>
    //[Transaction(TransactionMode.Manual)]
    //[Regeneration(RegenerationOption.Manual)]
    //public class AddSchedule : IExternalCommand
    //{
    //    public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
    //    {
    //        UIApplication uiApp = commandData.Application;
    //        Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
    //        Application app = doc.Application;
    //        UIApplication uiapp = new UIApplication(app);
    //        string sheetnumber = "";
    //
    //        try
    //        {
    //            // The active view must be a sheet (not a view on a sheet) — read its "Sheet Number".
    //            View view = doc.ActiveView;
    //            ElementId vId = view.Id;
    //            ParameterSet vPara = doc.GetElement(vId).Parameters;
    //            foreach (Parameter p in vPara)
    //            {
    //                if (p.Definition.Name.ToString() == "Sheet Number")
    //                {
    //                    sheetnumber = p.AsString();
    //                    if (!string.IsNullOrWhiteSpace(sheetnumber))
    //                    {
    //                        break;
    //                    }
    //                }
    //            }
    //
    //        }
    //        catch
    //        {
    //            TaskDialog.Show("AUS1 Error!", "Improper active view. To use this command, open the unit sheet to place the schedule on. Do not activate any views on the sheet.");
    //        }
    //
    //        // Only unit sheets (sheet numbers containing "U") get a piece schedule.
    //        if (!string.IsNullOrWhiteSpace(sheetnumber) && sheetnumber.Contains("U"))
    //        {
    //            using (Transaction t1 = new Transaction(doc, "Create Part Schedule"))
    //            {
    //                t1.Start();
    //
    //                // Find the "GAIT - Piece Tag" family among the families eligible for a note-block schedule.
    //                ICollection<ElementId> noteblockFamilies = ViewSchedule.GetValidFamiliesForNoteBlock(doc);
    //                foreach (ElementId ee in noteblockFamilies)
    //                {
    //                    Element field = doc.GetElement(ee);
    //                    string name = field.Name.ToString();
    //                    if (name == "GAIT - Piece Tag")
    //                    {
    //                        ViewSchedule vs = ViewSchedule.CreateNoteBlock(doc, ee);
    //                        doc.Regenerate();
    //
    //                        vs.Name = sheetnumber + " Pieces";
    //
    //                        // Locate the schedulable fields we want (Mark Number, Piece Description, Count, Origin) by name.
    //                        SchedulableField description = new SchedulableField();
    //                        SchedulableField count = new SchedulableField();
    //                        SchedulableField mrknum = new SchedulableField();
    //                        SchedulableField origin = new SchedulableField();
    //                        IList<SchedulableField> schedulableFields = vs.Definition.GetSchedulableFields();
    //                        foreach (SchedulableField sf in schedulableFields)
    //                        {
    //                            if (sf.GetName(doc) == "Piece Description")
    //                            {
    //                                description = sf;
    //                            }
    //                            if (sf.GetName(doc) == "Count")
    //                            {
    //                                count = sf;
    //                            }
    //                            if (sf.GetName(doc) == "Mark Number")
    //                            {
    //                                mrknum = sf;
    //                            }
    //                            if (sf.GetName(doc) == "Origin")
    //                            {
    //                                origin = sf;
    //                            }
    //                        }
    //                        vs.Definition.AddField(mrknum);
    //                        vs.Definition.AddField(description);
    //                        vs.Definition.AddField(count);
    //                        vs.Definition.AddField(origin);
    //
    //                        ScheduleFieldId markn = new ScheduleFieldId(0);
    //                        ScheduleFieldId ori = new ScheduleFieldId(3);
    //                        ScheduleFieldId desc = new ScheduleFieldId(1);
    //
    //
    //
    //                        // Re-resolve each field's actual ScheduleFieldId (added fields aren't guaranteed the ids assumed above).
    //                        int fieldcount = vs.Definition.GetFieldCount();
    //                        for (int i = 0; i < fieldcount; i++)
    //                        {
    //                            if (vs.Definition.GetField(i).GetName() == "Origin")
    //                            {
    //                                ori = vs.Definition.GetField(i).FieldId;
    //                            }
    //                            if (vs.Definition.GetField(i).GetName() == "Mark Number")
    //                            {
    //                                markn = vs.Definition.GetField(i).FieldId;
    //                            }
    //                            if (vs.Definition.GetField(i).GetName() == "Piece Description")
    //                            {
    //                                desc = vs.Definition.GetField(i).FieldId;
    //                            }
    //                        }
    //                        ScheduleSortGroupField marknfield = new ScheduleSortGroupField(markn, ScheduleSortOrder.Ascending);
    //                        vs.Definition.IsItemized = false;
    //                        vs.Definition.AddSortGroupField(marknfield);
    //                        // Filter rows to only this sheet's pieces (Origin == this sheet's number), then hide the Origin column.
    //                        ScheduleFilter orFilter = new ScheduleFilter(ori, ScheduleFilterType.Equal, sheetnumber);
    //                        vs.Definition.AddFilter(orFilter);
    //                        vs.Definition.GetField(ori).IsHidden = true;
    //                        vs.Definition.GetField(desc).GridColumnWidth = .125;
    //                        vs.Definition.ShowTitle = false;
    //
    //                        // Look up the "Schedule - Unit" text note type to use for the schedule's body/header text.
    //                        FilteredElementCollector txt = new FilteredElementCollector(doc).OfClass(typeof(TextNoteType));
    //                        string unitText = "";
    //                        string anyText = "";
    //                        foreach(Element t in txt)
    //                        {
    //                            if (t.Name.ToString() == "Schedule - Unit")
    //                            {
    //                                unitText = t.Id.ToString();
    //                            }
    //                            anyText = t.Id.ToString();
    //                        }
    //
    //                        if (string.IsNullOrWhiteSpace(unitText))
    //                        {
    //                            TaskDialog.Show("AUS2 Error!", "Text Note Style \"Schedule - Unit\" not found!");
    //                        }
    //                        else
    //                        {
    //                            long idInt = Convert.ToInt64(unitText);
    //                            ElementId id = new ElementId(idInt);
    //                            vs.BodyTextTypeId = id;
    //                            vs.HeaderTextTypeId = id;
    //                        }
    //
    //
    //                        // Place the new schedule on the active (unit) sheet at a fixed sheet-space location.
    //                        View view = doc.ActiveView;
    //                        ElementId vId = view.Id;
    //                        ElementId vsId = vs.Id;
    //                        ScheduleSheetInstance.Create(doc, vId, vsId, new XYZ(.925, .875, 0));
    //
    //                        break;
    //                    }
    //                }
    //                t1.Commit();
    //            }
    //
    //        }
    //        else
    //        {
    //            TaskDialog.Show("AUS3 Error!", "Improper active view. To use this command, open the unit sheet to place the schedule on. Do not activate any views on the sheet.");
    //        }
    //
    //        return Result.Succeeded;
    //    }
    //}
}
