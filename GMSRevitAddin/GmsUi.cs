using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GMSRevitAddin
{
    /// <summary>
    /// Centralized WinForms dialog helpers for the add-in.
    ///
    /// Revit hosts the add-in's WinForms dialogs inside its own process. A <see cref="MessageBox"/>
    /// or <see cref="Form"/> shown **without Revit set as its owner** can render *behind* the Revit
    /// window; its modal message loop then disables the main window (ribbon/menus) while the drawing
    /// canvas still repaints — i.e. the UI appears "locked" with no visible dialog. Parenting every
    /// dialog to Revit's main window via <see cref="Owner"/> keeps dialogs in front and modal-correct.
    ///
    /// The <c>ShowError</c> overloads also write to <see cref="GmsLog"/>, so error popups are
    /// captured (with a stack trace when an <see cref="Exception"/> is supplied). Historically many
    /// catch blocks showed <c>ex.Message</c> in a box but never logged it, leaving intermittent
    /// errors (e.g. "Improper argument") with no trace to diagnose.
    /// </summary>
    public static class GmsUi
    {
        private sealed class OwnerWindow : IWin32Window
        {
            private readonly IntPtr _handle;
            public OwnerWindow(IntPtr handle) { _handle = handle; }
            public IntPtr Handle { get { return _handle; } }
        }

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool IsWindowEnabled(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnableWindow(IntPtr hWnd, [MarshalAs(UnmanagedType.Bool)] bool bEnable);

        [DllImport("user32.dll")]
        private static extern IntPtr SetActiveWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr SetFocus(IntPtr hWnd);

        /// <summary>
        /// Revit's main window handle, captured from <c>UIControlledApplication.MainWindowHandle</c>
        /// in <c>GMS_tools.OnStartup</c>. This is the Revit-API-correct source; see
        /// <see cref="OwnerHandle"/> for why the <see cref="Process"/> fallback is not trusted first.
        /// </summary>
        internal static IntPtr RevitMainWindowHandle { get; set; }

        /// <summary>
        /// The window handle to parent dialogs to: Revit's real main window when it has been captured,
        /// otherwise the host process's main window handle, otherwise <see cref="IntPtr.Zero"/>.
        ///
        /// The fallback is a last resort, not the preferred source:
        /// <c>Process.GetCurrentProcess().MainWindowHandle</c> returns the *first visible top-level
        /// window with no owner* in the process — which is not guaranteed to be Revit's main frame and
        /// can pick up a transient window (including one of the add-in's own modeless forms). Parenting
        /// a modeless form to the wrong HWND means closing it never re-activates Revit's frame: the
        /// drawing canvas (its own child HWND) keeps working while the ribbon/menus appear dead.
        /// </summary>
        public static IntPtr OwnerHandle
        {
            get
            {
                try
                {
                    IntPtr h = RevitMainWindowHandle;
                    if (h != IntPtr.Zero && IsWindow(h))
                        return h;
                    return Process.GetCurrentProcess().MainWindowHandle;
                }
                catch
                {
                    return IntPtr.Zero;
                }
            }
        }

        /// <summary>
        /// Revit's main window as a dialog owner, or <c>null</c> if it cannot be resolved (callers
        /// then fall back to owner-less behavior — no regression). See <see cref="OwnerHandle"/>.
        /// </summary>
        public static IWin32Window Owner
        {
            get
            {
                IntPtr h = OwnerHandle;
                return h == IntPtr.Zero ? null : new OwnerWindow(h);
            }
        }

        /// <summary>
        /// Returns input focus to Revit's main window. Call this when a **modeless** add-in form closes:
        /// unlike a modal dialog (whose message loop restores activation on its own), a modeless form
        /// leaves activation wherever Windows puts it, and if its owner HWND wasn't Revit's frame the
        /// frame is never re-activated — ribbon and menus look frozen while the drawing canvas still
        /// responds.
        ///
        /// Also re-enables the window defensively. A main window found *disabled* here means something
        /// called <c>EnableWindow(false)</c> without a matching re-enable (an unbalanced modal dialog),
        /// which is a different fault than a bad owner — so that case is logged, since the log line is
        /// what tells the two apart after the fact. Never throws.
        /// </summary>
        public static void ActivateRevit()
        {
            try
            {
                IntPtr h = OwnerHandle;
                if (h == IntPtr.Zero || !IsWindow(h))
                    return;

                if (!IsWindowEnabled(h))
                {
                    GmsLog.Error("GmsUi.ActivateRevit: Revit's main window was disabled (unbalanced " +
                        "EnableWindow from a modal dialog?); re-enabling it.");
                    EnableWindow(h, true);
                }

                SetActiveWindow(h);
                SetForegroundWindow(h);
            }
            catch (Exception ex)
            {
                GmsLog.Error("GmsUi.ActivateRevit", ex);
            }
        }

        [DllImport("user32.dll")]
        private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

        private const byte VK_ESCAPE = 0x1B;
        private const uint KEYEVENTF_KEYUP = 0x0002;

        /// <summary>
        /// Simulates one physical Escape keypress into Revit, ending whatever interactive command is
        /// currently running. The Revit API has no "cancel the active command" call, so the only way
        /// an add-in can stop e.g. Tag by Category's "click to tag another element" repeat loop is the
        /// same way a user would.
        ///
        /// Both halves of the setup matter. **Focus:** the caller is typically a WPF control in a
        /// docked pane, which still holds Win32 keyboard focus after the click that got us here — an
        /// Escape delivered there is swallowed by the pane instead of reaching Revit's command loop.
        /// (That is also why a user has to press Escape *twice* to get out of a command started from
        /// such a pane: the first press is eaten moving focus off it.) <see cref="ActivateRevit"/>
        /// re-activates Revit's frame and <c>SetFocus</c> then moves keyboard focus onto it, so the
        /// keystroke lands where a real one would. <c>SetFocus</c> only works on a window owned by the
        /// calling thread's message queue, which holds here — callers run on Revit's UI thread, which
        /// owns the frame.
        ///
        /// **Context:** this is pure Win32 and touches no Revit API, so — unlike almost everything
        /// else the palette does — it is legal from a plain WPF click handler, outside any Revit API
        /// context. That is exactly where <c>TaggingPalettePane.TypeButton_Click</c> calls it, and it
        /// has to be called there rather than from the ExternalEvent it raises: Revit will not run a
        /// raised <c>ExternalEvent</c> while an interactive command is active, so an Escape sent from
        /// inside that handler could never cancel the command that was blocking the handler from
        /// running in the first place.
        /// </summary>
        public static void SendEscape()
        {
            try
            {
                ActivateRevit();

                IntPtr h = OwnerHandle;
                if (h != IntPtr.Zero && IsWindow(h))
                    SetFocus(h);

                keybd_event(VK_ESCAPE, 0, 0, UIntPtr.Zero);
                keybd_event(VK_ESCAPE, 0, KEYEVENTF_KEYUP, UIntPtr.Zero);
            }
            catch (Exception ex)
            {
                GmsLog.Error("GmsUi.SendEscape", ex);
            }
        }

        /// <summary>Informational message box, parented to Revit.</summary>
        public static DialogResult Show(string text, string caption)
        {
            return MessageBox.Show(Owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>Warning message box, parented to Revit.</summary>
        public static DialogResult ShowWarning(string text, string caption)
        {
            return MessageBox.Show(Owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        /// <summary>Error message box, parented to Revit; the shown text is also logged.</summary>
        public static DialogResult ShowError(string text, string caption)
        {
            GmsLog.Error((string.IsNullOrEmpty(caption) ? "Error" : caption) + ": " + text);
            return MessageBox.Show(Owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// Error message box for a caught exception: logs the full exception (with stack) via
        /// <see cref="GmsLog"/> and displays <paramref name="text"/>, parented to Revit so the
        /// dialog can't hide behind the window. Use when you want a friendly message but still
        /// want the underlying exception recorded.
        /// </summary>
        public static DialogResult ShowError(string text, string caption, Exception ex)
        {
            GmsLog.Error(caption, ex);
            return MessageBox.Show(Owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        /// <summary>
        /// Error message box for a caught exception: logs the exception and displays
        /// <c>ex.Message</c>, parented to Revit.
        /// </summary>
        public static DialogResult ShowError(Exception ex, string caption)
        {
            GmsLog.Error(caption, ex);
            string text = ex == null ? "Unknown error." : ex.Message;
            return MessageBox.Show(Owner, text, caption, MessageBoxButtons.OK, MessageBoxIcon.Error);
        }
    }
}
