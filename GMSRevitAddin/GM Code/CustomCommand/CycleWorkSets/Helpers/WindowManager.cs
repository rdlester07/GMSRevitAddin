using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB;
using Autodesk.Revit.DB.Events;
using Autodesk.Revit.UI;
using Autodesk.Revit.UI.Events;
using GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets;
using GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.UI;
using System.Collections.Generic;

namespace GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers
{
    // ================================================================================================
    // == WindowManager - Single-instance WPF window + Revit event wiring
    // ================================================================================================
    /// <summary>
    /// Owns the single <c>WorksetCyclerWindow</c> instance for the Revit session and the
    /// application-level events that keep it in sync with what the user is looking at: it shows/
    /// activates or hides the window, and, while docked to the dedicated cycler 3D view, rebuilds the
    /// workset list on every <c>ViewActivated</c>. Registered once from <c>OnStartup</c> via
    /// <see cref="RegisterApplicationEvents"/> and torn down via <see cref="UnregisterApplicationEvents"/>.
    /// </summary>
    public static class WindowManager
    {
        // ============================================================================================
        // == Fields
        // ============================================================================================
        private static WorksetCyclerWindow _window = null;
        private static UIControlledApplication _uiControlledApp = null;
        private static bool _eventsRegistered = false;

        // ============================================================================================
        // == ShowOrActivateWindow
        // ============================================================================================
        /// <summary>
        /// Creates the single <c>WorksetCyclerWindow</c> instance on first call (subsequent calls
        /// reuse it), loads it with <paramref name="worksetItems"/>, and shows it or, if already
        /// visible, brings it to the front. Safe to call from any thread — marshals onto the window's
        /// dispatcher if needed.
        /// </summary>
        public static void ShowOrActivateWindow(List<WorksetItem> worksetItems)
        {
            // Ensure UI operations happen on the window's dispatcher (UI thread)
            if (_window == null)
            {
                // Create on current thread and wire close
                _window = new WorksetCyclerWindow();
                _window.Closed += (s, e) => { _window = null; };
            }

            void showAction()
            {
                _window.LoadWorksets(worksetItems);
                if (!_window.IsVisible)
                    _window.Show();
                else
                    _window.Activate();
            }

            if (!_window.Dispatcher.CheckAccess())
                _window.Dispatcher.Invoke((System.Action)showAction);
            else
                showAction();
        }

        // ============================================================================================
        // == HideWindow
        // ============================================================================================
        /// <summary>Hides the cycler window if it exists and is currently visible. No-op if the
        /// window was never created. Safe to call from any thread.</summary>
        public static void HideWindow()
        {
            if (_window == null)
                return;

            void hideAction()
            {
                if (_window.IsVisible)
                    _window.Hide();
            }

            if (!_window.Dispatcher.CheckAccess())
                _window.Dispatcher.Invoke((System.Action)hideAction);
            else
                hideAction();
        }

        // ============================================================================================
        // == RegisterApplicationEvents (called from Application.OnStartup)
        // ============================================================================================
        /// <summary>
        /// Wires the application-level Revit events the cycler window depends on
        /// (<c>ViewActivated</c>, <c>DocumentClosing</c>, <c>DocumentOpened</c>). Idempotent — a
        /// second call is a no-op until <see cref="UnregisterApplicationEvents"/> runs.
        /// </summary>
        public static void RegisterApplicationEvents(UIControlledApplication app)
        {
            if (_eventsRegistered)
                return;

            _uiControlledApp = app;

            // View activation — drives show/hide/refresh of the cycler window based on which view
            // the user is now looking at (see OnViewActivated).
            _uiControlledApp.ViewActivated += OnViewActivated;

            // Document events — the cycler window's state is document-specific, so hide it whenever
            // the active document changes (a new one opens, or the current one is closing).
            _uiControlledApp.ControlledApplication.DocumentClosing += OnDocumentClosing;
            _uiControlledApp.ControlledApplication.DocumentOpened += OnDocumentOpened;

            _eventsRegistered = true;
        }

