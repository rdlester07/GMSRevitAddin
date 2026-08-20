namespace GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets
{
    /// <summary>
    /// Process-wide storage for the cycler window's last screen position, so it reopens where the
    /// user left it instead of at a default location. Populated by
    /// <c>WorksetCyclerWindow</c>'s <c>Loaded</c>/<c>LocationChanged</c>/<c>Closed</c> handlers and
    /// read back on the next <c>Loaded</c>. Not persisted to disk — resets when Revit restarts.
    /// </summary>
    public static class Globals
    {
        /// <summary>Last known window X position (WPF device-independent units), or null if never moved.</summary>
        public static double? WorksetCyclerWindowLeft { get; set; }
        /// <summary>Last known window Y position (WPF device-independent units), or null if never moved.</summary>
        public static double? WorksetCyclerWindowTop { get; set; }
    }
}
