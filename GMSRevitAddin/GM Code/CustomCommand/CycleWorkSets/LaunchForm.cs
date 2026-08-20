using System;
using System.IO;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;

// Compatibility entrypoint expected by older .addin manifests.
// Provides a class `CycleWorksets.LaunchForm` that implements IExternalApplication
// and exposes legacy static handlers (onViewChange, onDocumentOpen, onDocumentClose).
// This allows older .addin files that reference the CycleWorksets.LaunchForm type
// to successfully load the add-in while forwarding behavior to the new WindowManager/DebugLogger.

namespace CycleWorksets
{
    /// <summary>
    /// Legacy compatibility shim (see file header). Implements both <see cref="IExternalApplication"/>
    /// and <see cref="IExternalCommand"/> so an older .addin manifest that points at
    /// <c>CycleWorksets.LaunchForm</c> — whether registered as an application or as a command — still
    /// loads and behaves correctly, by forwarding everything to the current
    /// <see cref="GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers.WindowManager"/> and
    /// <see cref="GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.CycleWorksetsCommand"/>.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class LaunchForm : IExternalApplication, IExternalCommand
    {
        /// <summary>
        /// Registers the current <see cref="Helpers.WindowManager"/> application-level event handlers
        /// (view activation, document open/close) so the cycler window shows/hides correctly even when
        /// loaded under this legacy entry point. Also ensures the version-specific Addins folder exists,
        /// as a load confirmation side effect. Swallows and logs any failure rather than throwing, so a
        /// startup error here can't take down the rest of the add-in.
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
            try
            {
                // Register events via the new WindowManager
                GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers.WindowManager.RegisterApplicationEvents(application);
                GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.DebugLogger.Log("LaunchForm.OnStartup: Registered WindowManager events");
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("LaunchForm", __ex); }

            // Unconditional indicator file write to Revit Addins folder to confirm assembly loaded
            try
            {
                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string addinsDir = Path.Combine(appData, "Autodesk", "Revit", "Addins", GMSRevitAddin.GmsVersion.Number);
                if (!Directory.Exists(addinsDir))
                {
                    Directory.CreateDirectory(addinsDir);
                }
                // indicator file creation removed
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("LaunchForm", __ex); }

            return Result.Succeeded;
        }

        /// <summary>Unregisters the <see cref="Helpers.WindowManager"/> events and closes the debug logger.</summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            try
            {
                GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers.WindowManager.UnregisterApplicationEvents(application);
                GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.DebugLogger.Log("LaunchForm.OnShutdown: Unregistered WindowManager events");
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("LaunchForm", __ex); }

            try { GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.DebugLogger.Close(); } catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("LaunchForm", __ex); }
            return Result.Succeeded;
        }

        // Legacy static handlers used by existing code / .addin hooks
        /// <summary>Legacy view-activation hook, kept for older call sites; only logs (the real
        /// behavior now lives in <see cref="Helpers.WindowManager"/>'s own <c>ViewActivated</c> handler).</summary>
        public static void onViewChange(object sender, ViewActivatedEventArgs e)
        {
            try { GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.DebugLogger.Log("LaunchForm.onViewChange invoked"); } catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("LaunchForm", __ex); }
        }

        /// <summary>Legacy document-opened hook: hides the cycler window (switching projects invalidates its state).</summary>
        public static void onDocumentOpen(object sender, DocumentOpenedEventArgs e)
        {
            try
            {
                GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.DebugLogger.Log("LaunchForm.onDocumentOpen invoked - hiding window");
                GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers.WindowManager.HideWindow();
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("LaunchForm", __ex); }
        }

        /// <summary>Legacy document-closing hook: hides the cycler window before the document goes away.</summary>
        public static void onDocumentClose(object sender, DocumentClosingEventArgs e)
        {
            try
            {
                GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.DebugLogger.Log("LaunchForm.onDocumentClose invoked - hiding window");
                GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers.WindowManager.HideWindow();
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("LaunchForm", __ex); }
        }

        // Allow .addin manifests that point to CycleWorksets.LaunchForm to work as an ExternalCommand
        /// <summary>
        /// Forwards execution to a fresh <see cref="GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.CycleWorksetsCommand"/>
        /// so a manifest still wired to this legacy class name gets the current command behavior.
        /// Any exception is logged, shown in a <see cref="TaskDialog"/>, and turned into
        /// <see cref="Result.Failed"/> rather than propagating.
        /// </summary>
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.DebugLogger.Init();
                GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.DebugLogger.Log("LaunchForm.Execute invoked");
                var cmd = new GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.CycleWorksetsCommand();
                return cmd.Execute(commandData, ref message, elements);
            }
            catch (System.Exception ex)
            {
                try { GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.DebugLogger.Log("LaunchForm.Execute exception: " + ex.ToString()); } catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("LaunchForm", __ex); }
                try { TaskDialog.Show("Cycle Worksets", ex.Message); } catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("LaunchForm", __ex); }
                return Result.Failed;
            }
        }
    }
}
