using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Collections.Generic;
using GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers;
using System;

namespace GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets
{
    // ================================================================================================
    // == CycleWorksetsController - central state + orchestration
    // ================================================================================================
    /// <summary>
    /// Static holder for the workset cycler's Revit context (UIApplication/Document/active view),
    /// the current workset name→<see cref="WorksetId"/> map, the user's checkbox selection, and the
    /// shared <see cref="ExternalEvent"/> used to marshal visibility/zoom changes onto Revit's API
    /// thread. Both the command and <see cref="Helpers.WindowManager"/> read/write this shared state
    /// (there is one cycler window per Revit session, so a static controller is sufficient).
    /// </summary>
    public static class CycleWorksetsController
    {
        // ============================================================================================
        // == Revit context
        // ============================================================================================
        /// <summary>The current UIApplication, captured by <see cref="Initialize"/>. Setter is public so
        /// callers (and the command) can keep it current after re-initializing.</summary>
        public static UIApplication UiApp { get; set; }          // <-- setter now public
        /// <summary>The active UIDocument as of the last <see cref="Initialize"/> call.</summary>
        public static UIDocument UiDoc { get; private set; }
        /// <summary>The active Document as of the last <see cref="Initialize"/> call.</summary>
        public static Document Doc { get; private set; }
        /// <summary>The dedicated "Workset Cycle 3D View" that the cycler window controls.</summary>
        public static View ActView { get; set; }

        // ============================================================================================
        // == Workset data
        // ============================================================================================
        /// <summary>Maps each displayed workset name (including the " *CLOSED" suffix, if applicable) to
        /// its <see cref="WorksetId"/>. Rebuilt whenever the workset list is (re)loaded.</summary>
        public static Dictionary<string, WorksetId> Worksets { get; set; } = new Dictionary<string, WorksetId>();
        /// <summary>The longest workset display name seen, used by the UI for sizing.</summary>
        public static string LongestWorksetName { get; set; } = string.Empty;

        // ============================================================================================
        // == Selection + behavior flags
        // ============================================================================================
        /// <summary>Names of the worksets currently checked in the cycler window.</summary>
        public static List<string> SelectedWorksetNames { get; private set; } = new List<string>();
        /// <summary>True once the user has made a selection in this session, so a later window rebuild
        /// can restore checkbox state instead of defaulting everything unchecked.</summary>
        public static bool HasSavedSelection { get; private set; } = false;
        /// <summary>Set true to have the next <see cref="ExternalEvent"/> raise also zoom-to-fit the view;
        /// consumed (and reset to false) by <see cref="CycleWorksetsExternalEvent.Execute"/>.</summary>
        public static bool PendingZoom { get; set; } = false;

        // ============================================================================================
        // == ExternalEvent
        // ============================================================================================
        /// <summary>The shared external event that applies workset visibility (and optional zoom) on
        /// Revit's API thread. Created lazily by <see cref="EnsureExternalEvent"/>.</summary>
        public static ExternalEvent ExternalEvent { get; private set; }

        // ============================================================================================
        // == Initialize
        // ============================================================================================
        /// <summary>Captures the current UIApplication/UIDocument/Document into the controller's static
        /// fields. Called by the command on launch and again by <see cref="Helpers.WindowManager"/> on
        /// each view activation into the cycler view, to keep the context fresh.</summary>
        public static void Initialize(UIApplication uiApp, Document doc)
        {
            UiApp = uiApp;
            UiDoc = uiApp.ActiveUIDocument;
            Doc = doc;
        }

        // ============================================================================================
        // == EnsureWorksetCycleView - create or reuse the special 3D view
        // ============================================================================================
        /// <summary>
        /// Ensures a per-user "Workset Cycle 3D View - &lt;username&gt;" exists and is the active view,
        /// setting <see cref="ActView"/>. If the active view is already the target view, or an existing
        /// view by that name is found, it is reused (and activated); otherwise a new isometric 3D view is
        /// created via <see cref="ViewHelpers.CreateNew3DView"/> and prepared via
        /// <see cref="ViewHelpers.Prepare3DView"/>.
        /// </summary>
        public static void EnsureWorksetCycleView()
        {
            string username = System.Environment.UserName;
            string viewName = "Workset Cycle 3D View - " + username;

            View activeView = UiDoc.ActiveView;
            if (activeView != null && activeView.Name == viewName)
            {
                ActView = activeView;
                return;
            }

            // Try to find existing view
            FilteredElementCollector viewFec = new FilteredElementCollector(Doc)
                .OfClass(typeof(View));

            foreach (View v in viewFec)
            {
                if (!v.IsTemplate && v.Name == viewName)
                {
                    ActView = v;
                    UiDoc.ActiveView = v;
                    return;
                }
            }

            // Create new 3D view and prepare it
            View newView = ViewHelpers.CreateNew3DView(Doc, viewName);
            ViewHelpers.Prepare3DView(Doc, newView, UiDoc);
            ActView = newView;
        }

