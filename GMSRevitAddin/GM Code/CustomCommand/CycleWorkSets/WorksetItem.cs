namespace GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets
{
    /// <summary>
    /// UI-facing row for a single user workset in the cycler window's checkbox list. Built by
    /// <see cref="Helpers.WorksetHelpers.BuildWorksetItems"/> and bound to a WPF <c>CheckBox</c> via
    /// <c>WorksetCyclerWindow.LoadWorksets</c> (the checkbox's <c>Tag</c> holds the instance so
    /// <see cref="IsChecked"/> can be written back on toggle).
    /// </summary>
    public class WorksetItem
    {
        /// <summary>Display name shown in the checkbox list, including the " *CLOSED" suffix for closed worksets.</summary>
        public string Name { get; set; }
        /// <summary>The underlying Revit <see cref="Autodesk.Revit.DB.WorksetId"/> this row represents.</summary>
        public Autodesk.Revit.DB.WorksetId Id { get; set; }
        /// <summary>Current checked state, kept in sync with the bound checkbox as the user toggles it.</summary>
        public bool IsChecked { get; set; }
    }
}
