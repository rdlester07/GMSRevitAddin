using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets;
using System.Collections.Generic;

namespace GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets
{
    // ================================================================================================
    // == CycleWorksetsExternalEvent - Revit-safe execution of visibility + zoom
    // ================================================================================================
    /// <summary>
    /// <see cref="IExternalEventHandler"/> that applies the cycler window's selected workset
    /// visibility (and an optional zoom-to-fit) to <see cref="CycleWorksetsController.ActView"/>.
    /// Revit's API is single-threaded, so the WPF window cannot call the API directly from its own
    /// checkbox/button callbacks — it instead calls <see cref="CycleWorksetsController.RequestUpdate"/>,
    /// which raises the shared <see cref="ExternalEvent"/> wrapping this handler, and Revit invokes
    /// <see cref="Execute"/> on its own API thread at the next opportunity.
    /// </summary>
    public class CycleWorksetsExternalEvent : IExternalEventHandler
    {
        // ============================================================================================
        // == Execute
        // ============================================================================================
        /// <summary>
        /// Runs on Revit's API thread. Sets each workset's visibility on
        /// <see cref="CycleWorksetsController.ActView"/> according to
        /// <see cref="CycleWorksetsController.SelectedWorksetNames"/>, then, if
        /// <see cref="CycleWorksetsController.PendingZoom"/> is set, zooms the open view to fit and
        /// clears the flag. No-ops if the controller's context (document/view/worksets) isn't set.
        /// </summary>
        public void Execute(UIApplication uiApp)
        {
            // debug logging removed
            // Guard: ensure controller has valid context
            if (CycleWorksetsController.Doc == null ||
                CycleWorksetsController.ActView == null ||
                CycleWorksetsController.Worksets == null)
            {
                // debug logging removed
                return;
            }

            UIDocument uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null) return;

            Document doc = CycleWorksetsController.Doc;
            View view = CycleWorksetsController.ActView;

            using (Transaction tx = new Transaction(doc, "Workset Visibility Change"))
            {
                tx.Start();

                // Apply visibility based on selected workset names
                HashSet<string> selectedNames = new HashSet<string>(CycleWorksetsController.SelectedWorksetNames);

                foreach (var kvp in CycleWorksetsController.Worksets)
                {
                    // Checked worksets become Visible, everything else Hidden — this is a full
                    // re-apply each raise, not an incremental diff.
                    bool isSelected = selectedNames.Contains(kvp.Key);
                    view.SetWorksetVisibility(kvp.Value, isSelected ? WorksetVisibility.Visible : WorksetVisibility.Hidden);
                }

                // Optional zoom if requested
                if (CycleWorksetsController.PendingZoom)
                {
                    // Find the open UIView for the target view — SetWorksetVisibility works on the
                    // View element, but zooming requires the corresponding open UIView.
                    UIView uiView = null;
                    var uiViews = uiDoc.GetOpenUIViews();
                    foreach (UIView uv in uiViews)
                    {
                        if (uv.ViewId == view.Id)
                        {
                            uiView = uv;
                            break;
                        }
                    }

                    if (uiView != null)
                    {
                        uiView.ZoomToFit();
                    }

                    // Reset zoom flag
                    CycleWorksetsController.PendingZoom = false;
                }

                tx.Commit();
            }
        }

        // ============================================================================================
        // == GetName
        // ============================================================================================
        /// <summary>Display name Revit uses for this handler (e.g. in error/debug UI).</summary>
        public string GetName()
        {
            return "Cycle Worksets External Event";
        }
    }
}
