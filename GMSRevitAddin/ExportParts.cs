using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Data.Odbc;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace ExportParts
{
    // ====================================================================================================
    //  EXPORT PARTS COMMAND
    // ----------------------------------------------------------------------------------------------------
    //  Workflow:
    //      1. Run UpdateSchedules.UpdateAll
    //      2. Locate existing schedule
    //      3. Determine job number
    //      4. Resolve the per-job Access database path on J:\
    //      5. Read the schedule rows directly via the Revit API
    //      6. Write the rows into Access: clear the table, then insert (full snapshot each run)
    //
    //  Replaces the former flat-text export (RevitPieceCollection.txt). Data goes straight into the
    //  per-job Microsoft Access database; the schedule is read with GetCellText(SectionType.Body, ...)
    //  so Revit's title row never appears and no text post-processing is needed.
    //
    //  NOTE:
    //      Legacy fields contExport and partsManagerLocation are preserved
    //      because other project files reference them.
    // ====================================================================================================
    /// <summary>
    /// Reads the "(DO NOT OPEN) Parts Collection - Pieces" schedule directly via the Revit API and
    /// writes a full snapshot of its rows into the per-job Access database (path resolved from the
    /// "Parts Manager Location" GlobalParameter). Wired to the "Export Pieces" ribbon button via
    /// <see cref="ExportPieces"/>, which runs the doc-wide tag-origin update first, then this command.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    public class Export : IExternalCommand
    {
        // ======================================================================
        // LEGACY PLACEHOLDERS — REQUIRED BY OTHER PROJECT FILES
        // ======================================================================
        public static bool contExport = true;
        public static string partsManagerLocation = "";

        // ======================================================================
        // ACCESS TARGET CONFIGURATION — FILL THESE IN
        // ----------------------------------------------------------------------
        //  These describe the per-job Access database the schedule is written to.
        //  DbRelativePath is combined with the J:\ project root resolved from the
        //  job number. TableName / ColumnNames define the destination table; the
        //  schedule's body columns are inserted left-to-right into ColumnNames,
        //  so the order here must match the schedule's column order.
        // ======================================================================
        //  The database path itself comes from the "Parts Manager Location" Revit GlobalParameter
        //  (set via the GMS Settings form → "Project Parts Manager Location"); see GetPartsManagerDbPath.
        private const string TableName = "PieceInstance";
        private static readonly string[] ColumnNames = new[]
        {
            // Order must match the schedule's visible column order.
            "Prefix", "Number", "Quantity", "Length", "Material", "Origin", "OriginSheet", "DescID"
        };

        // Skip body row 0 (the schedule's column-header row) when inserting data rows.
        private const bool SkipHeaderRow = true;

        private static Document doc;
        private static UIApplication uiapp;

        // ====================================================================================================
        //  EXECUTE — MAIN ENTRY POINT
        // ====================================================================================================
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            uiapp = commandData.Application;
            doc = uiapp.ActiveUIDocument.Document;

            try
            {
                // ============================================================================================
                //  REGION 1 — RUN UPDATE SCHEDULES  (DISABLED 2026-06-26 at user request)
                // --------------------------------------------------------------------------------------------
                //  UpdateSchedules was retired from the GMS toolbar; Export Pieces no longer triggers it.
                //  To restore the pre-export schedule refresh, uncomment the block below (and re-enable the
                //  ribbon button in GMS_tools.cs).
                // ============================================================================================
                #region RUN_UPDATE_SCHEDULES

                //try
                //{
                //    var updateCmd = new UpdateSchedules.UpdateAll();
                //    updateCmd.Execute(commandData, ref message, elements);
                //}
                //catch (Exception exUpdate)
                //{
                //    // Ignore failures — export should still run, but record why.
                //    GMSRevitAddin.GmsLog.Error("ExportParts.UpdateAll", exUpdate);
                //}

                #endregion



                // ============================================================================================
                //  REGION 2 — FIND EXISTING SCHEDULE
                // ============================================================================================
                #region FIND_SCHEDULE

                const string scheduleName = "(DO NOT OPEN) Parts Collection - Pieces";

                ViewSchedule schedule = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewSchedule))
                    .Cast<ViewSchedule>()
                    .FirstOrDefault(vs => vs.Name.Equals(scheduleName, StringComparison.Ordinal));

                if (schedule == null)
                {
                    message = "Schedule not found: " + scheduleName;
                    return Result.Failed;
                }

                #endregion



                // ============================================================================================
                //  REGION 3 — RESOLVE ACCESS DATABASE PATH (from "Parts Manager Location" GlobalParameter)
                // ============================================================================================
                #region RESOLVE_DB_PATH

                string dbPath = GetPartsManagerDbPath(doc);

                if (string.IsNullOrWhiteSpace(dbPath))
                {
                    message = "The Project Parts Manager Location is not set. Set it on the GMS Settings form.";
                    GMSRevitAddin.GmsUi.ShowError(message, "Export Parts Error");
                    return Result.Failed;
                }

                if (!File.Exists(dbPath))
                {
                    message = "Parts Manager not found: " + dbPath;
                    GMSRevitAddin.GmsUi.ShowError(message, "Export Parts Error");
                    return Result.Failed;
                }

                #endregion



                // ============================================================================================
                //  REGION 5 — READ SCHEDULE ROWS VIA THE REVIT API
                // ============================================================================================
                #region READ_SCHEDULE

                List<List<string>> rows = ReadScheduleRows(schedule);

                if (rows.Count == 0)
                {
                    GMSRevitAddin.GmsUi.Show("The schedule contained no rows to export.", "Export Parts");
                    return Result.Cancelled;
                }

                #endregion



                // ============================================================================================
                //  REGION 6 — WRITE TO ACCESS (clear + insert, full snapshot)
                // ============================================================================================
                #region WRITE_ACCESS

                int written = WriteRowsToAccess(dbPath, rows, out List<List<string>> skippedRows);
                int skipped = skippedRows.Count;

                #endregion

                string summary = "Wrote " + written + " piece" + (written == 1 ? "" : "s") + " to:" +
                    Environment.NewLine + dbPath;

                if (skipped > 0)
                {
                    // Show a modeless results form listing the skipped pieces so the user can click
                    // through and fix each instance in the model. The grid lists one row per tag
                    // instance, so its count (pieces.Count) can exceed the schedule-row skip count.
                    List<SkippedPiece> pieces = ResolveSkippedPieces(schedule, skippedRows);
                    summary += Environment.NewLine + Environment.NewLine +
                        "Skipped " + skipped + " piece" + (skipped == 1 ? "" : "s") +
                        " with a non-numeric Number/Quantity (" + pieces.Count + " tag instance" +
                        (pieces.Count == 1 ? "" : "s") + " listed below):";
                    ExportResultsController.ShowResults(summary, pieces, uiapp.ActiveUIDocument, GMSRevitAddin.GmsUi.Owner);
                }
                else
                {
                    GMSRevitAddin.GmsUi.Show(summary, "Export Parts");
                }

                return Result.Succeeded;
            }
            catch (Exception ex)
            {
                GMSRevitAddin.GmsLog.Error("ExportParts", ex);
                GMSRevitAddin.GmsUi.ShowError(ex, "Export Parts Error");
                message = ex.Message;
                return Result.Failed;
            }
        }



        // ====================================================================================================
        //  REGION — READ SCHEDULE ROWS
        // ====================================================================================================
        #region READ_SCHEDULE_ROWS

        /// <summary>
        /// Reads the schedule body grid via the Revit API. Returns one inner list per row, each holding
        /// the cell text left-to-right. Honors <see cref="SkipHeaderRow"/> and drops fully-blank rows.
        /// </summary>
        private static List<List<string>> ReadScheduleRows(ViewSchedule schedule)
        {
            var result = new List<List<string>>();

            TableData table = schedule.GetTableData();
            TableSectionData body = table.GetSectionData(SectionType.Body);
            int rowCount = body.NumberOfRows;
            int colCount = body.NumberOfColumns;

            // Diagnostic: record the schedule's actual column layout so a mismatch against the
            // destination table's column order (which causes [22018] when text lands in a numeric
            // column) is visible in the log. Logs the header row (body row 0) left-to-right.
            var headerCells = new List<string>(colCount);
            for (int c = 0; c < colCount; c++)
                headerCells.Add(schedule.GetCellText(SectionType.Body, 0, c) ?? "");
            GMSRevitAddin.GmsLog.Info("ExportParts schedule has " + colCount + " columns; destination expects " +
                ColumnNames.Length + " (" + string.Join(", ", ColumnNames) + "). Schedule header row: [" +
                string.Join(" | ", headerCells) + "]");

            int startRow = SkipHeaderRow ? 1 : 0;
            for (int r = startRow; r < rowCount; r++)
            {
                var cells = new List<string>(colCount);
                for (int c = 0; c < colCount; c++)
                {
                    string text = schedule.GetCellText(SectionType.Body, r, c);
                    cells.Add(text == null ? "" : text.Trim());
                }

                // Skip fully-blank rows (mirror the old text-export cleanup intent).
                if (cells.All(string.IsNullOrWhiteSpace))
                    continue;

                result.Add(cells);
            }

            return result;
        }

        #endregion



        // ====================================================================================================
        //  REGION — WRITE ROWS TO ACCESS (clear + insert)
        // ====================================================================================================
        #region WRITE_ROWS_TO_ACCESS

        /// <summary>
        /// Connects directly to the per-job Access database, then in a SINGLE transaction clears
        /// <see cref="TableName"/> and inserts every schedule row using a PARAMETERIZED statement
        /// (schedule cell text is arbitrary and may contain quotes/commas). The transaction makes the
        /// write all-or-nothing: a mid-write failure rolls the table back to its prior contents rather
        /// than leaving a half-populated snapshot. Mirrors the ACE-lock release dance used in
        /// UpdateSchedules. Returns the number of rows inserted.
        /// </summary>
        private static int WriteRowsToAccess(string dbPath, List<List<string>> rows, out List<List<string>> skippedRows)
        {
            string connStr = @"Driver={Microsoft Access Driver (*.mdb, *.accdb)};Dbq=" + dbPath + ";";

            string columnList = string.Join(",", ColumnNames.Select(c => "[" + c + "]"));
            string placeholders = string.Join(",", ColumnNames.Select(_ => "?"));
            string insertSql = "INSERT INTO [" + TableName + "] (" + columnList + ") VALUES (" + placeholders + ")";

            int inserted = 0;
            skippedRows = new List<List<string>>();
            try
            {
                using (OdbcConnection cn = new OdbcConnection(connStr))
                {
                    try
                    {
                        cn.Open();
                    }
                    catch (OdbcException exOpen)
                    {
                        // Could not even open the database — most often it's open exclusively elsewhere.
                        throw DescribeAccessException(exOpen, dbPath);
                    }

                    // Discover each destination column's actual CLR type so numeric/date columns are bound
                    // with the right type instead of raw schedule text (Access rejects text→Number with
                    // [22018] "data type mismatch"). Falls back to string when a type can't be resolved.
                    Type[] columnTypes = GetColumnTypes(cn);

                    using (OdbcTransaction tx = cn.BeginTransaction())
                    {
                        int rowIndex = -1;
                        try
                        {
                            using (OdbcCommand del = new OdbcCommand("DELETE FROM [" + TableName + "]", cn, tx))
                                del.ExecuteNonQuery();

                            for (rowIndex = 0; rowIndex < rows.Count; rowIndex++)
                            {
                                List<string> row = rows[rowIndex];

                                // Skip rows whose value for a numeric column (Number/Quantity) can't be
                                // converted to a number — Access would reject them with [22018] and abort the
                                // whole write. Skipping keeps the export going for the remaining pieces.
                                string skipReason = RowSkipReason(row, columnTypes);
                                if (skipReason != null)
                                {
                                    skippedRows.Add(row);
                                    GMSRevitAddin.GmsLog.Info("ExportParts skipped row " + rowIndex + ": " + skipReason);
                                    continue;
                                }

                                using (OdbcCommand cmd = new OdbcCommand(insertSql, cn, tx))
                                {
                                    // ODBC binds parameters positionally; add exactly ColumnNames.Length of them,
                                    // coerced to each column's type (blanks -> NULL).
                                    for (int i = 0; i < ColumnNames.Length; i++)
                                    {
                                        string raw = i < row.Count ? row[i] : null;
                                        cmd.Parameters.AddWithValue("@p" + i, CoerceForColumn(raw, columnTypes[i]));
                                    }
                                    cmd.ExecuteNonQuery();
                                    inserted++;
                                }
                            }

                            tx.Commit();
                        }
                        catch (OdbcException exWrite)
                        {
                            // Roll the table back to its prior contents, then surface a clear message.
                            try { tx.Rollback(); }
                            catch (Exception exRollback) { GMSRevitAddin.GmsLog.Error("ExportParts.Rollback", exRollback); }

                            // Diagnostic: log the exact row + column=value(type) that Access rejected, so a
                            // column-order mismatch or bad cell value is immediately identifiable.
                            if (rowIndex >= 0 && rowIndex < rows.Count)
                            {
                                List<string> bad = rows[rowIndex];
                                string detail = string.Join(" | ", ColumnNames.Select((c, i) =>
                                    c + "[" + (columnTypes[i] != null ? columnTypes[i].Name : "?") + "]='" +
                                    (i < bad.Count ? bad[i] : "<missing>") + "'"));
                                GMSRevitAddin.GmsLog.Error("ExportParts insert failed at row " + rowIndex + ": " + detail, exWrite);
                            }

                            throw DescribeAccessException(exWrite, dbPath);
                        }
                    }

                    cn.Close();
                }
            }
            finally
            {
                // Release ACE's file handles so the .accdb isn't left locked (matches UpdateSchedules).
                System.Threading.Thread.Sleep(500);
                OdbcConnection.ReleaseObjectPool();
                System.GC.Collect();
            }

            return inserted;
        }

        /// <summary>
        /// Reads the destination table's schema (no rows) and returns the CLR type of each entry in
        /// <see cref="ColumnNames"/>, in the same order. Columns whose type cannot be resolved default to
        /// <see cref="string"/> so binding still falls back to text.
        /// </summary>
        private static Type[] GetColumnTypes(OdbcConnection cn)
        {
            var map = new Dictionary<string, Type>(StringComparer.OrdinalIgnoreCase);
            try
            {
                using (OdbcCommand cmd = new OdbcCommand("SELECT * FROM [" + TableName + "] WHERE 1 = 0", cn))
                using (OdbcDataReader reader = cmd.ExecuteReader(System.Data.CommandBehavior.SchemaOnly))
                {
                    for (int i = 0; i < reader.FieldCount; i++)
                        map[reader.GetName(i)] = reader.GetFieldType(i);
                }
            }
            catch (Exception ex)
            {
                // Non-fatal: fall back to text binding for every column.
                GMSRevitAddin.GmsLog.Error("ExportParts.GetColumnTypes", ex);
            }

            return ColumnNames
                .Select(c => map.TryGetValue(c, out Type t) ? t : typeof(string))
                .ToArray();
        }

        /// <summary>
        /// Returns null when the row is safe to insert, otherwise a short reason it should be skipped:
        /// a numeric destination column (e.g. Number/Quantity) holds a non-empty value that can't be
        /// converted to a number (Access would reject it with [22018]). Blank numeric cells are allowed
        /// (they bind as NULL).
        /// </summary>
        private static string RowSkipReason(List<string> row, Type[] columnTypes)
        {
            for (int i = 0; i < ColumnNames.Length; i++)
            {
                if (!IsNumericType(columnTypes[i]))
                    continue;

                string v = i < row.Count && row[i] != null ? row[i].Trim() : "";
                if (v.Length == 0)
                    continue; // blank -> NULL, allowed

                try
                {
                    Convert.ChangeType(v, columnTypes[i], System.Globalization.CultureInfo.InvariantCulture);
                }
                catch
                {
                    return ColumnNames[i] + "='" + v + "' is not a valid number";
                }
            }
            return null;
        }

        private static bool IsNumericType(Type t)
        {
            return t == typeof(int) || t == typeof(long) || t == typeof(short) || t == typeof(byte)
                || t == typeof(double) || t == typeof(float) || t == typeof(decimal);
        }

        /// <summary>
        /// Converts a raw schedule cell to the value bound for its destination column: blank -> DBNull,
        /// text columns -> the trimmed string, numeric/date columns -> the parsed value of that type.
        /// Anything that fails to parse falls back to the raw string (letting the driver report it).
        /// </summary>
        private static object CoerceForColumn(string raw, Type targetType)
        {
            string v = raw == null ? null : raw.Trim();
            if (string.IsNullOrEmpty(v))
                return DBNull.Value;

            if (targetType == null || targetType == typeof(string))
                return v;

            try
            {
                return Convert.ChangeType(v, targetType, System.Globalization.CultureInfo.InvariantCulture);
            }
            catch
            {
                return v; // last resort — let Access surface the mismatch rather than crashing here
            }
        }

        /// <summary>
        /// Builds the results-form model for the skipped rows: ONE entry per matching tag instance (not per
        /// schedule row), so the form can select/zoom to a single instance at a time. Instances are matched
        /// from the schedule's elements by the "Prefix" + "Number" parameters; each instance row carries that
        /// instance's own "Origin"/"Origin Sheet" parameters. A row with no matching instance is kept once,
        /// using the schedule-row cell text, and stays non-clickable.
        /// </summary>
        private static List<SkippedPiece> ResolveSkippedPieces(ViewSchedule schedule, List<List<string>> skippedRows)
        {
            int iPrefix = Array.IndexOf(ColumnNames, "Prefix");
            int iNumber = Array.IndexOf(ColumnNames, "Number");
            int iOrigin = Array.IndexOf(ColumnNames, "Origin");
            int iOriginSheet = Array.IndexOf(ColumnNames, "OriginSheet");

            // Map (Prefix|Number) -> matching instance ids, from the elements the schedule includes.
            Dictionary<string, List<ElementId>> byKey =
                new Dictionary<string, List<ElementId>>(StringComparer.OrdinalIgnoreCase);
            try
            {
                foreach (FamilyInstance fi in new FilteredElementCollector(doc, schedule.Id)
                    .OfClass(typeof(FamilyInstance)).Cast<FamilyInstance>())
                {
                    Parameter pp = fi.LookupParameter("Prefix");
                    Parameter np = fi.LookupParameter("Number");
                    string pfx = (pp != null ? (pp.AsString() ?? pp.AsValueString()) : "") ?? "";
                    string num = (np != null ? (np.AsString() ?? np.AsValueString()) : "") ?? "";

                    string key = pfx.Trim() + "|" + num.Trim();
                    if (!byKey.TryGetValue(key, out List<ElementId> ids))
                    {
                        ids = new List<ElementId>();
                        byKey[key] = ids;
                    }
                    ids.Add(fi.Id);
                }
            }
            catch (Exception ex)
            {
                GMSRevitAddin.GmsLog.Error("ExportParts.ResolveSkippedPieces", ex);
            }

            List<SkippedPiece> pieces = new List<SkippedPiece>();
            foreach (List<string> row in skippedRows)
            {
                string prefix = CellAt(row, iPrefix);
                string number = CellAt(row, iNumber);
                string key = prefix.Trim() + "|" + number.Trim();

                if (byKey.TryGetValue(key, out List<ElementId> matchIds) && matchIds.Count > 0)
                {
                    // One row per matching instance, each showing that instance's own Origin/Origin Sheet.
                    foreach (ElementId id in matchIds)
                    {
                        FamilyInstance fi = doc.GetElement(id) as FamilyInstance;
                        pieces.Add(new SkippedPiece
                        {
                            Prefix = prefix,
                            Number = number,
                            Origin = GMSRevitAddin.RevitParameterHelper.GetString(fi, "Origin") ?? "",
                            OriginSheet = GMSRevitAddin.RevitParameterHelper.GetString(fi, "Origin Sheet") ?? "",
                            Ids = { id }
                        });
                    }
                }
                else
                {
                    // No matching instance — keep the schedule-row data once; stays non-clickable.
                    pieces.Add(new SkippedPiece
                    {
                        Prefix = prefix,
                        Number = number,
                        Origin = CellAt(row, iOrigin),
                        OriginSheet = CellAt(row, iOriginSheet)
                    });
                }
            }
            return pieces;
        }

        private static string CellAt(List<string> row, int i)
        {
            return i >= 0 && i < row.Count && row[i] != null ? row[i] : "";
        }

        /// <summary>
        /// Turns a raw ODBC/ACE failure into a user-facing exception. Lock/in-use errors get a friendly
        /// "close the database and retry" message; anything else keeps the original detail. The original
        /// exception is always preserved as the inner exception for logging.
        /// </summary>
        private static Exception DescribeAccessException(OdbcException ex, string dbPath)
        {
            string raw = ex.Message ?? "";
            bool looksLocked =
                raw.IndexOf("lock", StringComparison.OrdinalIgnoreCase) >= 0 ||
                raw.IndexOf("in use", StringComparison.OrdinalIgnoreCase) >= 0 ||
                raw.IndexOf("could not use", StringComparison.OrdinalIgnoreCase) >= 0 ||
                raw.IndexOf("exclusively", StringComparison.OrdinalIgnoreCase) >= 0 ||
                raw.IndexOf("already opened", StringComparison.OrdinalIgnoreCase) >= 0;

            if (looksLocked)
            {
                return new Exception(
                    "The Parts Manager is currently in use and could not be updated." +
                    Environment.NewLine + Environment.NewLine +
                    "Close it (make sure no one has it open) and run Export Parts again." +
                    Environment.NewLine + Environment.NewLine + dbPath,
                    ex);
            }

            return new Exception(
                "Failed to write to the Parts Manager:" + Environment.NewLine + raw, ex);
        }

        #endregion



        // ====================================================================================================
        //  REGION — RESOLVE PARTS MANAGER DB PATH (from the "Parts Manager Location" GlobalParameter)
        // ====================================================================================================
        #region PARTS_MANAGER_DB_PATH

        /// <summary>
        /// Reads the per-project Access database path from the "Parts Manager Location" Revit
        /// GlobalParameter (set on the GMS Settings form → "Project Parts Manager Location").
        /// Returns null/empty when the parameter is missing or unset ("Not assigned").
        /// </summary>
        internal static string GetPartsManagerDbPath(Document document)
        {
            if (!GlobalParametersManager.AreGlobalParametersAllowed(document))
                return null;

            ElementId gpId = GlobalParametersManager.FindByName(document, "Parts Manager Location");
            if (gpId == null || gpId == ElementId.InvalidElementId)
                return null;

            GlobalParameter gp = document.GetElement(gpId) as GlobalParameter;
            StringParameterValue spv = gp?.GetValue() as StringParameterValue;
            string value = spv?.Value;

            if (string.IsNullOrWhiteSpace(value) ||
                value.Equals("Not assigned", StringComparison.OrdinalIgnoreCase))
                return null;

            return value.Trim();
        }

        #endregion



        // ====================================================================================================
        //  REGION — ACCESS LOCK FILE
        // ====================================================================================================
        #region LOCK_FILE

        /// <summary>
        /// Reads the Access lock file (.laccdb) next to <paramref name="dbPath"/> and returns a list of
        /// "MACHINE\User" strings for every open connection recorded there. Returns an empty list when the
        /// lock file is absent (no one has the DB open) or cannot be read.
        /// </summary>
        /// <remarks>
        /// The .laccdb format uses 64-byte fixed-width records: the first 32 bytes are the null-terminated
        /// ASCII machine name, the next 32 bytes are the null-terminated ASCII user/security name.
        /// </remarks>
        internal static List<string> ReadLockFileHolders(string dbPath)
        {
            var holders = new List<string>();
            string lockPath = Path.ChangeExtension(dbPath, ".laccdb");
            if (!File.Exists(lockPath))
                return holders;
            try
            {
                // ACE holds a lock on the .laccdb file while the DB is open, so FileShare.ReadWrite
                // is required — File.ReadAllBytes (FileShare.Read) will throw access denied.
                byte[] data;
                using (FileStream fs = new FileStream(lockPath, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                {
                    data = new byte[fs.Length];
                    fs.Read(data, 0, data.Length);
                }
                for (int offset = 0; offset + 64 <= data.Length; offset += 64)
                {
                    string machine = ReadNullTerminatedAscii(data, offset, 32).Trim();
                    string user    = ReadNullTerminatedAscii(data, offset + 32, 32).Trim();
                    if (string.IsNullOrEmpty(machine) && string.IsNullOrEmpty(user))
                        continue;
                    string entry = !string.IsNullOrEmpty(machine) && !string.IsNullOrEmpty(user)
                        ? machine + @"\" + user
                        : machine + user;
                    if (!string.IsNullOrEmpty(entry))
                        holders.Add(entry);
                }
            }
            catch (Exception ex)
            {
                GMSRevitAddin.GmsLog.Error("ExportParts.ReadLockFile", ex);
            }
            return holders;
        }

        private static string ReadNullTerminatedAscii(byte[] data, int offset, int maxLen)
        {
            int end = offset;
            while (end < offset + maxLen && end < data.Length && data[end] != 0)
                end++;
            return System.Text.Encoding.ASCII.GetString(data, offset, end - offset);
        }

        #endregion
    }

    // ====================================================================================================
    //  EXPORT PIECES COMMAND — direct "update all tags, then export" (no set-selection form)
    // ----------------------------------------------------------------------------------------------------
    //  Wired to the "Export Pieces" ribbon button (GMS_tools.cs). Replaces the old
    //  BySetForm.LaunchEPForm flow: since Export reads the whole piece schedule and writes a full
    //  snapshot to Access, the per-set picker added nothing to the export — it only scoped the tag
    //  update. This command updates ALL tag origins doc-wide, then runs the export.
    //
    //  Pre-flight: before starting the (potentially long) doc-wide tag update we check whether the
    //  target Access database is already open. If it is, the user is shown who has it open (read from
    //  the .laccdb lock file) and asked whether to proceed or cancel — avoiding a wasted wait only to
    //  have the write fail at the end.
    // ====================================================================================================
    [Transaction(TransactionMode.Manual)]
    public class ExportPieces : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;

            // ========================================================================================
            //  PRE-FLIGHT — check Access DB availability before the tag update
            // ========================================================================================
            string dbPath = Export.GetPartsManagerDbPath(doc);
            if (!string.IsNullOrWhiteSpace(dbPath) && File.Exists(dbPath))
            {
                // .laccdb existence is the primary signal — ACE creates it the moment the DB is opened
                // and removes it when the last connection closes. Parse it for user names (best-effort).
                string lockPath = Path.ChangeExtension(dbPath, ".laccdb");
                bool lockFileExists = File.Exists(lockPath);
                List<string> holders = lockFileExists ? Export.ReadLockFileHolders(dbPath) : new List<string>();

                // Exclusive=1 makes the test connection fail when anyone else already has the DB open
                // (the default shared open succeeds even with Access open, so it can't detect this).
                // If the exclusive open succeeds the DB was free — close it immediately via using +
                // ReleaseObjectPool before ManualUpdate runs.
                bool cannotOpen = false;
                try
                {
                    string connStr = @"Driver={Microsoft Access Driver (*.mdb, *.accdb)};Dbq=" + dbPath + ";Exclusive=1;";
                    using (OdbcConnection cn = new OdbcConnection(connStr))
                        cn.Open();
                }
                catch { cannotOpen = true; }
                finally
                {
                    OdbcConnection.ReleaseObjectPool();
                }

                if (lockFileExists || cannotOpen)
                {
                    string holderLines = holders.Any()
                        ? string.Join(Environment.NewLine, holders.Select(h => "  " + h))
                        : "  (user information unavailable)";

                    string content =
                        "Open connections:" + Environment.NewLine + holderLines +
                        Environment.NewLine + Environment.NewLine +
                        (cannotOpen
                            ? "The Parts Manager cannot be opened for writing right now. Close it and retry."
                            : "Close it before proceeding to ensure the export writes successfully.") +
                        Environment.NewLine + Environment.NewLine +
                        dbPath;

                    TaskDialog dlg = new TaskDialog("Parts Manager In Use");
                    dlg.MainInstruction = "The Parts Manager is currently open.";
                    dlg.MainContent = content;
                    dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink1, "Cancel");
                    dlg.AddCommandLink(TaskDialogCommandLinkId.CommandLink2, "Proceed anyway");
                    if (dlg.Show() == TaskDialogResult.CommandLink1)
                        return Result.Cancelled;
                }
            }

            // 1. Update all tag origins doc-wide (shows its own progress + dialogs).
            Result tagResult = new ManualUpdateOrigins.ManualUpdate()
                .Execute(commandData, ref message, elements);

            // Abort if tags are locked by other users (ManualUpdate already explained why).
            if (tagResult == Result.Cancelled)
                return Result.Cancelled;

            // 2. Export the full piece schedule to the per-project Access DB.
            return new Export().Execute(commandData, ref message, elements);
        }
    }
}
