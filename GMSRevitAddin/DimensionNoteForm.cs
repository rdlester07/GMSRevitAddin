using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace DimensionNotesForm
{
    /// <summary>
    /// Picker dialog for appending a standard dimension-line note (REF, ROUGH OPENING, FRAME HEIGHT,
    /// MOD, etc.) or a custom/"NOT TO SCALE" note to a selected dimension. Each preset button sets
    /// <c>DimensionNotes.DimNotes.NoteText</c> (and <c>isNotToScale</c>/<c>NotToScaleDimension</c> where
    /// relevant) and closes with <see cref="DialogResult.OK"/>; the calling command reads those static
    /// fields after <c>ShowDialog()</c> returns to apply the note to the Revit dimension.
    /// </summary>
    public partial class DimensionNoteForm : Form
    {
        public DimensionNoteForm()
        {
            InitializeComponent();
        }

        private void DimensionNoteForm_Load(object sender, EventArgs e)
        {
            // No DarkTheme.MarkPrimary call here on purpose: every preset button (REF, MOD, etc.) is
            // its own equal-weight "pick this note" action that immediately closes the dialog, so this
            // is a selector grid, not a fill-a-field-then-submit dialog — there's no single button that
            // deserves to stand out from the rest.
            // Match Revit's current light/dark theme
            GMSRevitAddin.DarkTheme.Apply(this);
            this.FormClosing += OnFormClosing;
            // Restore the form's last-used screen position, if one was saved.
            if (GMSRevitAddin.Properties.Settings.Default.DimNotFormLocation != new System.Drawing.Point(-100,-100))
            {
                this.Location = GMSRevitAddin.Properties.Settings.Default.DimNotFormLocation;
            }
            textBoxCustom.Focus();
        }

        // While the "Not To Scale" text box has focus, Enter should trigger its own Apply button
        // instead of the form's default (buttonApply for the custom-text box).
        private void textBoxNotToScale_FocusEnter(object sender, EventArgs e)
        {
            this.AcceptButton = buttonNotToScale;
        }
        private void textBoxNotToScale_FocusLeave(object sender, EventArgs e)
        {
            this.AcceptButton = buttonApply;
        }


        /// <summary>Persists the form's current screen position so it reopens in the same spot.</summary>
        public void OnFormClosing(object sender, EventArgs e)
        {
            GMSRevitAddin.Properties.Settings.Default.DimNotFormLocation = this.Location;
        }

        // The following buttonXxx_Click handlers are all the same pattern: set the shared static
        // NoteText (and isNotToScale = false, since these are all fixed presets) then close OK so
        // the calling command applies the chosen note text to the selected dimension.
        private void buttonRef_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "REF";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();
        }

        private void buttonRO_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "ROUGH OPENING";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        private void buttonFrameHeight_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "FRAME HEIGHT";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        private void buttonFrameWidth_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "FRAME WIDTH";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        private void buttonMullionLength_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "MULLION LENGTH";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        private void buttonDoorFrameOpening_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "DOOR FRAME OPENING";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        private void buttonUnitHeight_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "UNIT HEIGHT";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        private void buttonUnitWidth_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "UNIT WIDTH";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        private void buttonMod_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "MOD";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        private void buttonMullionLength2_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "MULLION LENGTH";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        private void buttonDLO_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "DLO";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        private void buttonGlass_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "GLASS";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        private void buttonNotch_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "NOTCH";
            DimensionNotes.DimNotes.isNotToScale = false;

            this.DialogResult = DialogResult.OK;
            this.Close();

        }

        // "Not To Scale" needs a free-text dimension value alongside the fixed note text, so it
        // validates the text box is non-empty before setting isNotToScale/NotToScaleDimension.
        private void buttonNotToScale_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(textBoxNotToScale.Text))
            {
                DimensionNotes.DimNotes.NoteText = "NOT TO SCALE";
                DimensionNotes.DimNotes.isNotToScale = true;
                DimensionNotes.DimNotes.NotToScaleDimension = textBoxNotToScale.Text;

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Text cannot be blank. Please enter the Not To Scale dimension.");
            }
        }

        // Free-form custom note text, validated non-empty the same way as the presets above.
        private void buttonApply_Click(object sender, EventArgs e)
        {
            if (!string.IsNullOrWhiteSpace(textBoxCustom.Text))
            {
                DimensionNotes.DimNotes.NoteText = textBoxCustom.Text;
                DimensionNotes.DimNotes.isNotToScale = false;

                this.DialogResult = DialogResult.OK;
                this.Close();
            }
            else
            {
                MessageBox.Show("Text cannot be blank. Please enter the text to add to the dimension.");
            }

        }

        // Clears any note text/flags and closes with Ignore so the caller knows to remove/skip
        // the dimension note rather than apply one.
        private void buttonReset_Click(object sender, EventArgs e)
        {
            DimensionNotes.DimNotes.NoteText = "";
            DimensionNotes.DimNotes.isNotToScale = false;
            DimensionNotes.DimNotes.NotToScaleDimension = "";
            this.DialogResult = DialogResult.Ignore;
            this.Close();
        }
    }
}
