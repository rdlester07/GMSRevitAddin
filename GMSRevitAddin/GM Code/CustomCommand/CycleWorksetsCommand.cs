using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers;

namespace GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets
{
    // ================================================================================================
    // == CycleWorksetsCommand - IExternalCommand entry point
    // ================================================================================================
    /// <summary>
    /// Ribbon entry point for the workset visibility cycler. Ensures the dedicated "Workset Cycle 3D
    /// View" exists and is active, builds the checkbox list from the document's user worksets, wires
    /// the <see cref="CycleWorksetsExternalEvent"/>, and shows (or activates) the WPF
    /// <c>WorksetCyclerWindow</c> via <see cref="Helpers.WindowManager"/>.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class CycleWorksetsCommand : IExternalCommand
    {
        // ============================================================================================
        // == Execute
        // ============================================================================================
        /// <summary>
        /// Initializes the <see cref="CycleWorksetsController"/> with the active document, ensures the
        /// special 3D view and the <see cref="ExternalEvent"/> exist, collects the user worksets, and
        /// shows the cycler window. Returns <see cref="Result.Cancelled"/> if there is no active
        /// document, or <see cref="Result.Succeeded"/> (with a dialog) if the project has no user
        /// worksets to cycle.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            UIDocument uiDoc = uiApp.ActiveUIDocument;

            // debug logging removed

            if (uiDoc == null || uiDoc.Document == null)
            {
                message = "No active document.";
                return Result.Cancelled;
            }

            Document doc = uiDoc.Document;

            try
            {
                // ====================================================================================
                // == Initialize controller with current Revit context
                // ====================================================================================
                CycleWorksetsController.Initialize(uiApp, doc);

                // (Optional safety: ensure UiApp is always set even if Initialize changes later)
                CycleWorksetsController.UiApp = uiApp;

                // ====================================================================================
                // == Ensure the special 3D view exists and is active
                // ====================================================================================
                CycleWorksetsController.EnsureWorksetCycleView();

                // ====================================================================================
                // == Collect worksets and build UI items
                // ====================================================================================
                var worksetItems = GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers.WorksetHelpers.BuildWorksetItems(
                    CycleWorksetsController.Doc,
                    CycleWorksetsController.ActView,
                    out var worksetMap,
                    out var longestName);

                // Store workset map and longest string in controller
                CycleWorksetsController.Worksets = worksetMap;
                CycleWorksetsController.LongestWorksetName = longestName;

                DebugLogger.Log($"Found {worksetItems.Count} workset items");
                if (worksetItems.Count == 0)
                {
                    TaskDialog.Show("Worksets", "No user worksets found in project.");
                    DebugLogger.Log("No user worksets found - exiting");
                    return Result.Succeeded;
                }

                // ====================================================================================
                // == Ensure ExternalEvent exists
                // ====================================================================================
                CycleWorksetsController.EnsureExternalEvent();

                // ====================================================================================
                // == Show or activate the WPF window
                // ====================================================================================
                GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers.WindowManager.ShowOrActivateWindow(worksetItems);
            }
            catch (Exception ex)
            {
                // debug logging removed
                TaskDialog.Show("Worksets - Error", ex.Message);
                return Result.Failed;
            }
            // debug logging removed
            return Result.Succeeded;
        }
    }
}
