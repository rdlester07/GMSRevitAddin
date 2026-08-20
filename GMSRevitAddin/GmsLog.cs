using System;
using System.IO;

namespace GMSRevitAddin
{
    /// <summary>
    /// Lightweight, thread-safe logging facility for the whole add-in.
    /// Writes daily rolling log files under %LOCALAPPDATA%\GMS\Revit&lt;version&gt;\Logs
    /// (e.g. Revit2025, Revit2026), keeping each Revit version's logs separate.
    ///
    /// Logging must never break a command, so every method swallows its own I/O
    /// errors. Use <see cref="Error(string, Exception)"/> from catch blocks instead of
    /// silently swallowing exceptions.
    /// </summary>
    public static class GmsLog
    {
        private static readonly object _gate = new object();

        private static string LogDirectory
        {
            get
            {
                return Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "GMS", "Revit" + GmsVersion.Number, "Logs");
            }
        }

        private static string LogFilePath
        {
            get { return Path.Combine(LogDirectory, "GMSRevitAddin_" + DateTime.Now.ToString("yyyyMMdd") + ".log"); }
        }

        /// <summary>Full path to today's log file (useful for "show log" links / error dialogs).</summary>
        public static string CurrentLogFile
        {
            get { return LogFilePath; }
        }

        /// <summary>Write an informational line to today's log file.</summary>
        public static void Info(string message)
        {
            Write("INFO", message);
        }

        /// <summary>Write a warning line to today's log file (non-fatal, unexpected condition).</summary>
        public static void Warn(string message)
        {
            Write("WARN", message);
        }

        /// <summary>Write an error line to today's log file.</summary>
        public static void Error(string message)
        {
            Write("ERROR", message);
        }

        /// <summary>
        /// Log a caught exception with a short context describing what was being attempted.
        /// </summary>
        public static void Error(string context, Exception ex)
        {
            string detail = ex == null ? "(null exception)" : ex.ToString();
            Write("ERROR", (string.IsNullOrEmpty(context) ? "" : context + " -> ") + detail);
        }

        private static void Write(string level, string message)
        {
            try
            {
                lock (_gate)
                {
                    // Ensure the (version-specific) log folder exists before every write - it may
                    // not exist yet on a fresh machine/user profile.
                    Directory.CreateDirectory(LogDirectory);
                    string line = string.Format(
                        "{0:yyyy-MM-dd HH:mm:ss.fff} [{1}] [{2}] {3}{4}",
                        DateTime.Now, level, Environment.UserName, message, Environment.NewLine);
                    File.AppendAllText(LogFilePath, line);
                }
            }
            catch
            {
                // Logging is best-effort; never let a logging failure propagate into a command.
            }
        }
    }
}
