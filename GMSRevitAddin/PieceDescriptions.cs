using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Visual;
using System.IO;
using System.Windows.Forms;
using Application = Autodesk.Revit.ApplicationServices.Application;
using View = Autodesk.Revit.DB.View;

namespace PieceDescriptions
{

    /// <summary>
    /// Looks up the human-readable description for a piece-type abbreviation key (e.g. "HM" ->
    /// "HORIZONTAL MULLION"), used when generating piece tags/descriptions. Reads overrides from a
    /// "key=description" text file (<c>GmsPaths.PieceDescriptionsFile</c>) if present, falling back
    /// to a hard-coded default dictionary if the file is missing or fails to parse.
    /// </summary>
    public class PieceDescriptions
    {
        /// <summary>
        /// Returns the description text for the given piece-type key, or "Description not found."
        /// if the key has no match in the loaded dictionary.
        /// </summary>
        public static string pieceDefinition(string key)
        {
            bool loadDefault = false;
            string descriptionsFile = GMSRevitAddin.GmsPaths.PieceDescriptionsFile;
            Dictionary<string, string> defDictionary = new Dictionary<string, string>();

            if (File.Exists(descriptionsFile))
            {
                try
                {
                    // Parse "key=description" lines from the override file into the dictionary.
                    StreamReader sr = new StreamReader(descriptionsFile);
                    var lines = new List<string>();

                    while (!sr.EndOfStream)
                    {
                        string line1 = sr.ReadLine();
                        if (line1.Contains("="))
                        {
                            lines.Add(line1);
                        }
                    }

                    foreach (string desc in lines)
                    {
                        string[] temp = desc.Split('=');
                        defDictionary.Add(temp[0], temp[1]);
                    }
                }
                catch
                {
                    // File exists but couldn't be parsed — fall back to the hard-coded defaults below.
                    loadDefault = true;
                    MessageBox.Show("Unable to load Piece Description file. Defaults will be used instead", "Warning!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                }
            }
            else if (!File.Exists(descriptionsFile) || loadDefault)
            {
                // No override file (or it failed to load) — use the built-in default abbreviation map.
                defDictionary.Clear();
                defDictionary = new Dictionary<string, string>()
                {
                    {"AA", "ALUMINUM ANCHOR" },
                    {"AC", "ALUMINUM COPING" },
                    {"AF", "ANCHOR FIST" },
                    {"AH", "ANCHOR HOOK" },
                    {"AL", "ANCHOR LATERAL CLIP" },
                    {"AP", "ALUMINUM PIECE" },
                    {"AS", "ALUMINUM SOFFIT PIECE" },
                    {"BM", "BRAKE METAL" },
                    {"BP", "BACK PAN" },
                    {"CP", "CLOSURE PIECE" },
                    {"CS", "COVER SPLICE" },
                    {"CV", "CARTERSVILLE PIECE" },
                    {"DS", "DEADLOAD SHIM" },
                    {"EC", "END CAP" },
                    {"FC", "FLOOR CLOSURE" },
                    {"GC", "GLAZING CHAIR" },
                    {"HA", "HORIZONTAL ADAPTER" },
                    {"HB", "HORIZONTAL BEAD" },
                    {"HC", "HORIZONTAL COVER" },
                    {"HE", "HORIZONTAL STOOL EXTENSION" },
                    {"HM", "HORIZONTAL MULLION" },
                    {"HP", "HORIZONTAL PRESSURE BAR" },
                    {"HS", "HORIZONTAL STOOL TRIM" },
                    {"HT", "HORIZONTAL CEILING TRIM" },
                    {"IA", "INSULATION GALVANIZED ANGLE" },
                    {"IN", "INSULATION" },
                    {"LL", "LIFTING LUG" },
                    {"LO", "LOUVER" },
                    {"LP", "LOUVER WITH PANEL" },
                    {"P", "PART - SHOP ATTACHED" },
                    {"PB", "PRESSURE BAR" },
                    {"PF", "POCKET FILLER" },
                    {"PL", "PLASTIC PIECE" },
                    {"PN", "ALUMINUM INFILL PANEL" },
                    {"PP", "SUBPART - SHOP ATTACHED" },
                    {"SA", "STEEL ANCHOR" },
                    {"SB", "SHADOW BOX PANEL" },
                    {"SC", "SOFFIT CLOSURE" },
                    {"SK", "STEEL KICKER" },
                    {"SP", "STEEL PIECE" },
                    {"SS", "STARTER SILL HORIZONTAL" },
                    {"SV", "STEEL VERTICAL" },
                    {"SW", "SERRATED ANCHOR WASHER" },
                    {"TE", "TRIM EXTENSION" },
                    {"TS", "TRIM SPLICE" },
                    {"VA", "VERTICAL ADAPTER" },
                    {"VB", "VERTICAL BEAD" },
                    {"VC", "VERTICAL COVER" },
                    {"VE", "VERTICAL TRIM EXTENSION" },
                    {"VM", "VERTICAL MULLION" },
                    {"VP", "VERTICAL PRESSURE BAR" },
                    {"VT", "VERTICAL TRIM" },

                    {"CU", "CUSTOMER PIECE" },
                    {"GK", "GASKET AT INFILL" },
                    {"GL", "GLAZING INFILL" },
                    {"SU", "SUBUNIT INFILL" }
                };
            }

            string result = string.Empty;
            // Key lookup can throw (e.g. KeyNotFoundException) — caught and logged; result stays empty.
            try
            {
                result = defDictionary[key];
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("PieceDescriptions", __ex); }

            if (string.IsNullOrWhiteSpace(result))
            {
                return "Description not found.";
            }
            else
            {
                return result;
            }
        }
    }
}