using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Autodesk.Revit.DB;

namespace StockLengthOptimizer
{
    // ----------------------------------------------------------------------------------
    // Reads the "(DO NOT OPEN) Framing Stock Lengths" schedule and turns it into the same
    // Finish+Die grouped structure the index.html ingest() built from a CSV. Column order is
    // auto-detected from the header row by keyword (matching the JS), so the schedule's exact
    // column layout doesn't matter as long as the headings are recognizable.
    // ----------------------------------------------------------------------------------
    public static class CutListReader
    {
        public const string ScheduleName = "(DO NOT OPEN) Framing Stock Lengths";

        /// <summary>Result of reading the schedule: the grouped dies (display order) plus counts.</summary>
        public class CutList
        {
            public List<DieGroup> Dies = new List<DieGroup>();
            public int Skipped;
            public int FinishCount;
        }

        /// <summary>Find the schedule by name. Returns null if not present.</summary>
        public static ViewSchedule FindSchedule(Document doc)
        {
            return new FilteredElementCollector(doc)
                .OfClass(typeof(ViewSchedule))
                .Cast<ViewSchedule>()
                .FirstOrDefault(vs => vs.Name.Equals(ScheduleName, StringComparison.Ordinal));
        }

        /// <summary>Read the schedule body grid into raw rows (cell text, left-to-right).</summary>
        private static List<List<string>> ReadRows(ViewSchedule schedule)
        {
            var result = new List<List<string>>();
            TableData table = schedule.GetTableData();
            TableSectionData body = table.GetSectionData(SectionType.Body);
            int rowCount = body.NumberOfRows;
            int colCount = body.NumberOfColumns;

            for (int r = 0; r < rowCount; r++)
            {
                var cells = new List<string>(colCount);
                for (int c = 0; c < colCount; c++)
                {
                    string text = schedule.GetCellText(SectionType.Body, r, c);
                    cells.Add(text == null ? "" : text.Trim());
                }
                result.Add(cells);
            }
            return result;
        }

        /// <summary>
        /// Build the grouped cut list from the schedule. Ports the JS ingest(): locate the header
        /// row, map columns by keyword, then accumulate Finish+Die groups. Throws on no usable data.
        /// </summary>
        public static CutList Read(ViewSchedule schedule)
        {
            var rows = ReadRows(schedule).Where(r => r.Any(c => c.Trim() != "")).ToList();
            if (rows.Count == 0) throw new InvalidOperationException("The schedule is empty.");

            // find header row (one of the first few rows that contains 'die' or 'mark')
            int hIdx = -1;
            for (int i = 0; i < Math.Min(rows.Count, 6); i++)
                if (rows[i].Any(c => Regex.IsMatch(c, "die|mark", RegexOptions.IgnoreCase))) { hIdx = i; break; }

            int dieCol = 0, countCol = 1, lenCol = 2, wpfCol = -1, finishCol = -1, descCol = -1;
            if (hIdx >= 0)
            {
                // Order matters: columns may share a prefix (e.g. "Framing - Die Number",
                // "Framing - Weight / Ft", "Framing - Finish"), so match the most specific
                // keyword first and never key the finish column off "framing".
                var header = rows[hIdx];
                for (int j = 0; j < header.Count; j++)
                {
                    string c = header[j];
                    if (Regex.IsMatch(c, "die|mark", RegexOptions.IgnoreCase)) dieCol = j;
                    else if (Regex.IsMatch(c, "desc", RegexOptions.IgnoreCase)) descCol = j;
                    else if (Regex.IsMatch(c, "weight", RegexOptions.IgnoreCase)) wpfCol = j;
                    else if (Regex.IsMatch(c, "finish", RegexOptions.IgnoreCase)) finishCol = j;
                    else if (Regex.IsMatch(c, "count|qty|quantit", RegexOptions.IgnoreCase)) countCol = j;
                    else if (Regex.IsMatch(c, @"length|len\b|overall", RegexOptions.IgnoreCase)) lenCol = j;
                }
            }

            int start = hIdx >= 0 ? hIdx + 1 : 0;
            var map = new Dictionary<string, DieGroup>();
            int skipped = 0;

            for (int i = start; i < rows.Count; i++)
            {
                var r = rows[i];
                string die = Cell(r, dieCol).Trim();
                int cnt = ParseIntLoose(Cell(r, countCol));
                double len = StockOptimizer.ParseLen(Cell(r, lenCol));
                if (die == "" || cnt <= 0 || double.IsNaN(len) || len <= 0) { skipped++; continue; }

                string finish = (finishCol >= 0 ? Cell(r, finishCol).Trim() : "");
                if (finish == "") finish = "(no finish)";
                double wpf = wpfCol >= 0 ? ParseDoubleLoose(Cell(r, wpfCol)) : double.NaN;
                string desc = descCol >= 0 ? Cell(r, descCol).Trim() : "";

                string key = finish + "" + die;
                if (!map.TryGetValue(key, out DieGroup g))
                {
                    g = new DieGroup { Key = key, Finish = finish, Die = die, Desc = desc, Wpf = double.IsNaN(wpf) ? 0 : wpf };
                    map[key] = g;
                }
                else
                {
                    if (g.Wpf == 0 && !double.IsNaN(wpf)) g.Wpf = wpf;
                    if (g.Desc == "" && desc != "") g.Desc = desc;
                }
                g.Lens[len] = (g.Lens.TryGetValue(len, out int q) ? q : 0) + cnt;
            }

            if (map.Count == 0)
                throw new InvalidOperationException("No valid data rows found. Check that the schedule has Die/Mark, Count, and Length columns.");

            // display/optimization order: group by Finish, then by Die (natural numeric order)
            var dies = map.Values
                .OrderBy(g => g.Finish, NaturalComparer.Instance)
                .ThenBy(g => g.Die, NaturalComparer.Instance)
                .ToList();

            return new CutList
            {
                Dies = dies,
                Skipped = skipped,
                FinishCount = dies.Select(g => g.Finish).Distinct().Count()
            };
        }

