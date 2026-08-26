using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace CollectDiesForm
{
    /// <summary>
    /// Small modal editor for the <see cref="BucketRuleSet"/> table used by "Collect Detail Items".
    /// One row per <see cref="BucketRule"/> (Prefix / Exclude-if-contains / Bucket); rules are matched
    /// top-to-bottom (first match wins), so row order matters — hence the Move Up/Down buttons rather
    /// than relying on the grid's own row drag-reorder (which is easy to trigger by accident).
    /// Built programmatically (no .designer.cs) the same way <see cref="ExportParts.ExportResultsForm"/>
    /// is, since this is a simple, one-off dialog.
    /// </summary>
    public class CollectDiesRulesForm : Form
    {
        private readonly DataGridView grid;

        public CollectDiesRulesForm()
        {
            Text = "Collect Detail Items - Bucket Rules";
            Width = 560;
            Height = 460;
            StartPosition = FormStartPosition.CenterScreen;
            MinimizeBox = false;
            MaximizeBox = false;
            // CollectDiesForm (this dialog's owner) is TopMost, which puts it in Windows' own
            // always-on-top z-order tier. An owned dialog that isn't ALSO TopMost can get pushed
            // behind a TopMost owner despite the owner/owned relationship — matching it here keeps
            // this dialog in front, where ShowDialog(owner) already intends it to stay.
            TopMost = true;
            ShowIcon = false;

            // AutoSize + Dock=Top (no fixed Height) lets the label grow to whatever height its
            // wrapped text actually needs at DarkTheme's font — a fixed guessed Height clipped the
            // second line once DarkTheme.Apply below switches the font.
            Label hint = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                MaximumSize = new Size(0, 0), // no cap on height; width is still constrained by Dock
                Padding = new Padding(10, 8, 10, 8),
                Text = "A family+type is placed in the first bucket whose rule matches its name " +
                       "(checked top to bottom). \"Exclude if contains\" is optional. Anything " +
                       "matching no rule goes in \"Other\"."
            };

            grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = true,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = SystemColors.Window,
            };
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Prefix", HeaderText = "Family name starts with" });
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = "Exclude", HeaderText = "…unless it also contains" });
            DataGridViewComboBoxColumn bucketCol = new DataGridViewComboBoxColumn
            {
                Name = "Bucket",
                HeaderText = "Bucket",
                DataSource = Enum.GetValues(typeof(FamilyBucket)),
            };
            grid.Columns.Add(bucketCol);

            FlowLayoutPanel rowButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 34,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(8, 4, 8, 4),
            };
            Button buttonUp = new Button { Text = "Move Up", AutoSize = true };
            Button buttonDown = new Button { Text = "Move Down", AutoSize = true };
            Button buttonDelete = new Button { Text = "Delete Row", AutoSize = true };
            Button buttonReset = new Button { Text = "Reset to Defaults", AutoSize = true };
            buttonUp.Click += (s, e) => MoveSelectedRow(-1);
            buttonDown.Click += (s, e) => MoveSelectedRow(1);
            buttonDelete.Click += (s, e) => DeleteSelectedRow();
            buttonReset.Click += (s, e) => LoadRules(BucketRuleSet.Default());
            rowButtons.Controls.Add(buttonUp);
            rowButtons.Controls.Add(buttonDown);
            rowButtons.Controls.Add(buttonDelete);
            rowButtons.Controls.Add(buttonReset);

            FlowLayoutPanel actionButtons = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 40,
                FlowDirection = FlowDirection.RightToLeft,
                Padding = new Padding(8, 4, 8, 4),
            };
            Button buttonCancel = new Button { Text = "Cancel", AutoSize = true, DialogResult = DialogResult.Cancel };
            Button buttonSave = new Button { Text = "Save", AutoSize = true };
            buttonSave.Click += ButtonSave_Click;
            actionButtons.Controls.Add(buttonCancel);
            actionButtons.Controls.Add(buttonSave);

            CancelButton = buttonCancel;

            Controls.Add(grid);
            Controls.Add(rowButtons);
            Controls.Add(actionButtons);
            Controls.Add(hint);

            LoadRules(BucketRuleSet.Load());

            GMSRevitAddin.DarkTheme.MarkPrimary(buttonSave);
            GMSRevitAddin.DarkTheme.Apply(this);
        }

        private void LoadRules(List<BucketRule> rules)
        {
            grid.Rows.Clear();
            foreach (BucketRule rule in rules)
            {
                grid.Rows.Add(rule.Prefix, rule.Exclude, rule.Bucket);
            }
        }

        private void MoveSelectedRow(int direction)
        {
            if (grid.CurrentRow == null || grid.CurrentRow.IsNewRow)
            {
                return;
            }
            int index = grid.CurrentRow.Index;
            int target = index + direction;
            if (target < 0 || target >= grid.Rows.Count || grid.Rows[target].IsNewRow)
            {
                return;
            }

            DataGridViewRow row = grid.Rows[index];
            grid.Rows.Remove(row);
            grid.Rows.Insert(target, row);
            grid.CurrentCell = grid.Rows[target].Cells[0];
        }

        private void DeleteSelectedRow()
        {
            if (grid.CurrentRow == null || grid.CurrentRow.IsNewRow)
            {
                return;
            }
            grid.Rows.Remove(grid.CurrentRow);
        }

        /// <summary>Reads the grid back into a rule list — skipping rows with a blank "starts with"
        /// prefix (including the trailing add-new-row placeholder) — and saves it.</summary>
        private void ButtonSave_Click(object sender, EventArgs e)
        {
            grid.EndEdit();

            List<BucketRule> rules = new List<BucketRule>();
            foreach (DataGridViewRow row in grid.Rows)
            {
                if (row.IsNewRow)
                {
                    continue;
                }
                string prefix = Convert.ToString(row.Cells["Prefix"].Value ?? "").Trim();
                if (string.IsNullOrEmpty(prefix))
                {
                    continue; // a rule with no prefix can never match anything — silently dropped
                }
                string exclude = Convert.ToString(row.Cells["Exclude"].Value ?? "").Trim();
                object bucketValue = row.Cells["Bucket"].Value;
                FamilyBucket bucket = bucketValue is FamilyBucket fb ? fb : FamilyBucket.Other;
                rules.Add(new BucketRule(prefix.ToLowerInvariant(), exclude.ToLowerInvariant(), bucket));
            }

            BucketRuleSet.Save(rules);
            DialogResult = DialogResult.OK;
            Close();
        }
    }
}
