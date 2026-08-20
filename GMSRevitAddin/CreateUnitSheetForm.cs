using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Visual;
using Autodesk.Revit.DB.Events;
using System.Windows.Forms;
using View = Autodesk.Revit.DB.View;
using System.IO;

namespace CreateUnitSheetForm
{
    /// <summary>
    /// Modal dialog prompting for a new unit sheet number (e.g. "U-001") and, optionally (via
    /// <see cref="checkBoxDuplicate"/>), an existing unit sheet to duplicate. Its <c>DialogResult</c>
    /// plus the static fields it sets on <see cref="CreateUnitSheet.CreateUnitSheet"/>
    /// (<c>newUnitNumber</c>, <c>copyUnit</c>, <c>copyUnitNumber</c>) drive what
    /// <see cref="CreateUnitSheet.CreateUnitSheet.Execute"/> does after the dialog closes.
    /// </summary>
    public partial class CreateUnitSheetForm : System.Windows.Forms.Form
    {
        public CreateUnitSheetForm()
        {
            InitializeComponent();
            //buttonCancel.Select();
        }

        public static List<string> unitSheets = new List<string>();

        /// <summary>Placeholder shown in the new-unit-number box while it's empty and unfocused.</summary>
        private const string UnitNumberPlaceholder = "EXAMPLE: U-001";

        /// <summary>Muted hint shown under the unit-number field while its value is blank or valid;
        /// replaced by a themed error message (see <see cref="UpdateUnitNumberValidation"/>) once the
        /// user types something that would be rejected.</summary>
        private const string UnitNumberHint = "Format: contains \"U-\", no spaces or braces.";

        /// <summary>Every combo box entry (the unfiltered source that <see cref="comboBox1_TextChanged"/>
        /// filters into <c>comboBox1.Items</c>). No longer needs a blank "none" entry — whether to
        /// duplicate at all is now decided by <see cref="checkBoxDuplicate"/>, not by an empty combo.</summary>
        private readonly List<string> allComboEntries = new List<string>();

        /// <summary>Guards against re-entrancy: rewriting Items/Text re-raises TextChanged.</summary>
        private bool suppressFilter;

        /// <summary>Set when the user picks an item from the list. Choosing an entry also raises
        /// TextChanged, and re-filtering there would immediately reopen the dropdown the user just
        /// closed — so that one change is skipped.</summary>
        private bool selectionCommitted;

        /// <summary>Applies the shared theme and populates the "duplicate from" combo box with every
        /// existing unit sheet number.</summary>
        private void CreateUnitSheetForm_Load(object sender, EventArgs e)
        {
            // Marks the one accent-colored action for this dialog; must be called before Apply, which
            // reads the marker rather than pushing a live update.
            GMSRevitAddin.DarkTheme.MarkPrimary(buttonOK);
            GMSRevitAddin.DarkTheme.Apply(this);
            textBoxNewUnitNumber.Focus();
            unitSheets.Clear();
            unitSheets = CreateUnitSheet.CreateUnitSheet.getCurrentUnitSheetNames();

            // Apply() above resets ForeColor to the theme's primary text color, so the placeholder
            // and status hint have to be re-muted afterwards or they read as active values.
            if (textBoxNewUnitNumber.Text == UnitNumberPlaceholder)
            {
                textBoxNewUnitNumber.ForeColor = GMSRevitAddin.DarkTheme.CurrentMutedText;
            }
            labelDuplicateHint.ForeColor = GMSRevitAddin.DarkTheme.CurrentMutedText;

            allComboEntries.Clear();
            allComboEntries.AddRange(unitSheets);

            suppressFilter = true;
            comboBox1.Items.Clear();
            comboBox1.Items.AddRange(allComboEntries.Cast<object>().ToArray());
            suppressFilter = false;

            // Establishes the initial hint text/color and leaves Create disabled until a valid unit
            // number is entered (the field starts on the placeholder, i.e. "empty").
            UpdateUnitNumberValidation();
        }

        /// <summary>Clears the placeholder example text when the field gains focus.</summary>
        private void textBoxNewUnitNumber_FocusEnter(object sender, EventArgs e)
        {
            textBoxNewUnitNumber.ForeColor = GMSRevitAddin.DarkTheme.CurrentForeText;
            if (textBoxNewUnitNumber.Text == UnitNumberPlaceholder)
            {
                textBoxNewUnitNumber.Text = "";
            }
        }
        /// <summary>Restores the greyed-out placeholder example text if the field is left blank.</summary>
        private void textBoxNewUnitNumber_FocusLeave(object sender, EventArgs e)
        {
            if (string.IsNullOrWhiteSpace(textBoxNewUnitNumber.Text))
            {
                textBoxNewUnitNumber.ForeColor = GMSRevitAddin.DarkTheme.CurrentMutedText;
                textBoxNewUnitNumber.Text = UnitNumberPlaceholder;
            }
        }