        // ============================================================================================
        // == UnregisterApplicationEvents (called from Application.OnShutdown)
        // ============================================================================================
        /// <summary>Detaches the events wired by <see cref="RegisterApplicationEvents"/>. No-op if
        /// events were never registered.</summary>
        public static void UnregisterApplicationEvents(UIControlledApplication app)
        {
            if (!_eventsRegistered || _uiControlledApp == null)
                return;

            _uiControlledApp.ViewActivated -= OnViewActivated;
            _uiControlledApp.ControlledApplication.DocumentClosing -= OnDocumentClosing;
            _uiControlledApp.ControlledApplication.DocumentOpened -= OnDocumentOpened;

            _eventsRegistered = false;
            _uiControlledApp = null;
        }

        // ============================================================================================
        // == OnViewActivated - Hide when leaving special 3D view, refresh when entering
        // ============================================================================================
        /// <summary>
        /// Keeps the cycler window scoped to its dedicated per-user 3D view: hides it whenever the
        /// newly activated view is a sheet (or was reached by leaving a sheet) or simply isn't the
        /// target view, and, when the target view is (re)entered, re-initializes
        /// <see cref="CycleWorksetsController"/> for the new context and rebuilds/shows the workset
        /// list so it always reflects the current document's worksets.
        /// </summary>
        private static void OnViewActivated(object sender, ViewActivatedEventArgs e)
        {
            string username = System.Environment.UserName;
            string targetViewName = "Workset Cycle 3D View - " + username;

            View newView = e.CurrentActiveView;
            View prevView = e.PreviousActiveView;

            // If the user activated a sheet, always hide the cycler UI.
            if (newView is ViewSheet)
            {
                HideWindow();
                return;
            }

            // If the user just left a sheet (previous view was a sheet) and is not entering the special 3D view,
            // hide the cycler UI as well.
            if (prevView is ViewSheet && (newView == null || newView.Name != targetViewName))
            {
                HideWindow();
                return;
            }

            // If no view or wrong view → hide
            if (newView == null || newView.Name != targetViewName)
            {
                HideWindow();
                return;
            }

            // If window exists and we are in the correct view → refresh it
            if (_window != null)
            {
                // USE THE REAL UIApplication STORED BY THE COMMAND
                UIApplication uiApp = CycleWorksetsController.UiApp;

                if (uiApp == null)
                {
                    // Safety fallback: cannot refresh without a valid UIApplication
                    HideWindow();
                    return;
                }

                Document doc = e.Document;

                // Reinitialize controller with correct UI context
                CycleWorksetsController.Initialize(uiApp, doc);

                // Set active view
                CycleWorksetsController.ActView = newView;

                // Rebuild workset items
                var worksetItems = WorksetHelpers.BuildWorksetItems(
                    CycleWorksetsController.Doc,
                    CycleWorksetsController.ActView,
                    out var worksetMap,
                    out var longestName);

                CycleWorksetsController.Worksets = worksetMap;
                CycleWorksetsController.LongestWorksetName = longestName;

                if (worksetItems.Count > 0)
                    ShowOrActivateWindow(worksetItems);
                else
                    HideWindow();
            }
        }


        // ============================================================================================
        // == OnDocumentOpened - Hide window when switching projects
        // ============================================================================================
        /// <summary>Hides the cycler window when a document opens — its worksets/view context belong
        /// to whichever document it was last shown for, which is now stale.</summary>
        private static void OnDocumentOpened(object sender, DocumentOpenedEventArgs e)
        {
            HideWindow();
        }

        // ============================================================================================
        // == OnDocumentClosing - Hide window when closing project
        // ============================================================================================
        /// <summary>Hides the cycler window before its document closes, so it doesn't linger showing
        /// stale/invalid state.</summary>
        private static void OnDocumentClosing(object sender, DocumentClosingEventArgs e)
        {
            HideWindow();
        }
    }
}
