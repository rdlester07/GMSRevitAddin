namespace GMSRevitAddin
{
    /// <summary>
    /// Centralized file-system locations used across the add-in.
    ///
    /// Values are unchanged from the literals that were previously hard-coded in individual
    /// commands; collecting them here means an environment change (server rename, drive
    /// remap, install-path change) is a single edit rather than a hunt through the codebase.
    /// </summary>
    public static class GmsPaths
    {
        /// <summary>Root of the company file-server share.</summary>
        public const string CommonShare = @"\\gmsfs01\Common";

        /// <summary>Project files on the share (also mapped to the J: drive on workstations).</summary>
        public const string ProjectsFolder = CommonShare + @"\Projects\";

        /// <summary>Extrusion database (Microsoft Access .accdb) on the share.</summary>
        public const string ExtrusionDatabase = CommonShare + @"\Databases\GMS Extrusion Database.accdb";

        /// <summary>Root folder for per-user unit PDF exports; a user name is appended.</summary>
        public const string PdfOutputRoot = CommonShare + @"\Files\PDF_Output\";

        /// <summary>Mapped-drive root used for piece-extraction output (equivalent to ProjectsFolder).</summary>
        public const string ExportRoot = @"J:\";

        /// <summary>
        /// Local install root for the add-in's support files, e.g. <c>C:\GMS\Revit\2025</c>.
        /// Version-specific so each Revit version's install is isolated; the version comes from
        /// <see cref="GmsVersion.Number"/> (baked at build time). No longer a <c>const</c>
        /// because the value is computed at runtime.
        /// </summary>
        public static readonly string GmsRevitRoot = @"C:\GMS\Revit\" + GmsVersion.Number;

        /// <summary>Compiled help file shipped with the add-in.</summary>
        public static readonly string HelpFile = GmsRevitRoot + @"\GMS Help Information.chm";

        /// <summary>Text file listing standard piece descriptions.</summary>
        public static readonly string PieceDescriptionsFile = GmsRevitRoot + @"\Addins\PieceDescriptions.txt";
    }
}
