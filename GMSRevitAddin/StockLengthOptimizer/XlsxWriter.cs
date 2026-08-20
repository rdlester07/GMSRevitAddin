using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;

namespace StockLengthOptimizer
{
    // ----------------------------------------------------------------------------------
    // Minimal self-contained OOXML (.xlsx) writer ported from the index.html export. No
    // spreadsheet library — builds the parts as XML and packages them with ZipArchive.
    // Style indices match the ported STYLES_XML:
    //   0 default | 1 header | 2 num 0.00 | 3 num 0.0 | 4 int | 5 bold text | 6 bold 0.00 | 7 bold int | 8 title
    // ----------------------------------------------------------------------------------
    public static class XlsxWriter
    {
        public class Cell
        {
            public bool IsNumber;
            public string Text;     // for text cells
            public double Number;   // for number cells
            public int Style;
        }

        public class Sheet
        {
            public string Name;
            public string Title;                 // merged title row (row 1)
            public List<List<Cell>> Rows = new List<List<Cell>>();
        }

        // cell helpers (mirror the JS X.txt / X.num)
        public static Cell Txt(string v, int s = 0) => new Cell { IsNumber = false, Text = v ?? "", Style = s };
        public static Cell Num(double v, int s = 2) => new Cell { IsNumber = true, Number = v, Style = s };

        /// <summary>inches -> feet, 2 dp (ports ftN).</summary>
        public static double FeetN(double inch) => Math.Round(inch / 12 * 100) / 100;

        private const string StylesXml =
@"<?xml version=""1.0"" encoding=""UTF-8"" standalone=""yes""?>
<styleSheet xmlns=""http://schemas.openxmlformats.org/spreadsheetml/2006/main"">
<numFmts count=""1""><numFmt numFmtId=""164"" formatCode=""0.0""/></numFmts>
<fonts count=""4""><font><sz val=""11""/><name val=""Calibri""/></font><font><b/><sz val=""11""/><name val=""Calibri""/></font><font><b/><sz val=""11""/><color rgb=""FFFFFFFF""/><name val=""Calibri""/></font><font><b/><sz val=""13""/><name val=""Calibri""/></font></fonts>
<fills count=""3""><fill><patternFill patternType=""none""/></fill><fill><patternFill patternType=""gray125""/></fill><fill><patternFill patternType=""solid""><fgColor rgb=""FF2F5496""/><bgColor indexed=""64""/></patternFill></fill></fills>
<borders count=""2""><border><left/><right/><top/><bottom/><diagonal/></border><border><left/><right/><top/><bottom style=""thin""><color rgb=""FFBFBFBF""/></bottom><diagonal/></border></borders>
<cellStyleXfs count=""1""><xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0""/></cellStyleXfs>
<cellXfs count=""9"">
<xf numFmtId=""0"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0""/>
<xf numFmtId=""0"" fontId=""2"" fillId=""2"" borderId=""1"" xfId=""0"" applyFont=""1"" applyFill=""1"" applyBorder=""1"" applyAlignment=""1""><alignment horizontal=""center"" vertical=""center"" wrapText=""1""/></xf>
<xf numFmtId=""2"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0"" applyNumberFormat=""1""/>
<xf numFmtId=""164"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0"" applyNumberFormat=""1""/>
<xf numFmtId=""1"" fontId=""0"" fillId=""0"" borderId=""0"" xfId=""0"" applyNumberFormat=""1""/>
<xf numFmtId=""0"" fontId=""1"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1""/>
<xf numFmtId=""2"" fontId=""1"" fillId=""0"" borderId=""0"" xfId=""0"" applyNumberFormat=""1"" applyFont=""1""/>
<xf numFmtId=""1"" fontId=""1"" fillId=""0"" borderId=""0"" xfId=""0"" applyNumberFormat=""1"" applyFont=""1""/>
<xf numFmtId=""0"" fontId=""3"" fillId=""0"" borderId=""0"" xfId=""0"" applyFont=""1""/>
</cellXfs>
<cellStyles count=""1""><cellStyle name=""Normal"" xfId=""0"" builtinId=""0""/></cellStyles>
</styleSheet>";

        private static string XmlEsc(string s) =>
            (s ?? "").Replace("&", "&amp;").Replace("<", "&lt;").Replace(">", "&gt;");

        private static string ColName(int i)
        {
            string s = ""; i++;
            while (i > 0) { int m = (i - 1) % 26; s = (char)(65 + m) + s; i = (i - 1) / 26; }
            return s;
        }

        // auto-fit: width per column from the widest cell (header + data rows; the merged title row
        // is excluded so it can't stretch column A). Ports autoWidths.
        private static List<double> AutoWidths(List<List<Cell>> rows)
        {
            var w = new Dictionary<int, int>();
            foreach (var row in rows)
            {
                for (int ci = 0; ci < row.Count; ci++)
                {
                    var cell = row[ci];
                    if (cell == null) continue;
                    string v = cell.IsNumber ? cell.Number.ToString(CultureInfo.InvariantCulture) : (cell.Text ?? "");
                    if (cell.IsNumber && (cell.Style == 2 || cell.Style == 6) && v.IndexOf('.') < 0) v += ".00";
                    if (!w.TryGetValue(ci, out int cur) || cur < v.Length) w[ci] = v.Length;
                }
            }
            int max = w.Count == 0 ? 0 : w.Keys.Max() + 1;
            var outv = new List<double>();
            for (int i = 0; i < max; i++)
            {
                int len = w.TryGetValue(i, out int x) ? x : 8;
                outv.Add(Math.Max(8, Math.Min(60, len + 2)));
            }
            return outv;
        }

