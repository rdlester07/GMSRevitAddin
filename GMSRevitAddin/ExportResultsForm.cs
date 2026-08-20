using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Autodesk.Revit.UI;
using ElementId = Autodesk.Revit.DB.ElementId;

namespace ExportParts
{
    // ====================================================================================================
    //  EXPORT RESULTS — modeless results window for the Export Pieces command.
    // ----------------------------------------------------------------------------------------------------
    //  Shows a summary line plus a grid of pieces that were SKIPPED (non-numeric Number/Quantity that the
    //  Access integer columns can't store). Clicking a row selects + zooms to that instance in Revit so
    //  the user can go fix it. Revit's API is single-threaded, so the row click raises an ExternalEvent
    //  rather than touching the API from the WinForms callback (same idiom as CycleWorkSets).
    // ====================================================================================================

    /// <summary>One skipped piece: the values shown in the grid + the matching Revit instance id(s).</summary>
    public class SkippedPiece
    {
        /// <summary>The piece's "Prefix" instance parameter value, as shown in the schedule.</summary>
        public string Prefix;
        /// <summary>The piece's "Number" instance parameter value (the reason it was skipped, if alphanumeric).</summary>
        public string Number;
        /// <summary>The matching instance's "Origin" parameter value.</summary>
        public string Origin;
        /// <summary>The matching instance's "Origin Sheet" parameter value.</summary>
        public string OriginSheet;
        /// <summary>The Revit element id(s) of the tag instance(s) this schedule row resolved to; empty if none matched.</summary>
        public List<ElementId> Ids = new List<ElementId>();
    }

    /// <summary>
    /// Bridges the modeless results form (WinForms/UI thread) to Revit's API thread. Holds the single
    /// open results form, the active UIDocument, and the ExternalEvent used to select/zoom to a piece.
    /// </summary>
    public static class ExportResultsController
    {
        private static ExternalEvent _showEvent;
        private static readonly ShowPieceHandler _handler = new ShowPieceHandler();
        private static ExportResultsForm _form;

        /// <summary>The active UIDocument the open results form is showing pieces for.</summary>
        public static UIDocument UiDoc { get; private set; }
        /// <summary>The element id(s) queued by the most recent <see cref="RequestShow"/> call, consumed by <see cref="ShowPieceHandler"/> on Revit's API thread.</summary>
        public static IList<ElementId> PendingIds { get; private set; }

        /// <summary>Replaces any open results form with a new one (modeless, parented to Revit).</summary>
        public static void ShowResults(string summary, List<SkippedPiece> pieces, UIDocument uiDoc, IWin32Window owner)
        {
            // Close any previous window FIRST: its FormClosed handler runs synchronously and clears the
            // controller's state (UiDoc, PendingIds, the ExternalEvent), so setting up the new window
            // before this point would have that teardown wipe it out.
            try { if (_form != null && !_form.IsDisposed) _form.Close(); }
            catch (Exception ex) { GMSRevitAddin.GmsLog.Error("ExportResults.CloseOld", ex); }

            UiDoc = uiDoc;
            // ExternalEvent.Create must run in a valid API context — Execute (our caller) is one.
            if (_showEvent == null)
                _showEvent = ExternalEvent.Create(_handler);

            _form = new ExportResultsForm(summary, pieces);
            _form.FormClosed += OnFormClosed;
            _form.Show(owner);
        }

        /// <summary>
        /// Tears down everything the results window owned once the user closes it, and hands input
        /// focus back to Revit.
        ///
        /// The reactivation matters: this is a **modeless** form, so it outlives the command that
        /// showed it and nothing restores activation the way a modal dialog's message loop would. If
        /// activation lands anywhere but Revit's main frame, the ribbon and menus look frozen while the
        /// drawing canvas (its own child HWND) still responds — the symptom this was reported as.
        /// The ExternalEvent is released here rather than being kept alive for the session (cf.
        /// BuildBunkForm.OnFormClosed); ShowResults recreates it, and it only ever runs from Execute,
        /// which is a valid API context.
        /// </summary>
        private static void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                // Unsubscribe only — WinForms disposes a modeless form itself once this event returns,
                // so calling Dispose() here would just re-enter the close it's still processing.
                ExportResultsForm closed = sender as ExportResultsForm;
                if (closed != null)
                    closed.FormClosed -= OnFormClosed;

                _form = null;
                PendingIds = null;
                UiDoc = null;

                if (_showEvent != null)
                {
                    _showEvent.Dispose();
                    _showEvent = null;
                }
            }
            catch (Exception ex) { GMSRevitAddin.GmsLog.Error("ExportResults.OnFormClosed", ex); }

