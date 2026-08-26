using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace CollectDiesForm
{
    /// <summary>One family+type that failed to place into the new collection view, and why.</summary>
    public class PlacementFailure
    {
        public string FamilyName;
        public string TypeName;
        public string Reason;
    }

    /// <summary>
    /// Modeless results window shown after "Collect Detail Items" finishes, when one or more
    /// placements failed. Each row shows the family, type, and the exception message — replacing the
    /// old approach of dumping just "Family - Type" lines into a <c>TextNote</c> on the new view with
    /// no reason attached. The <c>TextNote</c> itself still gets written too (it's the permanent,
    /// find-it-later record on the model for anyone who opens the view afterward); this dialog is the
    /// easier-to-read, immediate view of the same failures, shown once right after the run.
    ///
    /// There's nothing to navigate to from a row — placement itself failed, so no instance exists to
    /// select/zoom to — so unlike <see cref="ExportParts.ExportResultsForm"/> this is a plain
    /// read-only grid with no click handler and no <c>ExternalEvent</c>. It's still shown via a
    /// static controller (<see cref="CollectDiesResultsController"/>) for the same reason
    /// <c>ExportResultsController</c> exists: a modeless form outlives the command that shows it, so
    /// something has to guarantee only one instance is open and hand focus back to Revit when closed.
    /// </summary>
    public class CollectDiesResultsForm : Form
    {
        public CollectDiesResultsForm(List<PlacementFailure> failures)
        {
            failures = failures ?? new List<PlacementFailure>();

            Text = "Collect Detail Items - Placement Failures";
            Width = 520;
            Height = 400;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            ShowInTaskbar = true; // outlives the command; give the user a way back to it

            Label summary = new Label
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(10, 8, 10, 4),
                Text = failures.Count + " item" + (failures.Count == 1 ? "" : "s") + " could not be placed " +
                       "into the new view. They were skipped; everything else was placed normally.",
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
            };
            grid.Columns.Add("Family", "Family");
            grid.Columns.Add("Type", "Type");
            grid.Columns.Add("Reason", "Reason");

            foreach (PlacementFailure f in failures)
            {
                grid.Rows.Add(f.FamilyName, f.TypeName, f.Reason);
            }

            Controls.Add(grid);
            Controls.Add(summary);

            GMSRevitAddin.DarkTheme.Apply(this);
        }
    }

    /// <summary>Holds the single open <see cref="CollectDiesResultsForm"/> (if any) and tears it down
    /// cleanly, mirroring <see cref="ExportParts.ExportResultsController"/>.</summary>
    public static class CollectDiesResultsController
    {
        private static CollectDiesResultsForm _form;

        /// <summary>Replaces any open results window with a new one (modeless, parented to Revit —
        /// NOT to the "Collect Detail Items" dialog, which is about to close and would take an owned
        /// window down with it).</summary>
        public static void ShowResults(List<PlacementFailure> failures, IWin32Window owner)
        {
            try { if (_form != null && !_form.IsDisposed) _form.Close(); }
            catch (Exception ex) { GMSRevitAddin.GmsLog.Error("CollectDiesResults.CloseOld", ex); }

            _form = new CollectDiesResultsForm(failures);
            _form.FormClosed += OnFormClosed;
            _form.Show(owner);
        }

        private static void OnFormClosed(object sender, FormClosedEventArgs e)
        {
            try
            {
                CollectDiesResultsForm closed = sender as CollectDiesResultsForm;
                if (closed != null)
                {
                    closed.FormClosed -= OnFormClosed;
                }
                _form = null;
            }
            catch (Exception ex) { GMSRevitAddin.GmsLog.Error("CollectDiesResults.OnFormClosed", ex); }

            GMSRevitAddin.GmsUi.ActivateRevit();
        }
    }
}
