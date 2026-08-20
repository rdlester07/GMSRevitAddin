using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Visual;
using System.Windows.Forms;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ProgressBar
{
    /// <summary>
    /// Writes progress text directly into Revit's own status bar (the native Win32 status bar
    /// control at the bottom of the main window), as an alternative to a separate progress dialog.
    /// </summary>
    public class UpdateProgress
    {
        // Sets the text of a native window (used here to set the status bar's caption text).
        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        static extern int SetWindowText(IntPtr hWnd, string lpString);

        // Finds a child window by class name (used here to locate Revit's status bar control).
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string lpszClass, string lpszWindow);

        //[DllImport("user32.dll", SetLastError = true)]
        //static extern int SetProgressBar(IntPtr hwnd, )

        /// <summary>
        /// Sets the text shown in Revit's status bar. No-op if the Revit process or its status
        /// bar control cannot be found (e.g. running headless).
        /// </summary>
        public static void SetStatusText(string text)
        {
            // Locate the running Revit process to get its main window handle.
            Process[] processes = Process.GetProcessesByName("Revit");

            if (0 < processes.Length)
            {
                // Find the native Win32 status bar control (class "msctls_statusbar32") within
                // Revit's main window.
                IntPtr statusBar = FindWindowEx(
                  processes[0].MainWindowHandle, IntPtr.Zero,
                  "msctls_statusbar32", "");

                if (statusBar != IntPtr.Zero)
                {
                    SetWindowText(statusBar, text);
                }
            }
        }
    }
}