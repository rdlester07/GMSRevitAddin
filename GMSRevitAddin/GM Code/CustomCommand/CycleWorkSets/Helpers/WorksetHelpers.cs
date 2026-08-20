using Autodesk.Revit.DB;
using GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets;
using System.Collections.Generic;
using System.Linq;

namespace GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers
{
    // ================================================================================================
    // == WorksetHelpers - collect and format worksets for UI
    // ================================================================================================
    /// <summary>Builds the cycler window's checkbox list from a document's user worksets.</summary>
    public static class WorksetHelpers
    {
        // ============================================================================================
        // == BuildWorksetItems
        // ============================================================================================
        /// <summary>
        /// Collects every user workset (<see cref="WorksetKind.UserWorkset"/>) in <paramref name="doc"/>,
        /// appending " *CLOSED" to the display name of any that are closed, and returns them as
        /// alphabetically sorted <see cref="WorksetItem"/> rows. Each row's initial
        /// <see cref="WorksetItem.IsChecked"/> is restored from
        /// <see cref="CycleWorksetsController.SelectedWorksetNames"/> if the controller has a saved
        /// selection, otherwise defaults to unchecked. Also returns, via <paramref name="worksetMap"/>,
        /// the display-name→<see cref="WorksetId"/> lookup the external event uses to apply visibility,
        /// and, via <paramref name="longestName"/>, the longest display name seen (for UI sizing).
        /// The <paramref name="view"/> parameter is currently unused by this method.
        /// </summary>
        public static List<WorksetItem> BuildWorksetItems(
            Document doc,
            View view,
            out Dictionary<string, WorksetId> worksetMap,
            out string longestName)
        {
            worksetMap = new Dictionary<string, WorksetId>(); // Refreshing file after WorksetItem creation
            longestName = string.Empty;
            List<WorksetItem> items = new List<WorksetItem>();

            FilteredWorksetCollector coll = new FilteredWorksetCollector(doc)
                .OfKind(WorksetKind.UserWorkset);

            int maxLen = 0;

            foreach (Workset ws in coll)
            {
                string name = ws.Name;

                if (!ws.IsOpen)
                {
                    // Closed worksets can still have their visibility toggled per-view, so they stay
                    // in the list — the suffix just flags their closed state to the user.
                    name += " *CLOSED";
                }

                worksetMap[name] = ws.Id;

                bool isChecked;
                if (CycleWorksetsController.HasSavedSelection)
                {
                    // Restore the user's prior checkbox selection (e.g. after a window rebuild)
                    // rather than defaulting everything unchecked.
                    isChecked = CycleWorksetsController.SelectedWorksetNames.Contains(name);
                }
                else
                {
                    isChecked = false;
                }

                items.Add(new WorksetItem
                {
                    Name = name,
                    Id = ws.Id,
                    IsChecked = isChecked
                });

                if (name.Length > maxLen)
                {
                    maxLen = name.Length;
                    longestName = name;
                }
            }

            // Sort alphabetically for UI
            items = items.OrderBy(i => i.Name).ToList();

            return items;
        }
    }
}
