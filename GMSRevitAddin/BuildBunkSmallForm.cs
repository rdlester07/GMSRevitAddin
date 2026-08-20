using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace BuildBunkSmallForm
{
    /// <summary>
    /// Small single-textbox prompt dialog shared by three commands (Build number, Bunk number, and
    /// Level number entry for the selected units) — which one is active is chosen by the
    /// <paramref name="formToDisplay"/> constructor argument ("Build"/"Bunk"/"Level"), which drives the
    /// form title, label text, and which static <c>number</c> field (on <c>UnitBuildNumber</c>,
    /// <c>UnitBunkNumber</c>, or <c>UnitLevelNumber</c>) gets written on Apply.
    /// </summary>
    public partial class BuildBunkSmallForm : Form
    {
        private int unitCount = 0;
        private string formType = "";

        /// <summary>
        /// Constructs the dialog for a given selection count (shown in the informational label) and
        /// form type ("Build", "Bunk", or "Level") that determines which downstream static field is set.
        /// </summary>
        public BuildBunkSmallForm(int selectionCount, string formToDisplay)
        {
            InitializeComponent();
            unitCount = selectionCount;
            formType = formToDisplay;
        }

        private void BuildBunkSmallForm_Load(object sender, EventArgs e)
        {
            // Match Revit's current light/dark theme
            GMSRevitAddin.DarkTheme.MarkPrimary(buttonApply);
            GMSRevitAddin.DarkTheme.Apply(this);
            this.Text = formType + " Number Tool";
            if (unitCount == 1)
            {
                labelMainText.Text = "1 unit was selected.";
            }
            else
            {
                labelMainText.Text = unitCount.ToString() + " units were selected.";
            }
            // Template label reads e.g. "Enter _ Number:" — swap in "Build"/"Bunk"/"Level".
            labelBuildBunk.Text = labelBuildBunk.Text.Replace("_", formType);
            textBoxBuildBunkNumber.Focus();
            this.AcceptButton = buttonApply;
            // Restore the form's last-used screen position, if one was saved.
            if (GMSRevitAddin.Properties.Settings.Default.BBSmallFormLocation != new System.Drawing.Point(-100, -100))
            {
                this.Location = GMSRevitAddin.Properties.Settings.Default.BBSmallFormLocation;
            }
        }

        private void buttonApply_Click(object sender, EventArgs e)
        {
            GMSRevitAddin.Properties.Settings.Default.BBSmallFormLocation = this.Location;
            GMSRevitAddin.Properties.Settings.Default.Save();

            // Non-blank entry: write the typed value into the matching command's static "number"
            // field based on which form type this dialog instance represents.
            if (!string.IsNullOrWhiteSpace(textBoxBuildBunkNumber.Text))
            {
                try
                {
                    if (formType == "Build")
                    {
                        UnitBuildNumber.UnitBuildNumber.number = textBoxBuildBunkNumber.Text;
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else if (formType == "Bunk")
                    {
                        UnitBunkNumber.UnitBunkNumber.number = textBoxBuildBunkNumber.Text;
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else if (formType == "Level")
                    {
                        UnitLevelNumber.UnitLevelNumber.number = textBoxBuildBunkNumber.Text;
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                }
                catch
                {
                    this.DialogResult = DialogResult.Abort;
                    this.Close();
                }
            }
            else
            {
                // Blank entry is still a valid "clear the value" action — write an empty string
                // rather than treating it as a validation failure.
                try
                {
                    if (formType == "Build")
                    {
                        UnitBuildNumber.UnitBuildNumber.number = "";
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else if (formType == "Bunk")
                    {
                        UnitBunkNumber.UnitBunkNumber.number = "";
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                    else if (formType == "Level")
                    {
                        UnitLevelNumber.UnitLevelNumber.number = "";
                        this.DialogResult = DialogResult.OK;
                        this.Close();
                    }
                }
                catch
                {
                    this.DialogResult = DialogResult.Abort;
                    this.Close();
                }
            }
        }
    }
}
