using System;
using System.Collections.Generic;
using System.Data.Odbc;
using System.IO;
using System.Linq;
using System.Text;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace UpdateFramingWeights
{
    /// <summary>
    /// Scans every generic-model family TYPE in the active document. For any type that has the
    /// "Framing - Weight / Ft" parameter, it reads the type's "Framing - Die Number", looks the die
    /// up in the GMS Extrusion Access DB ("Extrusion Data" table, matched on "Die Number"), and writes
    /// the DB "Weight" into "Framing - Weight / Ft" and "Alloy"-"Temper" into "Framing - Alloy / Temper".
    /// Type parameters; all generic-model types; unmatched/blank dies are skipped and reported.
    /// Wired in GMS_tools.cs as "UpdateFramingWeights.UpdateWeights".
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class UpdateWeights : IExternalCommand
    {
        // Revit family TYPE parameters (exact spellings per the team).
        private const string WeightParam = "Framing - Weight / Ft";
        private const string AlloyTemperParam = "Framing - Alloy / Temper";
        private const string DieParam = "Framing - Die Number";

        // Access DB schema.
        private const string Table = "Extrusion Data";
        private const string DieField = "Die Number";
        private const string WeightField = "Weight";
        private const string AlloyField = "Alloy";
        private const string TemperField = "Temper";

        private class ExtrusionRow
        {
            public double? Weight;
            public string Alloy = "";
            public string Temper = "";
        }

        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            try
            {
                Document doc = commandData.Application.ActiveUIDocument.Document;

                // 1. Collect generic-model TYPES that carry the weight parameter, paired with their die.
                var targets = new List<(FamilySymbol Symbol, string Die)>();
                var blankDie = new List<string>();   // family type names with the param but no die

                var symbols = new FilteredElementCollector(doc)
                    .OfCategory(BuiltInCategory.OST_GenericModel)
                    .WhereElementIsElementType()
                    .OfClass(typeof(FamilySymbol))
                    .Cast<FamilySymbol>();

                foreach (FamilySymbol fs in symbols)
                {
                    if (fs.LookupParameter(WeightParam) == null)
                        continue;   // not a framing extrusion family — leave untouched

                    Parameter dp = fs.LookupParameter(DieParam);
                    string die = dp == null ? null : (dp.AsString() ?? dp.AsValueString());
                    die = die?.Trim();
                    if (string.IsNullOrWhiteSpace(die))
                    {
                        blankDie.Add(fs.FamilyName + " : " + fs.Name);
                        continue;
                    }
                    targets.Add((fs, die));
                }

                if (targets.Count == 0 && blankDie.Count == 0)
                {
                    GMSRevitAddin.GmsUi.Show("No generic-model family types with a \"" + WeightParam + "\" parameter were found.", "Update Framing Weights");
                    return Result.Succeeded;
                }

                var distinctDies = targets.Select(t => t.Die).Distinct(StringComparer.OrdinalIgnoreCase).ToList();

                int updated = 0;
                var unmatched = new SortedSet<string>(StringComparer.OrdinalIgnoreCase);

                // Modeless progress bar spanning the DB reads (one per distinct die) + the per-type writes.
                var progress = new ProgressForm.ProgressForm("Update Framing Weights", null, distinctDies.Count + targets.Count, "Done");
                try
                {
                    // 2. Query the Extrusion DB once per distinct die (reads — no transaction needed).
                    Dictionary<string, ExtrusionRow> dbRows;
                    try
                    {
                        dbRows = QueryExtrusionData(distinctDies, progress);
                    }
                    catch (Exception ex)
                    {
                        GMSRevitAddin.GmsLog.Error("UpdateFramingWeights.Query", ex);
                        GMSRevitAddin.GmsUi.ShowError("Error reading the extrusion database." + Environment.NewLine + ex.Message, "Update Framing Weights", ex);
                        return Result.Failed;
                    }

                    // 3. Write the values back into the family types in a single transaction.
                    using (Transaction t = new Transaction(doc, "Update Framing Weights"))
                    {
                        t.Start();
                        foreach (var (fs, die) in targets)
                        {
                            progress.IncrementWithText("Updating " + fs.FamilyName + " : " + fs.Name);

                            if (!dbRows.TryGetValue(die, out ExtrusionRow row) || row == null || row.Weight == null)
                            {
                                unmatched.Add(die);
                                continue;
                            }

                            bool changed = SetWeight(fs, row.Weight.Value);

                            string at = JoinAlloyTemper(row.Alloy, row.Temper);
                            if (!string.IsNullOrEmpty(at) && fs.LookupParameter(AlloyTemperParam) != null)
                                changed |= GMSRevitAddin.RevitParameterHelper.TrySetString(fs, AlloyTemperParam, at);

                            if (changed) updated++;
                        }
                        t.Commit();
                    }
                }
                finally
                {
                    progress.Close();
                }

                // 4. Report.
                var sb = new StringBuilder();
                sb.AppendLine("Updated " + updated + " family type" + (updated == 1 ? "" : "s") + " (weight + alloy/temper).");
                if (unmatched.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Skipped " + unmatched.Count + " die number" + (unmatched.Count == 1 ? "" : "s") + " not found in the database:");
                    sb.AppendLine(string.Join(", ", unmatched));
                }
                if (blankDie.Count > 0)
                {
                    sb.AppendLine();
                    sb.AppendLine("Skipped " + blankDie.Count + " family type" + (blankDie.Count == 1 ? "" : "s") + " with a blank \"" + DieParam + "\".");
                }
                GMSRevitAddin.GmsLog.Info("UpdateFramingWeights: updated=" + updated + ", unmatched=" + unmatched.Count + ", blankDie=" + blankDie.Count);
                GMSRevitAddin.GmsUi.Show(sb.ToString(), "Update Framing Weights");

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                GMSRevitAddin.GmsLog.Error("UpdateFramingWeights", ex);
                GMSRevitAddin.GmsUi.ShowError(ex, "Update Framing Weights Error");
                return Result.Failed;
            }
        }

        /// <summary>
        /// Copies the extrusion DB to %TEMP%\GMS and queries Weight/Alloy/Temper for each die via ODBC.
        /// Mirrors the connection-string + ACE-lock-release workaround used in UpdateSchedules.cs.
        /// </summary>
        private static Dictionary<string, ExtrusionRow> QueryExtrusionData(List<string> dies, ProgressForm.ProgressForm progress)
        {
            string dbPath = GMSRevitAddin.GmsPaths.ExtrusionDatabase;
            if (!File.Exists(dbPath))
                throw new FileNotFoundException("Could not locate the extrusion database: " + dbPath);

            string gmsTemp = Path.Combine(Path.GetTempPath(), "GMS");
            if (!Directory.Exists(gmsTemp)) Directory.CreateDirectory(gmsTemp);
            string tempDb = Path.Combine(gmsTemp, "GMS Extrusion Database.accdb");
            File.Copy(dbPath, tempDb, true);

            var result = new Dictionary<string, ExtrusionRow>(StringComparer.OrdinalIgnoreCase);
            // ODBC connection string (NOT the OLEDB "Provider=..." form). Requires the Microsoft Access
            // ODBC driver (Access Database Engine redistributable).
            string connStr = @"Driver={Microsoft Access Driver (*.mdb, *.accdb)};Dbq=" + tempDb + ";";

            using (OdbcConnection cn = new OdbcConnection(connStr))
            {
                cn.Open();
                string sql = "SELECT [" + WeightField + "], [" + AlloyField + "], [" + TemperField + "] FROM [" + Table + "] WHERE [" + DieField + "] = ?";
                foreach (string die in dies)
                {
                    progress?.IncrementWithText("Reading die " + die);
                    using (OdbcCommand cmd = new OdbcCommand(sql, cn))
                    {
                        cmd.Parameters.AddWithValue("@die", die);   // parameterized — die text is arbitrary
                        using (OdbcDataReader reader = cmd.ExecuteReader())
                        {
                            if (reader.Read() && !result.ContainsKey(die))
                            {
                                var row = new ExtrusionRow
                                {
                                    Weight = ParseWeight(reader.IsDBNull(0) ? null : reader.GetValue(0)),
                                    Alloy = reader.IsDBNull(1) ? "" : reader.GetValue(1).ToString().Trim(),
                                    Temper = reader.IsDBNull(2) ? "" : reader.GetValue(2).ToString().Trim()
                                };
                                result[die] = row;
                            }
                        }
                    }
                }
                cn.Close();
            }

            // ACE file-lock release (same workaround as UpdateSchedules).
            System.Threading.Thread.Sleep(500);
            OdbcConnection.ReleaseObjectPool();
            GC.Collect();

            return result;
        }

        private static double? ParseWeight(object value)
        {
            if (value == null) return null;
            string s = value.ToString().Trim();
            if (s == "") return null;
            return double.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out double w) ? (double?)w : null;
        }

        /// <summary>Joins Alloy and Temper with a "-". Either blank → the other alone; both blank → "".</summary>
        private static string JoinAlloyTemper(string alloy, string temper)
        {
            alloy = (alloy ?? "").Trim();
            temper = (temper ?? "").Trim();
            if (alloy != "" && temper != "") return alloy + "-" + temper;
            return alloy != "" ? alloy : temper;
        }

        /// <summary>Sets the weight parameter respecting its storage type. Returns true if set.</summary>
        private static bool SetWeight(FamilySymbol fs, double weight)
        {
            Parameter p = fs.LookupParameter(WeightParam);
            if (p == null || p.IsReadOnly) return false;
            try
            {
                switch (p.StorageType)
                {
                    case StorageType.Double: return p.Set(weight);
                    case StorageType.Integer: return p.Set((int)Math.Round(weight));
                    case StorageType.String: return p.Set(weight.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    default: return false;
                }
            }
            catch (Exception ex)
            {
                GMSRevitAddin.GmsLog.Error("UpdateFramingWeights.SetWeight " + fs.FamilyName, ex);
                return false;
            }
        }
    }
}
