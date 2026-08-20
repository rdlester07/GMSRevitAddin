using System;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace StockLengthOptimizer
{
    /// <summary>
    /// Ribbon entry point for the Stock Length Optimizer. Reads the
    /// "(DO NOT OPEN) Framing Stock Lengths" schedule and opens the optimizer form. We never edit
    /// the model, but use TransactionMode.Manual (NOT ReadOnly): reading schedule cells via
    /// GetTableData()/GetCellText can force Revit to regenerate the schedule, which ReadOnly mode
    /// blocks with "Changes are disabled for the active document". Manual doesn't pre-lock the doc
    /// and we simply open no transaction. Same mode ExportParts uses for the same read pattern.
    /// Wired in GMS_tools.cs as "StockLengthOptimizer.LaunchForm" (string is not compile-checked).
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchForm : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;

                ViewSchedule schedule = CutListReader.FindSchedule(doc);
                if (schedule == null)
                {
                    message = "Schedule not found: " + CutListReader.ScheduleName;
                    GMSRevitAddin.GmsUi.ShowError(message, "Stock Length Optimizer");
                    return Result.Failed;
                }

                CutListReader.CutList list = CutListReader.Read(schedule);

                using (var form = new StockOptimizerForm(list.Dies, list.FinishCount, list.Skipped))
                    form.ShowDialog(GMSRevitAddin.GmsUi.Owner);

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                GMSRevitAddin.GmsLog.Error("StockLengthOptimizer", ex);
                GMSRevitAddin.GmsUi.ShowError(ex, "Stock Length Optimizer Error");
                return Result.Failed;
            }
        }
    }
}
