using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace EditDetailItemTag
{
    /// <summary>
    /// Modal "Edit &lt;kind&gt;" dialog shown by <see cref="EditDetailItemTagCommand"/>: a
    /// Parameter/Value/Scope grid of the target's editable parameters, each row pre-populated from
    /// <paramref name="parameters"/>' current value (blank if the parameter has never been set —
    /// there's nothing else to show). Save copies the grid's current values into
    /// <see cref="EditedValues"/> and closes with <c>DialogResult.OK</c> — no Revit API calls happen
    /// in here; the caller applies the edits inside its own Transaction after <c>ShowDialog</c>
    /// returns, keeping API/transaction work centralized in the command (matching
    /// <c>CreateUnitSheetForm</c>'s dialog/command split rather than <c>UnitReleaseForm</c>'s older
    /// do-the-work-in-the-click-handler style). Takes the instance's type name as a plain string
    /// (rather than a Revit element reference) so this file has no Revit API dependency and no
    /// need for the <c>Autodesk.Revit.DB.Form</c> / <c>System.Windows.Forms.Form</c> name collision
    /// workaround other Revit-typed forms in this add-in need (see <c>ExportResultsForm.cs</c>).
    ///
    /// Built in code with no designer file, following <see cref="CollectDiesForm.CollectDiesResultsForm"/>.
    /// </summary>
    public class EditAnnotationInstanceForm : Form
    {
        private const int MinValueColumnWidth = 400;

        private readonly DataGridView _grid;
        private readonly Button _buttonSave;

        // The rows currently shown, in the same order as the grid's rows — read-only lookup for
        // ButtonSave_Click to recover each row's Name/IsTypeParameter (not editable in the grid, so
        // no need to re-read them from the grid itself; only Value can have changed).
        private readonly List<EditableParameter> _parameters;

        /// <summary>Every row's parameter (Name/IsTypeParameter unchanged from the constructor's
        /// input) paired with its current Value from the grid — whether or not the text actually
        /// changed; the caller re-applies all of them, which is harmless/idempotent.</summary>
        public List<EditableParameter> EditedValues { get; } = new List<EditableParameter>();

        public EditAnnotationInstanceForm(string typeName, string kindLabel, List<EditableParameter> parameters)
        {
            _parameters = parameters ?? new List<EditableParameter>();
            typeName = typeName ?? "";

            Text = "Edit " + kindLabel + (string.IsNullOrEmpty(typeName) ? "" : " - " + typeName);
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            // Final size (width guaranteeing the Value column's minimum, height fit to the row
            // count) is computed at the end of the constructor, once the grid's columns/rows/font
            // are all in their final state — see the ClientSize assignment below.

            Label heading = new Label
            {
                Dock = DockStyle.Top,
                Height = 32,
                Padding = new Padding(10, 8, 10, 4),
                Text = kindLabel + (string.IsNullOrEmpty(typeName) ? "" : " — " + typeName),
            };

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.CellSelect,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
            };
            _grid.DefaultCellStyle.WrapMode = DataGridViewTriState.True; // so a taller row can actually show wrapped multi-line text
            // Parameter and Scope autofit to their content; Value is the only column left on the
            // grid's own AutoSizeColumnsMode (Fill), so it soaks up all remaining width — making it
            // the widest column without needing to hand-tune fill weights.
            _grid.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill;
            DataGridViewTextBoxColumn nameCol = new DataGridViewTextBoxColumn
            {
                Name = "Parameter", HeaderText = "Parameter", ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            };
            // MinimumWidth is a hard floor DataGridView enforces even under Fill sizing, so Value
            // stays >= 400px even if the user manually shrinks the (resizable) form afterward.
            DataGridViewTextBoxColumn valueCol = new DataGridViewTextBoxColumn { Name = "Value", HeaderText = "Value", ReadOnly = false, MinimumWidth = MinValueColumnWidth };
            // Read-only — lets the user see at a glance that a "Type" row edits the shared type
            // (affecting every instance of it), not just this one instance.
            DataGridViewTextBoxColumn scopeCol = new DataGridViewTextBoxColumn
            {
                Name = "Scope", HeaderText = "Scope", ReadOnly = true,
                AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells,
            };
            _grid.Columns.Add(nameCol);
            _grid.Columns.Add(valueCol);
            _grid.Columns.Add(scopeCol);
            foreach (DataGridViewColumn col in _grid.Columns)
                col.SortMode = DataGridViewColumnSortMode.NotSortable;

            // Parameter and Scope are read-only already (no editing), but a click there still lands
            // and highlights the cell by default — redirect focus/selection back to that row's Value
            // cell so Value is the only column that ever visibly selects.
            _grid.CurrentCellChanged += (s, e) =>
            {
                if (_grid.CurrentCell != null && _grid.CurrentCell.ColumnIndex != valueCol.Index)
                    _grid.CurrentCell = _grid.Rows[_grid.CurrentCell.RowIndex].Cells[valueCol.Index];
            };

            // Each row is pre-populated with the parameter's current value (blank if it has never
            // been set) — row order matches _parameters' order, relied on by ButtonSave_Click.
            foreach (EditableParameter p in _parameters)
                _grid.Rows.Add(p.Name, p.Value, p.IsTypeParameter ? "Type" : "Instance");

            // Plain Panel (not a FlowLayoutPanel) with the buttons positioned by hand and re-centered
            // on resize — a FlowLayoutPanel only packs controls against one edge, it can't center a
            // pair of them as a group.
            Panel buttonPanel = new Panel { Dock = DockStyle.Bottom, Height = 44 };
            Button buttonCancel = new Button { Text = "Cancel", DialogResult = DialogResult.Cancel, Width = 90 };
            _buttonSave = new Button { Text = "Save", Width = 90 };
            _buttonSave.Click += ButtonSave_Click;
            buttonPanel.Controls.Add(buttonCancel);
            buttonPanel.Controls.Add(_buttonSave);

            const int buttonSpacing = 10;
            void CenterButtons()
            {
                int totalWidth = buttonCancel.Width + buttonSpacing + _buttonSave.Width;
                int startX = Math.Max(0, (buttonPanel.ClientSize.Width - totalWidth) / 2);
                int y = (buttonPanel.ClientSize.Height - _buttonSave.Height) / 2;
                buttonCancel.Location = new Point(startX, y);
                _buttonSave.Location = new Point(startX + buttonCancel.Width + buttonSpacing, y);
            }
            buttonPanel.Resize += (s, e) => CenterButtons();
            CenterButtons();

            Controls.Add(_grid);
            Controls.Add(buttonPanel);
            Controls.Add(heading);

            AcceptButton = _buttonSave;
            CancelButton = buttonCancel;

            GMSRevitAddin.DarkTheme.MarkPrimary(_buttonSave);
            GMSRevitAddin.DarkTheme.Apply(this);

            // Tall enough for at least 3 lines of wrapped text — computed after DarkTheme.Apply so
            // it's measured against the grid's actual (theme-applied) font, then applied both to
            // the row template (for consistency/any future rows) and every row already added above.
            int rowHeight = TextRenderer.MeasureText("Ay", _grid.Font).Height * 3 + 10;
            _grid.RowTemplate.Height = rowHeight;
            foreach (DataGridViewRow row in _grid.Rows)
                row.Height = rowHeight;

            // Width: a "standard" dialog width, sized off the two auto-fit columns (Parameter,
            // Scope — both AllCells, so their .Width already reflects their content by this point)
            // plus a guaranteed-minimum Value column, plus a small buffer for the grid's own border
            // and a vertical scrollbar (only actually needed if the height cap below kicks in, but
            // reserved either way so Value never dips under the minimum).
            const int chromeBuffer = 24;
            int clientWidth = nameCol.Width + scopeCol.Width + MinValueColumnWidth + chromeBuffer;

            // Height: fit every row without scrolling, unless that would run off the screen — in
            // that case cap it and let the grid's own vertical scrollbar (default DataGridView
            // behavior) cover the rest.
            int desiredClientHeight = heading.Height + _grid.ColumnHeadersHeight +
                rowHeight * _parameters.Count + buttonPanel.Height + 4;
            int maxClientHeight = Screen.PrimaryScreen.WorkingArea.Height - 100;
            int clientHeight = Math.Min(desiredClientHeight, maxClientHeight);

            ClientSize = new Size(clientWidth, clientHeight);
        }

        /// <summary>Commits any in-progress cell edit, copies every row's current Value (paired with
        /// its original Name/IsTypeParameter) into <see cref="EditedValues"/>, and closes with
        /// <c>DialogResult.OK</c>.</summary>
        private void ButtonSave_Click(object sender, EventArgs e)
        {
            try { _grid.EndEdit(); } catch (Exception ex) { GMSRevitAddin.GmsLog.Error("EditAnnotationInstanceForm.EndEdit", ex); }

            EditedValues.Clear();
            for (int i = 0; i < _grid.Rows.Count; i++)
            {
                DataGridViewRow row = _grid.Rows[i];
                if (row.IsNewRow || i >= _parameters.Count) continue;

                string value = Convert.ToString(row.Cells["Value"].Value) ?? string.Empty;
                EditedValues.Add(new EditableParameter
                {
                    Name = _parameters[i].Name,
                    Value = value,
                    IsTypeParameter = _parameters[i].IsTypeParameter,
                });
            }

            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