        private static string Cell(List<string> row, int i) => (i >= 0 && i < row.Count) ? (row[i] ?? "") : "";

        private static int ParseIntLoose(string s)
        {
            string digits = Regex.Replace(s ?? "", "[^0-9-]", "");
            return int.TryParse(digits, out int v) ? v : 0;
        }

        private static double ParseDoubleLoose(string s)
        {
            string cleaned = Regex.Replace(s ?? "", @"[^0-9.\-]", "");
            return double.TryParse(cleaned, System.Globalization.NumberStyles.Any,
                System.Globalization.CultureInfo.InvariantCulture, out double v) ? v : double.NaN;
        }

        /// <summary>Natural (numeric-aware) string compare, approximating the JS localeCompare(numeric:true).</summary>
        private class NaturalComparer : IComparer<string>
        {
            public static readonly NaturalComparer Instance = new NaturalComparer();
            public int Compare(string a, string b)
            {
                a = a ?? ""; b = b ?? "";
                int ia = 0, ib = 0;
                while (ia < a.Length && ib < b.Length)
                {
                    if (char.IsDigit(a[ia]) && char.IsDigit(b[ib]))
                    {
                        int sa = ia, sb = ib;
                        while (ia < a.Length && char.IsDigit(a[ia])) ia++;
                        while (ib < b.Length && char.IsDigit(b[ib])) ib++;
                        string na = a.Substring(sa, ia - sa).TrimStart('0');
                        string nb = b.Substring(sb, ib - sb).TrimStart('0');
                        if (na.Length != nb.Length) return na.Length - nb.Length;
                        int cmp = string.CompareOrdinal(na, nb);
                        if (cmp != 0) return cmp;
                    }
                    else
                    {
                        int cmp = char.ToUpperInvariant(a[ia]).CompareTo(char.ToUpperInvariant(b[ib]));
                        if (cmp != 0) return cmp;
                        ia++; ib++;
                    }
                }
                return (a.Length - ia) - (b.Length - ib);
            }
        }
    }
}