            GMSRevitAddin.GmsUi.ActivateRevit();
        }

        /// <summary>Queue element id(s) and ask Revit to select + zoom to them on its own thread.</summary>
        public static void RequestShow(IList<ElementId> ids)
        {
            PendingIds = ids;
            if (_showEvent != null && ids != null && ids.Count > 0)
                _showEvent.Raise();
        }
    }

    /// <summary>Selects and zooms to the pending element id(s) — runs in Revit's API context.</summary>
    public class ShowPieceHandler : IExternalEventHandler
    {
        public void Execute(UIApplication app)
        {
            // Auto-confirm Revit's "There is no open view that shows any of the highlighted elements …
            // Continue?" prompt that ShowElements raises when the piece isn't in an open view, so the
            // navigation isn't interrupted. OverrideResult(1) == the OK/Continue button. Scoped to just
            // the ShowElements call so no other dialog is accidentally suppressed.
            EventHandler<Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs> suppress =
                (s, e) => e.OverrideResult(1);

            try
            {
                UIDocument uiDoc = ExportResultsController.UiDoc;
                IList<ElementId> ids = ExportResultsController.PendingIds;
                if (uiDoc == null || ids == null || ids.Count == 0)
                    return;

                uiDoc.Selection.SetElementIds(ids);

                app.DialogBoxShowing += suppress;
                try
                {
                    uiDoc.ShowElements(ids); // activates a view containing the element(s) and zooms to them
                }
                finally
                {
                    app.DialogBoxShowing -= suppress;
                }
            }
            catch (Exception ex)
            {
                GMSRevitAddin.GmsLog.Error("ExportResults.ShowPiece", ex);
            }
        }

        public string GetName() => "GMS Export Results - Show Piece";
    }

    /// <summary>
    /// Modeless results window: summary label + grid of skipped pieces (Prefix, Number, Origin,
    /// Origin Sheet). Rows are styled like links and clicking one selects + zooms to the instance.
    /// </summary>
    public class ExportResultsForm : Form
    {
        /// <summary>Builds the results grid from the skipped pieces and applies Revit's light/dark theme.</summary>
        public ExportResultsForm(string summary, List<SkippedPiece> pieces)
        {
            pieces = pieces ?? new List<SkippedPiece>();

            Text = "Export Pieces - Results";
            Width = 560;
            Height = 440;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            // Keep a taskbar entry: this window outlives the command, so if activation ever goes wrong
            // the user still has a way to click back to it rather than being stuck.
            ShowInTaskbar = true;

            Label summaryLabel = new Label
            {
                Dock = DockStyle.Top,
                Text = summary,
                AutoSize = false,
                Height = 64,
                Padding = new Padding(10, 8, 10, 4)
            };

            // Theme-aware colors so the hint / link / disabled rows read in light AND dark.
            bool dark = false;
            try { dark = Autodesk.Revit.UI.UIThemeManager.CurrentTheme == Autodesk.Revit.UI.UITheme.Dark; }
            catch { }

            Label hint = new Label
            {
                Dock = DockStyle.Top,
                Height = 22,
                Padding = new Padding(10, 0, 10, 4),
                ForeColor = dark ? Color.FromArgb(0x9c, 0xa5, 0xaf) : Color.FromArgb(0x4b, 0x58, 0x63),
                Text = pieces.Count > 0
                    ? "Each row is one tag instance — click it to select and zoom to that instance in the active model."
                    : "No pieces were skipped."
            };

            DataGridView grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                Cursor = Cursors.Hand
            };
            grid.Columns.Add("Prefix", "Prefix");
            grid.Columns.Add("Number", "Number");
            grid.Columns.Add("Origin", "Origin");
            grid.Columns.Add("OriginSheet", "Origin Sheet");

            // Link rows carry their affordance via the underline font (no blue accent) + gray-scale color.
            // Match the rest of the form (DarkTheme.Apply re-fonts everything to the UI font below).
            Font linkFont = new Font(GMSRevitAddin.DarkTheme.UiFontName, grid.Font.Size, FontStyle.Underline);
            Color linkColor = dark ? Color.FromArgb(0x9c, 0xa5, 0xaf) : Color.FromArgb(0x4b, 0x58, 0x63);
            Color disabledColor = dark ? Color.FromArgb(0x6f, 0x79, 0x85) : Color.FromArgb(0x6f, 0x79, 0x85);

            foreach (SkippedPiece p in pieces)
            {
                int idx = grid.Rows.Add(p.Prefix, p.Number, p.Origin, p.OriginSheet);
                DataGridViewRow gridRow = grid.Rows[idx];
                gridRow.Tag = p;
                if (p.Ids.Count > 0)
                {
                    gridRow.DefaultCellStyle.ForeColor = linkColor;
                    gridRow.DefaultCellStyle.Font = linkFont;
                }
                else
                {
                    // No matching instance found — show it but make clear it isn't clickable.
                    gridRow.DefaultCellStyle.ForeColor = disabledColor;
                }
            }

            grid.CellClick += (s, e) =>
            {
                if (e.RowIndex < 0 || e.RowIndex >= grid.Rows.Count)
                    return;
                SkippedPiece p = grid.Rows[e.RowIndex].Tag as SkippedPiece;
                if (p != null && p.Ids.Count > 0)
                    ExportResultsController.RequestShow(p.Ids);
            };

            // Docked controls: add Fill first, then the Top labels stack above it (last added sits highest).
            Controls.Add(grid);
            Controls.Add(hint);
            Controls.Add(summaryLabel);

            // Match Revit's theme (light/dark). Row-level link/disabled colors set above survive this,
            // since DataGridView row styles override the grid-wide default the theme sets.
            GMSRevitAddin.DarkTheme.Apply(this);
        }
    }
}