        /// <summary>
        /// Live validation as the user types: shows an inline, themed error under the field (instead of
        /// a blocking popup) and disables <see cref="buttonOK"/> until the value is acceptable. Also
        /// fires from the placeholder swap in <see cref="textBoxNewUnitNumber_FocusEnter"/>/
        /// <see cref="textBoxNewUnitNumber_FocusLeave"/>, which is what keeps Create correctly disabled
        /// while the field shows the placeholder.
        /// </summary>
        private void textBoxNewUnitNumber_TextChanged(object sender, EventArgs e)
        {
            UpdateUnitNumberValidation();
        }

        /// <summary>Re-evaluates the new-unit-number field and updates the status label + Create button
        /// to match. A blank/placeholder value is treated as "not yet entered", not an error.</summary>
        private void UpdateUnitNumberValidation()
        {
            string raw = textBoxNewUnitNumber.Text;
            string candidate = raw == UnitNumberPlaceholder ? "" : raw.Trim();

            if (string.IsNullOrEmpty(candidate))
            {
                labelUnitStatus.Text = UnitNumberHint;
                labelUnitStatus.ForeColor = GMSRevitAddin.DarkTheme.CurrentMutedText;
                buttonOK.Enabled = false;
                return;
            }

            string error;
            bool valid = TryValidateNewUnitNumber(candidate, out error);
            labelUnitStatus.Text = valid ? UnitNumberHint : error;
            labelUnitStatus.ForeColor = valid ? GMSRevitAddin.DarkTheme.CurrentMutedText : GMSRevitAddin.DarkTheme.CurrentErrorText;
            buttonOK.Enabled = valid;
        }

        /// <summary>
        /// The single source of truth for whether a candidate new-unit-number is acceptable: must
        /// contain "U-" with no spaces/braces, and must not already name an existing unit sheet. Shared
        /// by the live validation in <see cref="UpdateUnitNumberValidation"/> and the belt-and-braces
        /// check in <see cref="buttonOK_Click"/>. <paramref name="candidate"/> is expected to already be
        /// trimmed and non-empty.
        /// </summary>
        private bool TryValidateNewUnitNumber(string candidate, out string error)
        {
            if (!candidate.Contains("U-") || candidate.Contains(" ") || candidate.Contains("{") || candidate.Contains("}"))
            {
                error = "Must contain \"U-\", with no spaces or braces.";
                return false;
            }
            if (unitSheets.Contains(candidate))
            {
                error = "A unit sheet named \"" + candidate + "\" already exists.";
                return false;
            }
            error = null;
            return true;
        }

        /// <summary>
        /// Enables/disables the "duplicate from" combo box to match the checkbox. This is now the only
        /// signal for whether to duplicate — replaces the old convention of inferring it from whether
        /// the combo box's text happened to be blank. Unchecking clears any typed/selected value so a
        /// stale duplicate-from unit can never survive an unchecked box.
        /// </summary>
        private void checkBoxDuplicate_CheckedChanged(object sender, EventArgs e)
        {
            comboBox1.Enabled = checkBoxDuplicate.Checked;
            if (checkBoxDuplicate.Checked)
            {
                comboBox1.Focus();
            }
            else
            {
                suppressFilter = true;
                comboBox1.Items.Clear();
                comboBox1.Items.AddRange(allComboEntries.Cast<object>().ToArray());
                comboBox1.Text = "";
                suppressFilter = false;
            }
        }

        /// <summary>
        /// Filters the "duplicate from" list as the user types, matching anywhere in the unit number
        /// (not just the start) — every unit shares the "U-" prefix, so typing "012" finds "U-012".
        /// Rewriting Items resets the caret, so the cursor position is captured and restored.
        /// </summary>
        private void comboBox1_TextChanged(object sender, EventArgs e)
        {
            if (suppressFilter)
            {
                return;
            }

            // A pick from the list isn't a search — leave the dropdown closed.
            if (selectionCommitted)
            {
                selectionCommitted = false;
                return;
            }

            string typed = comboBox1.Text;
            int caret = comboBox1.SelectionStart;

            List<string> matches = string.IsNullOrWhiteSpace(typed)
                ? new List<string>(allComboEntries)
                : allComboEntries.Where(s => s.IndexOf(typed, StringComparison.OrdinalIgnoreCase) >= 0).ToList();

            suppressFilter = true;
            try
            {
                comboBox1.Items.Clear();
                comboBox1.Items.AddRange(matches.Cast<object>().ToArray());
                // Items.Clear() wipes the edit text — put back exactly what was typed.
                comboBox1.Text = typed;
                // Only toggle when it isn't already open — re-setting DroppedDown mid-type flickers.
                if (matches.Count > 0 && !string.IsNullOrEmpty(typed) && !comboBox1.DroppedDown)
                {
                    comboBox1.DroppedDown = true;
                    // Opening the list programmatically while the user is typing makes Windows hide
                    // the mouse pointer (its "hide cursor while typing" behavior), and it stays
                    // hidden — so the filtered list can't be clicked. ShowCursor is ref-counted, so
                    // this is paired with the one hide caused by the line above; it sits inside the
                    // !DroppedDown guard precisely so it runs once per open, not once per keystroke.
                    System.Windows.Forms.Cursor.Show();
                }
                // DroppedDown and the Items rewrite both reset the caret to 0; without this the
                // text appears to type in reverse.
                comboBox1.SelectionStart = caret;
                comboBox1.SelectionLength = 0;
            }
            finally
            {
                suppressFilter = false;
            }
        }

