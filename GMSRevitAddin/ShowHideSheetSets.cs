using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ShowHideSheetSets
{
    /// <summary>
    /// Dialog that lets the user pick which sheet-set "phases" (checked list of sheet set names)
    /// and which tag categories (Piece/Gasket/Glazing/Customer/Sub-Unit) to show or hide tags for,
    /// shared by both the "Show Tags" (<c>ShowTags.ShowTags</c>) and "Hide Tags" (<c>ShowTags.HideTags</c>)
    /// commands. The <paramref name="show"/> flag passed to the constructor selects which of those two
    /// static result holders gets populated on Apply, and whether the tag-category panel is shown at all
    /// (Hide Tags only needs the sheet-set list).
    /// </summary>
    public partial class ShowHideSheetSets : Form
    {
        List<string> sheetSetTypeList = new List<string>();
        bool showHide = false;

        /// <summary>
        /// Constructs the dialog. <paramref name="shSetTypes"/> is the distinct list of sheet-set
        /// names to offer as checkboxes; <paramref name="show"/> is true for the "Show Tags" flow
        /// (tag-category panel visible) and false for "Hide Tags" (panel hidden — hiding doesn't need
        /// per-category granularity).
        /// </summary>
        public ShowHideSheetSets(List<string> shSetTypes, bool show)
        {
            InitializeComponent();
            sheetSetTypeList = shSetTypes;
            showHide = show;
            if(!showHide)
            {
                flowLayoutPanel1.Visible = false;
            }
            // Glazing/Customer/Sub-Unit tag options are not yet wired up for this flow — keep them
            // hidden and disabled until that functionality is implemented.
            checkBoxCustomer.Visible = false;
            checkBoxGlazing.Visible = false;
            checkBoxSubUnit.Visible = false;
            checkBoxCustomer.Enabled = false;
            checkBoxGlazing.Enabled = false;
            checkBoxSubUnit.Enabled = false;
        }

        private void ShowHideSheetSets_Load(object sender, EventArgs e)
        {
            // Match Revit's current light/dark theme
            GMSRevitAddin.DarkTheme.MarkPrimary(buttonStart);
            GMSRevitAddin.DarkTheme.Apply(this);
            var items = checkedListBox1.Items;
            sheetSetTypeList.Sort();
            // Populate the checked list with the sheet-set names passed in; blank names are shown
            // as "???" so a legitimate empty phase name is still selectable/visible in the list.
            foreach (string sheetSet in sheetSetTypeList)
            {
                if (!string.IsNullOrWhiteSpace(sheetSet))
                {
                    items.Add(sheetSet);
                }
                else
                {
                    items.Add("???");
                }
            }
        }

        // Cancel: clear both commands' result holders so a stale selection from a prior run
        // can't be picked up by the caller, then close without a result.
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            ShowTags.ShowTags.chosenSheetSets.Clear();
            ShowTags.HideTags.chosenSheetSets.Clear();
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        private void buttonStart_Click(object sender, EventArgs e)
        {
            // Stash the checkbox selections into the static fields the calling command reads
            // after ShowDialog() returns.
            ShowTags.ShowTags.collectElevations = checkBoxElevations.Checked;
            ShowTags.ShowTags.collectFloorplans = checkBoxFloorPlans.Checked;
            ShowTags.HideTags.collectElevations = checkBoxElevations.Checked;
            ShowTags.HideTags.collectFloorplans = checkBoxFloorPlans.Checked;
            ShowTags.ShowTags.PieceTags = checkBoxPiece.Checked;
            ShowTags.ShowTags.GasketTags = checkBoxGasket.Checked;
            ShowTags.ShowTags.GlazingTags = checkBoxGlazing.Checked;
            ShowTags.ShowTags.CustomerTags = checkBoxCustomer.Checked;
            ShowTags.ShowTags.SubUnitTags = checkBoxSubUnit.Checked;

            // Collect the checked sheet-set ("phase") names from the checklist.
            var checkedPhases = checkedListBox1.CheckedIndices;
            List<string> phases = new List<string>();
            foreach (int indexChecked in checkedPhases)
            {
                phases.Add(checkedListBox1.Items[indexChecked].ToString());
            }

            if (phases.Count > 0)
            {
                // Route the result into whichever command's static holder is active for this
                // dialog instance (Show vs. Hide), based on the constructor's "show" flag.
                if (showHide)
                {
                    ShowTags.ShowTags.chosenSheetSets.Clear();
                    ShowTags.ShowTags.chosenSheetSets = phases;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else if (!showHide)
                {
                    ShowTags.HideTags.chosenSheetSets.Clear();
                    ShowTags.HideTags.chosenSheetSets = phases;
                    this.DialogResult = DialogResult.OK;
                    this.Close();
                }
                else
                {
                    this.DialogResult = DialogResult.Abort;
                    this.Close();
                }
            }
        }

        // Unchecking "Elevations" forces Piece/Gasket/Glazing/Customer/Sub-Unit tags on and locks
        // most of them, since those tag types only apply to elevation views.
        private void checkBoxElevations_CheckedChanged(object sender, EventArgs e)
        {
            if (!checkBoxElevations.Checked)
            {
                checkBoxPiece.Checked = true;
                checkBoxPiece.Enabled = false;
                checkBoxGasket.Checked = true;
                checkBoxGasket.Enabled = false;
                checkBoxGlazing.Checked = true;
                //checkBoxGlazing.Enabled = false;
                checkBoxCustomer.Checked = true;
                //checkBoxCustomer.Enabled = false;
                checkBoxSubUnit.Checked = true;
                //checkBoxSubUnit.Enabled = false;
            }
            if (checkBoxElevations.Checked)
            {
                checkBoxPiece.Enabled = true;
                checkBoxGasket.Enabled = true;
                //checkBoxGlazing.Enabled = true;
                //checkBoxCustomer.Enabled = true;
                //checkBoxSubUnit.Enabled = true;
            }
        }
    }
}
