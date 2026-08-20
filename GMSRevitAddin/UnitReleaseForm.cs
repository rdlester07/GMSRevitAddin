using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace UnitReleaseForm
{
    /// <summary>
    /// Modal "Unit Release" dialog: shows a searchable, checkbox-selectable grid of unit sheets (built
    /// from the <see cref="UnitRelease.UnitRelease.UnitInfo"/> list passed in) and, on Apply, runs
    /// <see cref="UnitRelease.UnitReleaseUpdate"/> against the checked units with the chosen release
    /// date and an optional "print PDF" flag. Restores/saves its size and location via
    /// <c>Properties.Settings</c>.
    /// </summary>
    public partial class UnitReleaseForm : System.Windows.Forms.Form
    {
        public static List<UnitRelease.UnitRelease.UnitInfo> sheetsSet = new List<UnitRelease.UnitRelease.UnitInfo>();
        public static UIApplication uiApp = null;
        public static Autodesk.Revit.DB.Document doc = null;
        public static Autodesk.Revit.ApplicationServices.Application app = null;
        public static UIApplication uiapp = null;
        public static UIDocument uidoc = null;
        public static ExternalCommandData comData = null;
        public static DataGridView dgv = null;
        public static Label CountLabel = null;
        public static DataTable dataTable = null;

        public UnitReleaseForm(List<UnitRelease.UnitRelease.UnitInfo> sheets, ExternalCommandData commandData)
        {
            InitializeComponent();
            sheetsSet.Clear();
            uiApp = commandData.Application;
            doc = uiApp.ActiveUIDocument.Document;
            app = doc.Application;
            uiapp = new UIApplication(app);
            uidoc = new UIDocument(doc);
            comData = commandData;
            sheetsSet = sheets;
        }

        /// <summary>Applies the shared theme, restores the saved size/location, and builds the
        /// backing <see cref="DataTable"/> (Selected checkbox + Unit/Description/Date columns, with a
        /// hidden ElementID column used to resolve selections back to Revit elements), sorted by
        /// unit name.</summary>
        private void UnitReleaseForm_Load(object sender, EventArgs e)
        {
            GMSRevitAddin.DarkTheme.MarkPrimary(buttonApply);
            GMSRevitAddin.DarkTheme.Apply(this);
            dgv = dataGridView1;
            CountLabel = labelSelectedCount;
            if(GMSRevitAddin.Properties.Settings.Default.UnitRelsaseFormLocation != new System.Drawing.Point(-100, -100))
            {
                this.Location = GMSRevitAddin.Properties.Settings.Default.UnitRelsaseFormLocation;
            }
            if(GMSRevitAddin.Properties.Settings.Default.UnitReleaseFromSize != new System.Drawing.Size(-100, -100))
            {
                this.Size = GMSRevitAddin.Properties.Settings.Default.UnitReleaseFromSize;
            }

            dataTable = new DataTable();
            dataTable.Columns.Add(new DataColumn("Selected", typeof(bool)));
            dataTable.Columns.Add("Unit");
            dataTable.Columns.Add("Description");
            dataTable.Columns.Add("Date");
            dataTable.Columns.Add("ElementID");
            dataTable.Columns[0].ReadOnly = false;
            dataTable.Columns[1].ReadOnly = true;
            dataTable.Columns[2].ReadOnly = true;
            dataTable.Columns[3].ReadOnly = true;
            dataTable.Columns[4].ReadOnly = true;
            dataTable.Columns[4].ColumnMapping = MappingType.Hidden;   // ElementID is data-only, never rendered
            foreach (UnitRelease.UnitRelease.UnitInfo info in sheetsSet)
            {
                DataRow dataRow = dataTable.NewRow();
                dataRow[0] = false;
                dataRow[1] = info.sheetName;
                dataRow[2] = info.description;
                dataRow[3] = info.releaseDate;
                dataRow[4] = info.elementID;
                dataTable.Rows.Add(dataRow);
            }
            dataTable.DefaultView.Sort = dataTable.Columns[1] + " asc";
            dataTable = dataTable.DefaultView.ToTable();
            dataGridView1.Columns.Clear();
            dataGridView1.DataSource = dataTable;
            // Sorting is fixed (by unit name) — disable interactive column-header sort so the
            // ElementID-keyed row identity stays predictable for the checkbox column.
            foreach(DataGridViewColumn dgvC in dataGridView1.Columns)
            {
                dgvC.SortMode = DataGridViewColumnSortMode.NotSortable;
            }
            dataGridView1.ClearSelection();
            buttonApply.Focus();
        }

        /// <summary>
        /// Builds a unit-name-by-ElementID map from every checked row and, if at least one is checked,
        /// runs <see cref="UnitRelease.UnitReleaseUpdate"/> with the chosen date and PDF flag against
        /// them. Clears the search filter and grid selection afterward so the dialog is ready for
        /// another release batch without closing.
        /// </summary>
        private void buttonApply_Click(object sender, EventArgs e)
        {
            dataTable.AcceptChanges();
            Dictionary<string, string> selectedUnits = new Dictionary<string, string>();

            DataRow[] rows = dataTable.Select("Selected = 1");
            if (rows.Any())
            {
                foreach (DataRow row in rows)
                {
                    selectedUnits.Add(row[4].ToString(), row[1].ToString());
                }
            }

            if (selectedUnits.Any())
            {
                GMSRevitAddin.Properties.Settings.Default.Save();

                string date = dateTimePicker1.Text;
                UnitRelease.UnitReleaseUpdate.date = dateTimePicker1.Text;
                UnitRelease.UnitReleaseUpdate.units = selectedUnits;
                UnitRelease.UnitReleaseUpdate.printPDF = checkBoxPDF.Checked;

                var command = new UnitRelease.UnitReleaseUpdate();
                command.Execute(comData, ref date, null);

                dataGridView1.ClearSelection();
                textBoxSearch.Clear();
                dataTable.DefaultView.RowFilter = null;
            }
            else
            {
                MessageBox.Show("No sheets were selected.", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>Persists the form's current size/location for next time it's opened.</summary>
        private void onFormClosing(object sender, FormClosingEventArgs e)
        {
            GMSRevitAddin.Properties.Settings.Default.UnitReleaseFromSize = this.Size;
            GMSRevitAddin.Properties.Settings.Default.UnitRelsaseFormLocation = this.Location;
            GMSRevitAddin.Properties.Settings.Default.Save();
        }

        /// <summary>Live-filters the grid to units whose name contains the search text (clearing the
        /// filter — and the current selection — when the search box is emptied).</summary>
        private void textBoxSearch_TextChanged(object sender, EventArgs e)
        {
            dataGridView1.ClearSelection();
            if(string.IsNullOrWhiteSpace(textBoxSearch.Text))
            {
                dataTable.DefaultView.RowFilter = null;
            }
            else
            {
                dataTable.DefaultView.RowFilter = string.Format("Unit LIKE '%{0}%'", textBoxSearch.Text);
            }
        }

        /// <summary>Updates the "N units selected" label whenever a row's checkbox changes.</summary>
        private void dataGridView1_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            if (dataTable != null)
            {
                dataTable.AcceptChanges();
                int count = dataTable.Select("Selected = 1").Length;
                if (count < 1)
                {
                    CountLabel.Text = "No units selected.";
                }
                else if (count == 1)
                {
                    CountLabel.Text = "1 unit selected.";
                }
                else
                {
                    CountLabel.Text = count.ToString() + " units selected.";
                }
            }
        }

        /// <summary>Clears every row's Selected checkbox and resets the selected-count label.</summary>
        private void buttonUncheckAll_Click(object sender, EventArgs e)
        {
            foreach(DataRow row in dataTable.Rows)
            {
                row[0] = 0;
            }
            CountLabel.Text = "No units selected.";
        }

        /// <summary>
        /// Lets a multi-row grid selection (e.g. shift/ctrl-click on the row headers) toggle every
        /// selected row's checkbox at once, rather than requiring the user to click each checkbox cell
        /// individually.
        /// </summary>
        private void dataGridView1_CellMouseUp(object sender, DataGridViewCellMouseEventArgs e)
        {
            List<int> indexes = new List<int>();
            foreach (DataGridViewRow row in dataGridView1.SelectedRows)
            {
                indexes.Add(row.Index);
            }

            foreach (int index in indexes)
            {
                if (Convert.ToBoolean(dataGridView1.Rows[index].Cells[0].Value) == true)
                {
                    dataGridView1.Rows[index].Cells[0].Value = CheckState.Unchecked;
                }
                else
                {
                    dataGridView1.Rows[index].Cells[0].Value = CheckState.Checked;
                }
            }
            dataTable.AcceptChanges();
        }
    }
}