        /// <summary>Flags that the next TextChanged came from picking a list item, not typing.</summary>
        private void comboBox1_SelectionChangeCommitted(object sender, EventArgs e)
        {
            selectionCommitted = true;
        }

        /// <summary>
        /// Rejects a typed unit that isn't an actual sheet before the user reaches OK. Cancel has
        /// CausesValidation = false, so an invalid value can still be abandoned. A disabled combo box
        /// never gains focus, so this naturally doesn't fire while checkBoxDuplicate is unchecked; the
        /// explicit guard below is just belt-and-braces.
        /// </summary>
        private void comboBox1_Validating(object sender, CancelEventArgs e)
        {
            if (!checkBoxDuplicate.Checked)
            {
                return;
            }

            string typed = comboBox1.Text;
            if (string.IsNullOrWhiteSpace(typed))
            {
                return;
            }

            if (!unitSheets.Any(s => string.Equals(s, typed.Trim(), StringComparison.OrdinalIgnoreCase)))
            {
                GMSRevitAddin.GmsUi.ShowWarning(
                    "\"" + typed + "\" is not an existing unit sheet." + Environment.NewLine + Environment.NewLine +
                    "Pick a unit from the list, or uncheck \"Duplicate an existing unit\" to create a blank sheet.",
                    "Invalid unit to duplicate");
                e.Cancel = true;
                comboBox1.SelectAll();
            }
        }

        /// <summary>Cancels sheet creation and closes the dialog with <see cref="DialogResult.Cancel"/>.</summary>
        private void buttonCancel_Click(object sender, EventArgs e)
        {
            CreateUnitSheet.CreateUnitSheet.newUnitNumber = "";
            this.DialogResult = DialogResult.Cancel;
            this.Close();
        }

        /// <summary>
        /// Re-validates the new unit number and, if "Duplicate an existing unit" is checked, the chosen
        /// unit to duplicate. Live validation already keeps Create disabled for an invalid number, so
        /// this is a belt-and-braces re-check (e.g. against Enter being pressed before TextChanged
        /// settles) rather than the primary gate. On success, sets the static fields
        /// <see cref="CreateUnitSheet.CreateUnitSheet"/> reads after the dialog closes and returns
        /// <see cref="DialogResult.OK"/>.
        /// </summary>
        private void buttonOK_Click(object sender, EventArgs e)
        {
            string raw = textBoxNewUnitNumber.Text;
            string newNumber = (raw == UnitNumberPlaceholder ? "" : raw).Trim();

            if (string.IsNullOrEmpty(newNumber))
            {
                GMSRevitAddin.GmsUi.ShowError("Enter a new unit number, or select Cancel.", "Error");
                return;
            }

            string numberError;
            if (!TryValidateNewUnitNumber(newNumber, out numberError))
            {
                GMSRevitAddin.GmsUi.ShowError(numberError, "Error");
                return;
            }

            string duplicateText = "";
            if (checkBoxDuplicate.Checked)
            {
                duplicateText = comboBox1.Text.Trim();
                if (string.IsNullOrWhiteSpace(duplicateText))
                {
                    GMSRevitAddin.GmsUi.ShowError("Select a unit to duplicate, or uncheck \"Duplicate an existing unit\".", "Error");
                    comboBox1.Focus();
                    return;
                }
                if (!unitSheets.Any(s => string.Equals(s, duplicateText, StringComparison.OrdinalIgnoreCase)))
                {
                    GMSRevitAddin.GmsUi.ShowError("Could not find a unit sheet named \"" + duplicateText + "\" to duplicate.", "Error");
                    comboBox1.Focus();
                    return;
                }
            }

            CreateUnitSheet.CreateUnitSheet.newUnitNumber = newNumber;
            CreateUnitSheet.CreateUnitSheet.copyUnit = checkBoxDuplicate.Checked;
            CreateUnitSheet.CreateUnitSheet.copyUnitNumber = duplicateText;
            this.DialogResult = DialogResult.OK;
            this.Close();
        }
    }
}