        // ============================================================================================
        // == EnsureExternalEvent
        // ============================================================================================
        /// <summary>Lazily creates the shared <see cref="ExternalEvent"/> (wrapping a new
        /// <see cref="CycleWorksetsExternalEvent"/> handler) if one doesn't already exist. Safe to call
        /// repeatedly — a no-op once the event has been created.</summary>
        public static void EnsureExternalEvent()
        {
            if (ExternalEvent == null)
            {
                // ExternalEvent.Create must run on Revit's API thread (i.e. from the command), and only
                // needs to happen once per session — the same handler instance is reused thereafter.
                var handler = new CycleWorksetsExternalEvent();
                ExternalEvent = ExternalEvent.Create(handler);
            }
        }

        // ============================================================================================
        // == UpdateSelectionFromUI
        // ============================================================================================
        /// <summary>Replaces <see cref="SelectedWorksetNames"/> with the given names (from the cycler
        /// window's checked checkboxes) and marks <see cref="HasSavedSelection"/> true.</summary>
        public static void UpdateSelectionFromUI(IEnumerable<string> selectedNames)
        {
            SelectedWorksetNames.Clear();
            SelectedWorksetNames.AddRange(selectedNames);
            HasSavedSelection = true;
        }

        // ============================================================================================
        // == ResetSavedSelection
        // ============================================================================================
        /// <summary>Clears <see cref="SelectedWorksetNames"/> and resets <see cref="HasSavedSelection"/>
        /// to false, so the next window build defaults every checkbox unchecked.</summary>
        public static void ResetSavedSelection()
        {
            SelectedWorksetNames.Clear();
            HasSavedSelection = false;
        }

        // ============================================================================================
        // == RequestUpdate - visibility only or visibility + zoom
        // ============================================================================================
        /// <summary>
        /// Requests that the current <see cref="SelectedWorksetNames"/> selection be applied to
        /// <see cref="ActView"/>'s workset visibility, optionally followed by a zoom-to-fit. Sets
        /// <see cref="PendingZoom"/> and raises the shared <see cref="ExternalEvent"/> so the actual
        /// Revit API calls happen on Revit's thread via <see cref="CycleWorksetsExternalEvent.Execute"/>.
        /// </summary>
        public static void RequestUpdate(bool zoomAlso)
        {
            PendingZoom = zoomAlso;

            if (ExternalEvent != null)
            {
                // Raise() queues the handler to run on Revit's API thread at the next opportunity;
                // it does not execute synchronously.
                ExternalEvent.Raise();
            }
        }
    }

    // DebugLogger now delegates to the shared GmsLog facility so its existing call sites
    // actually record to the add-in log instead of being silently dropped.
    /// <summary>
    /// Thin compatibility shim over <see cref="GMSRevitAddin.GmsLog"/> that preserves the legacy
    /// Init/Log/Close call sites used throughout this module. <see cref="Log"/> forwards to
    /// <see cref="GMSRevitAddin.GmsLog.Info"/>; <see cref="Init"/> and <see cref="Close"/> are no-ops
    /// because <c>GmsLog</c> creates and manages its log file lazily on its own.
    /// </summary>
    public static class DebugLogger
    {
        /// <summary>No-op: <see cref="GMSRevitAddin.GmsLog"/> creates its file lazily on first write.</summary>
        public static void Init() { /* no-op: GmsLog creates its file lazily */ }
        /// <summary>Forwards to <see cref="GMSRevitAddin.GmsLog.Info"/>.</summary>
        public static void Log(string msg) { GMSRevitAddin.GmsLog.Info(msg); }
        /// <summary>No-op: nothing to close since <see cref="GMSRevitAddin.GmsLog"/> owns its own file handle.</summary>
        public static void Close() { /* no-op */ }
    }
}
