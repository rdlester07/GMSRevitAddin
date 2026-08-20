#region Header
// Revit MEP API sample application
//
// Copyright (C) 2007-2010 by Jeremy Tammik, Autodesk, Inc.
//
// Permission to use, copy, modify, and distribute this software
// for any purpose and without fee is hereby granted, provided
// that the above copyright notice appears in all copies and
// that both that copyright notice and the limited warranty and
// restricted rights notice below appear in all supporting
// documentation.
//
// AUTODESK PROVIDES THIS PROGRAM "AS IS" AND WITH ALL FAULTS.
// AUTODESK SPECIFICALLY DISCLAIMS ANY IMPLIED WARRANTY OF
// MERCHANTABILITY OR FITNESS FOR A PARTICULAR USE.  
// AUTODESK, INC. DOES NOT WARRANT THAT THE OPERATION OF THE 
// PROGRAM WILL BE UNINTERRUPTED OR ERROR FREE.
//
// Use, duplication, or disclosure by the U.S. Government is subject
// to restrictions set forth in FAR 52.227-19 (Commercial Computer
// Software - Restricted Rights) and DFAR 252.227-7013(c)(1)(ii)
// (Rights in Technical Data and Computer Software), as applicable.
#endregion // Header

#region Namespaces
using System;
using System.Runtime.InteropServices;
using System.Windows.Forms;
#endregion // Namespaces

namespace ProgressForm
{
    /// <summary>
    /// Shared modeless progress dialog used by long-running commands (DB reads, per-element loops,
    /// exports, etc.) to give the user feedback without blocking Revit. Shown once via the
    /// constructor; callers call <see cref="Increment"/> or <see cref="IncrementWithText"/> once per
    /// unit of work, and are responsible for closing the form themselves when the work is done.
    /// Parented to Revit's main window when available (via <see cref="GMSRevitAddin.GmsUi.Owner"/>)
    /// so it stays in front of Revit rather than risking being shown owner-less.
    /// </summary>
    public partial class ProgressForm : Form
    {
        // Not currently used by this class - see commented candidate helper below.
        [DllImport("user32.dll", SetLastError = true)]
        static extern IntPtr GetForegroundWindow();

        string _format;
        string _final;

        /// <summary>The underlying WinForms progress bar control, exposed for callers that need direct access.</summary>
        public System.Windows.Forms.ProgressBar pb1 { get; set; }

        /// <summary>
        /// Set up progress bar form and immediately display it modelessly.
        /// </summary>
        /// <param name="caption">Form caption</param>
        /// <param name="format">Progress message string</param>
        /// <param name="max">Number of elements to process</param>
        public ProgressForm(string caption, string format, int max, string final)
        {
            _format = format;
            _final = final;
            InitializeComponent();
            GMSRevitAddin.DarkTheme.Apply(this);
            Text = caption;
            label1.Text = (null == format) ? caption : string.Format(format, 0);
            pb1 = progressBar1;
            progressBar1.Minimum = 0;
            progressBar1.Maximum = max;
            progressBar1.Step = 1;
            progressBar1.Value = 0;
            IWin32Window owner = GMSRevitAddin.GmsUi.Owner;
            if (owner != null)
            {
                // GmsUi.Owner is a plain IWin32Window handle wrapper: Show(owner) parents this form to
                // Revit via SetWindowLong(GWL_HWNDPARENT) and nothing else.
                //
                // This used to wrap the handle in a NativeWindow + AssignHandle instead. Do NOT go back
                // to that: AssignHandle *subclasses* the target window (installs WinForms' window proc
                // on Revit's main frame), and the NativeWindow here was a local that was never
                // ReleaseHandle()d — so it became garbage when the form was disposed and its finalizer
                // unsubclassed Revit's main window from the finalizer thread, in nondeterministic order
                // relative to any other ProgressForm's. Export Pieces shows two of these and then calls
                // GC.Collect() (the ACE lock-release recipe in ExportParts.WriteRowsToAccess), forcing
                // exactly that; the result was a wedged main frame — ribbon and menus dead while the
                // drawing canvas, a separate child HWND, kept working.
                Show(owner);
            }
            else
            {
                // Fallback when Revit's window can't be found (e.g. headless): show owner-less but
                // force TopMost so the dialog doesn't get lost behind other windows.
                Show();
                this.TopMost = true;
            }
            // Pump the message queue once so the form actually paints before the caller's loop starts.
            Application.DoEvents();
        }

        /// <summary>
        /// Advances the progress bar by one step and updates the label using the constructor's
        /// <c>format</c> string (formatted with the new value), or the <c>final</c> text once the
        /// bar reaches its last step. No-op on the label if no <c>format</c> was supplied.
        /// </summary>
        public void Increment()
        {
            if (null != _format)
            {
                if (progressBar1.Value < (progressBar1.Maximum - 1))
                {
                    ++progressBar1.Value;
                    label1.Text = string.Format(_format, progressBar1.Value);
                }
                else
                {
                    label1.Text = _final;
                }
            }
            // Pump the message queue so the modeless form actually repaints during a tight loop.
            Application.DoEvents();
        }

        /// <summary>
        /// Advances the progress bar by one step (while below its maximum) and sets the label to
        /// the given literal text, e.g. "Updating &lt;name&gt;". Unlike <see cref="Increment"/>, the
        /// caller supplies the exact text rather than a format string.
        /// </summary>
        public void IncrementWithText(string text)
        {
            if (progressBar1.Value < progressBar1.Maximum)
            {
                ++progressBar1.Value;
                label1.Text = text;
            }
            else
            {
                label1.Text = text;
            }
            // Pump the message queue so the modeless form actually repaints during a tight loop.
            Application.DoEvents();
        }
    }
}