        private static string SheetXml(Sheet sheet)
        {
            var widths = AutoWidths(sheet.Rows);
            int ncols = Math.Max(1, widths.Count);
            var cols = new StringBuilder();
            for (int i = 0; i < widths.Count; i++)
                cols.Append($"<col min=\"{i + 1}\" max=\"{i + 1}\" width=\"{widths[i].ToString(CultureInfo.InvariantCulture)}\" customWidth=\"1\" bestFit=\"1\"/>");

            bool hasTitle = !string.IsNullOrEmpty(sheet.Title);
            int off = hasTitle ? 1 : 0;
            var rowsXml = new StringBuilder();
            if (hasTitle)
                rowsXml.Append($"<row r=\"1\"><c r=\"A1\" s=\"8\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{XmlEsc(sheet.Title)}</t></is></c></row>");

            for (int ri = 0; ri < sheet.Rows.Count; ri++)
            {
                var row = sheet.Rows[ri];
                int rr = ri + 1 + off;
                var cells = new StringBuilder();
                for (int ci = 0; ci < row.Count; ci++)
                {
                    var cell = row[ci];
                    string refr = ColName(ci) + rr;
                    int s = cell != null ? cell.Style : 0;
                    if (cell != null && cell.IsNumber)
                        cells.Append($"<c r=\"{refr}\" s=\"{s}\"><v>{cell.Number.ToString(CultureInfo.InvariantCulture)}</v></c>");
                    else
                        cells.Append($"<c r=\"{refr}\" s=\"{s}\" t=\"inlineStr\"><is><t xml:space=\"preserve\">{XmlEsc(cell?.Text)}</t></is></c>");
                }
                rowsXml.Append($"<row r=\"{rr}\">{cells}</row>");
            }

            string merge = hasTitle ? $"<mergeCells count=\"1\"><mergeCell ref=\"A1:{ColName(ncols - 1)}1\"/></mergeCells>" : "";
            int ySplit = hasTitle ? 2 : 1;
            string topLeft = hasTitle ? "A3" : "A2";
            return "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>" +
                "<worksheet xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\">" +
                $"<sheetViews><sheetView workbookViewId=\"0\"><pane ySplit=\"{ySplit}\" topLeftCell=\"{topLeft}\" activePane=\"bottomLeft\" state=\"frozen\"/><selection pane=\"bottomLeft\"/></sheetView></sheetViews>" +
                $"<cols>{cols}</cols><sheetData>{rowsXml}</sheetData>{merge}</worksheet>";
        }

        /// <summary>Write the workbook to <paramref name="path"/> (ports writeXlsx).</summary>
        public static void Write(List<Sheet> sheets, string path)
        {
            var parts = new Dictionary<string, string>();

            string ov = string.Concat(sheets.Select((s, i) =>
                $"<Override PartName=\"/xl/worksheets/sheet{i + 1}.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml\"/>"));
            parts["[Content_Types].xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<Types xmlns=\"http://schemas.openxmlformats.org/package/2006/content-types\"><Default Extension=\"rels\" ContentType=\"application/vnd.openxmlformats-package.relationships+xml\"/><Default Extension=\"xml\" ContentType=\"application/xml\"/><Override PartName=\"/xl/workbook.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml\"/><Override PartName=\"/xl/styles.xml\" ContentType=\"application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml\"/>" + ov + "</Types>";

            parts["_rels/.rels"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\"><Relationship Id=\"rId1\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument\" Target=\"xl/workbook.xml\"/></Relationships>";

            string sh = string.Concat(sheets.Select((s, i) =>
                $"<sheet name=\"{XmlEsc(s.Name)}\" sheetId=\"{i + 1}\" r:id=\"rId{i + 1}\"/>"));
            parts["xl/workbook.xml"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<workbook xmlns=\"http://schemas.openxmlformats.org/spreadsheetml/2006/main\" xmlns:r=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships\"><sheets>" + sh + "</sheets></workbook>";

            string rels = string.Concat(sheets.Select((s, i) =>
                $"<Relationship Id=\"rId{i + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet\" Target=\"worksheets/sheet{i + 1}.xml\"/>"));
            rels += $"<Relationship Id=\"rId{sheets.Count + 1}\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/styles\" Target=\"styles.xml\"/>";
            parts["xl/_rels/workbook.xml.rels"] =
                "<?xml version=\"1.0\" encoding=\"UTF-8\" standalone=\"yes\"?>\n<Relationships xmlns=\"http://schemas.openxmlformats.org/package/2006/relationships\">" + rels + "</Relationships>";

            parts["xl/styles.xml"] = StylesXml;
            for (int i = 0; i < sheets.Count; i++)
                parts["xl/worksheets/sheet" + (i + 1) + ".xml"] = SheetXml(sheets[i]);

            using (var fs = new FileStream(path, FileMode.Create, FileAccess.Write))
            using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
            {
                foreach (var kv in parts)
                {
                    var entry = zip.CreateEntry(kv.Key, CompressionLevel.Optimal);
                    using (var w = new StreamWriter(entry.Open(), new UTF8Encoding(false)))
                        w.Write(kv.Value);
                }
            }
        }
    }
}
