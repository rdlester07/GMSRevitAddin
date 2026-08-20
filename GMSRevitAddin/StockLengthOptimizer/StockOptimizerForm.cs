using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Printing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
// NB: do NOT `using Autodesk.Revit.UI;` here — it defines its own TextBox/ComboBox that collide
// with WinForms. UIThemeManager/UITheme are referenced fully-qualified in Theme.Current().

namespace StockLengthOptimizer
{
    // ----------------------------------------------------------------------------------
    // WinForms UI for the Stock Length Optimizer — the native equivalent of the index.html
    // Settings + Results + Export cards. Pure in-memory: it receives the grouped dies (read
    // from the schedule by the command) and never touches the Revit API, so it can be modal.
    // Flat, modern styling in the shared UI font; palette follows Revit's current light/dark UI theme.
    // ----------------------------------------------------------------------------------
    public class StockOptimizerForm : Form
    {
        private readonly List<DieGroup> _dies;
        private List<OptimizeResult> _results;
        private readonly Theme _t;

        private TextBox _kerf, _trim, _from, _to;
        private ComboBox _step;
        private FlowLayoutPanel _statsPanel;
        private string _summaryText = "";
        private DataGridView _dieGrid;
        private DataGridView _patGrid;
        private Button _exportBtn, _printBtn, _optimizeBtn;

        private static readonly string FontName = GMSRevitAddin.DarkTheme.UiFontName;

        public StockOptimizerForm(List<DieGroup> dies, int finishCount, int skipped)
        {
            _dies = dies ?? new List<DieGroup>();
            _t = Theme.Current();

            Text = "GMS — Stock Length Optimizer";
            Width = 960;
            Height = 700;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(780, 540);
            Font = new Font(FontName, 9f);
            BackColor = _t.FormBg;
            ForeColor = _t.Text;

            BuildUi(finishCount, skipped);
        }

        // ============================ theme ============================

        private class Theme
        {
            public Color FormBg, PanelBg, CardBg, Line, Text, Muted, Accent, AccentHover, AccentText;
            public Color GridHeaderBg, GridHeaderText, GridBg, GridAltRow, GridSelBg, GridSelText, GridLine, GroupBg;
            public Color Good, Warn, Bad;
            public bool Dark;

            public static Theme Current()
            {
                bool dark = false;
                try { dark = Autodesk.Revit.UI.UIThemeManager.CurrentTheme == Autodesk.Revit.UI.UITheme.Dark; }
                catch { /* older/headless — default light */ }

                // Neutral gray scale (matches DarkTheme.Palette): 50 f9fafb … 950 030912. No blue
                // accent — the header bar and grid selection sit on mid-grays. Good/Warn/Bad stay their
                // semantic green/amber/red since they encode pass/warn/fail, not surface color.
                return dark ? new Theme
                {
                    Dark = true,
                    FormBg = C(0x111b27), PanelBg = C(0x1f2c37), CardBg = C(0x374451), Line = C(0x4b5863),
                    Text = C(0xf3f4f6), Muted = C(0x9ca5af), Accent = C(0x6f7985), AccentHover = C(0x9ca5af), AccentText = C(0xf9fafb),
                    GridHeaderBg = C(0x374451), GridHeaderText = C(0xe5e8eb), GridBg = C(0x111b27), GridAltRow = C(0x1f2c37),
                    GridSelBg = C(0x6f7985), GridSelText = C(0xf9fafb), GridLine = C(0x4b5863), GroupBg = C(0x1f2c37),
                    Good = C(0x4FC36B), Warn = C(0xE0A93C), Bad = C(0xF1746B)
                } : new Theme
                {
                    Dark = false,
                    FormBg = C(0xf9fafb), PanelBg = C(0xf9fafb), CardBg = C(0xf3f4f6), Line = C(0xd1d6db),
                    Text = C(0x111b27), Muted = C(0x6f7985), Accent = C(0x4b5863), AccentHover = C(0x6f7985), AccentText = C(0xf9fafb),
                    GridHeaderBg = C(0xf3f4f6), GridHeaderText = C(0x374451), GridBg = C(0xf9fafb), GridAltRow = C(0xf3f4f6),
                    GridSelBg = C(0xd1d6db), GridSelText = C(0x111b27), GridLine = C(0xe5e8eb), GroupBg = C(0xf3f4f6),
                    Good = C(0x2E9E4F), Warn = C(0xB8860B), Bad = C(0xC0392B)
                };
            }

            private static Color C(int rgb) => Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }

        // ── Rounded-corner helpers (this form is self-themed, so it can't use DarkTheme's) ──
        private const int ButtonCornerRadius = 6;
        private const int CardCornerRadius = 10;

        private static System.Drawing.Drawing2D.GraphicsPath RoundedPath(Rectangle r, int radius)
        {
            int d = Math.Max(1, radius * 2);
            d = Math.Min(d, Math.Min(r.Width, r.Height));
            var path = new System.Drawing.Drawing2D.GraphicsPath();
            if (d <= 1) { path.AddRectangle(r); path.CloseFigure(); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void RoundControl(Control ctrl, int radius)
        {
            void SetRegion()
            {
                if (ctrl.Width <= 0 || ctrl.Height <= 0) return;
                ctrl.Region = new Region(RoundedPath(new Rectangle(0, 0, ctrl.Width, ctrl.Height), radius));
            }
            ctrl.Resize += (s, e) => SetRegion();
            SetRegion();
        }

        // ============================ UI ============================

        private void BuildUi(int finishCount, int skipped)
        {
            var bottomBar = BuildToolbar();      // dock bottom first so it sits below everything

            // ----- header bar (accent) -----
            // Sized from measured text height (not a fixed pixel guess) so the title+subtitle never
            // overlap or get clipped at larger DPI/font scaling — a hard-coded Height/Location pair
            // silently overlapped the settings row once the actual rendered font was taller than assumed.
            var titleFont = new Font(FontName, 14f, FontStyle.Bold);
            var subtitleFont = new Font(FontName, 8.5f);
            var titleLabel = new Label
            {
                Text = "Stock Length Optimizer",
                AutoSize = true,
                ForeColor = _t.AccentText,
                Font = titleFont,
                Location = new Point(16, 8)
            };
            Size titleSize = TextRenderer.MeasureText(titleLabel.Text, titleFont);
            var subtitleLabel = new Label
            {
                Text = "Cutting-stock optimization for extruded aluminum — by die / mark number",
                AutoSize = true,
                ForeColor = Color.FromArgb(220, _t.AccentText),
                Font = subtitleFont,
                Location = new Point(18, 8 + titleSize.Height + 2)
            };
            Size subtitleSize = TextRenderer.MeasureText(subtitleLabel.Text, subtitleFont);

            var header = new Panel
            {
                Dock = DockStyle.Top,
                Height = 8 + titleSize.Height + 2 + subtitleSize.Height + 8,
                BackColor = _t.Accent
            };
            header.Controls.Add(titleLabel);
            header.Controls.Add(subtitleLabel);

            // ----- settings bar -----
            var settings = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                BackColor = _t.PanelBg,
                Padding = new Padding(12, 12, 12, 10),
                WrapContents = true
            };
            _kerf = AddField(settings, "Saw kerf (in)", "0.188");
            _trim = AddField(settings, "End trim / bar (in)", "3");
            _from = AddField(settings, "Search from", "12'");
            _to = AddField(settings, "Search to", "24'");

            var stepPanel = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(6, 0, 6, 0) };
            stepPanel.Controls.Add(MutedLabel("Step"));
            _step = new ComboBox { DropDownStyle = ComboBoxStyle.DropDownList, Width = 84, FlatStyle = FlatStyle.Flat, Font = new Font(FontName, 9f), BackColor = _t.PanelBg, ForeColor = _t.Text };
            _step.Items.AddRange(new object[] { "1\"", "2\"", "4\"", "6\"" });
            _step.SelectedIndex = 1; // 2"
            stepPanel.Controls.Add(_step);
            settings.Controls.Add(stepPanel);

            _optimizeBtn = AccentButton("Optimize  ▶");
            _optimizeBtn.Margin = new Padding(16, 18, 6, 0);
            _optimizeBtn.Click += (s, e) => RunOptimize();
            settings.Controls.Add(_optimizeBtn);

            // ----- loaded info -----
            var loaded = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 24,
                BackColor = _t.PanelBg,
                ForeColor = _t.Muted,
                Padding = new Padding(14, 4, 12, 2),
                Font = new Font(FontName, 8.5f),
                Text = "Loaded " + _dies.Count + " die" + (_dies.Count != 1 ? "s" : "") +
                       " across " + finishCount + " finish" + (finishCount != 1 ? "es" : "") +
                       (skipped > 0 ? "   ·   skipped " + skipped + " blank/zero rows" : "") +
                       ".   Set options and click Optimize."
            };

            // ----- stat cards (summary) -----
            _statsPanel = new FlowLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                BackColor = _t.FormBg,
                Padding = new Padding(10, 8, 10, 8),
                WrapContents = true
            };

            // ----- results split -----
            var split = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 330,
                BackColor = _t.FormBg
            };
            split.Panel1.BackColor = _t.FormBg;
            split.Panel2.BackColor = _t.FormBg;

            _dieGrid = MakeGrid();
            // Finish is shown as a group-header row (see RenderResults), not a per-row column.
            _dieGrid.Columns.Add(TextCol("Die", "Mark #", 90));
            _dieGrid.Columns.Add(TextCol("Desc", "Description", 180));
            _dieGrid.Columns.Add(TextCol("Stock", "Stock Length", 100));
            _dieGrid.Columns.Add(NumCol("Bars", "Bars"));
            // Widened beyond NumCol's default 60 — a 2-length die's cell holds a breakdown like
            // "12 @ 18'-0" + 8 @ 26'-0"", not just a single number (see RenderResults).
            _dieGrid.Columns["Bars"].MinimumWidth = 110;
            _dieGrid.Columns.Add(TextCol("Required", "Required", 90));
            _dieGrid.Columns.Add(TextCol("Purchased", "Purchased", 90));
            _dieGrid.Columns.Add(TextCol("Drop", "Drop", 80));
            _dieGrid.Columns.Add(TextCol("Yield", "Yield", 70));
            _dieGrid.Columns.Add(TextCol("Note", "Note", 200));
            _dieGrid.SelectionChanged += (s, e) => ShowPatternsForSelectedDie();
            split.Panel1.Controls.Add(_dieGrid);
            split.Panel1.Controls.Add(SectionHeader("Optimization results"));

            _patGrid = MakeGrid();
            _patGrid.Columns.Add(NumCol("PCount", "Bars"));
            // Shows each pattern's own stock length — relevant once a die uses 2 lengths (see
            // OptimizeResult.StockLengths); for single-length dies every row just repeats the same value.
            _patGrid.Columns.Add(TextCol("PStock", "Stock", 90));
            _patGrid.Columns.Add(TextCol("Pattern", "Cut pattern", 430));
            _patGrid.Columns.Add(NumCol("Cuts", "Cuts"));
            _patGrid.Columns.Add(TextCol("Used", "Used", 90));
            _patGrid.Columns.Add(TextCol("PDrop", "Drop", 80));
            split.Panel2.Controls.Add(_patGrid);
            split.Panel2.Controls.Add(SectionHeader("Cut patterns for selected die"));

            Controls.Add(split);
            Controls.Add(_statsPanel);
            Controls.Add(loaded);
            Controls.Add(settings);
            Controls.Add(header);
            Controls.Add(bottomBar);
        }

        private FlowLayoutPanel BuildToolbar()
        {
            var bar = new FlowLayoutPanel { Dock = DockStyle.Bottom, AutoSize = true, FlowDirection = FlowDirection.RightToLeft, BackColor = _t.PanelBg, Padding = new Padding(10) };
            _printBtn = SecondaryButton("🖨  Print");
            _printBtn.Enabled = false;
            _printBtn.Click += (s, e) => PrintResults();
            _exportBtn = AccentButton("⬇  Export to Excel (.xlsx)");
            _exportBtn.Margin = new Padding(6);
            _exportBtn.Enabled = false;
            _exportBtn.Click += (s, e) => ExportExcel();
            bar.Controls.Add(_exportBtn);
            bar.Controls.Add(_printBtn);
            return bar;
        }

        private Label SectionHeader(string text) => new Label
        {
            Dock = DockStyle.Top,
            Height = 26,
            Text = text,
            ForeColor = _t.Accent,
            BackColor = _t.FormBg,
            Font = new Font(FontName, 9.5f, FontStyle.Bold),
            Padding = new Padding(4, 5, 0, 0)
        };

        // ============================ settings parsing ============================

        private OptimizerSettings ReadSettings()
        {
            return new OptimizerSettings
            {
                KerfIn = ParseDouble(_kerf.Text, 0),
                TrimIn = ParseDouble(_trim.Text, 0),
                RangeMinIn = StockOptimizer.ParseLen(_from.Text),
                RangeMaxIn = StockOptimizer.ParseLen(_to.Text),
                StepIn = StockOptimizer.ParseLen((string)_step.SelectedItem)
            };
        }

        private void RunOptimize()
        {
            var set = ReadSettings();
            if (double.IsNaN(set.RangeMinIn) || double.IsNaN(set.RangeMaxIn) || double.IsNaN(set.StepIn) ||
                set.StepIn <= 0 || set.RangeMaxIn < set.RangeMinIn)
            {
                GMSRevitAddin.GmsUi.ShowWarning("Check the search range (From / To / Step).", "Stock Length Optimizer");
                return;
            }

            _results = StockOptimizer.RunRecommend(_dies, set);
            RenderResults();
            _exportBtn.Enabled = _results.Any(r => r.Error == null);
            _printBtn.Enabled = _exportBtn.Enabled;
        }

        // ============================ rendering ============================

        private void RenderResults()
        {
            int totBars = 0; double totPurchased = 0, totReq = 0, totReqWt = 0, totPurWt = 0; bool anyWt = false;
            int dieCount = 0;
            foreach (var r in _results)
            {
                if (r.Error != null) continue;
                dieCount++;
                totBars += r.Bars; totPurchased += r.Purchased; totReq += r.TotalReq;
                if (r.Wpf > 0) { totReqWt += r.TotalReq / 12 * r.Wpf; totPurWt += r.Purchased / 12 * r.Wpf; anyWt = true; }
            }
            double yld = totPurchased != 0 ? totReq / totPurchased * 100 : 0;
            double drop = totPurchased - totReq;

            _statsPanel.SuspendLayout();
            _statsPanel.Controls.Clear();
            _statsPanel.Controls.Add(StatCard(totBars.ToString("N0"), "Stock bars needed", null));
            _statsPanel.Controls.Add(StatCard(dieCount.ToString(), "Die numbers", null));
            _statsPanel.Controls.Add(StatCard(StockOptimizer.FormatLen(totReq), "Required length", null));
            _statsPanel.Controls.Add(StatCard(StockOptimizer.FormatLen(totPurchased), "Purchased length", null));
            _statsPanel.Controls.Add(StatCard(StockOptimizer.FormatLen(drop), "Drop / waste", null));
            _statsPanel.Controls.Add(StatCard(yld.ToString("F1", CultureInfo.InvariantCulture) + "%", "Overall yield", YieldColor(yld)));
            if (anyWt)
            {
                _statsPanel.Controls.Add(StatCard(FmtWt(totPurWt), "Purchased weight", null));
                _statsPanel.Controls.Add(StatCard(FmtWt(totReqWt), "Required weight (net)", null));
            }
            _statsPanel.ResumeLayout();

            _summaryText = "Stock bars: " + totBars.ToString("N0") + " | Required: " + StockOptimizer.FormatLen(totReq) +
                " | Purchased: " + StockOptimizer.FormatLen(totPurchased) + " | Drop: " + StockOptimizer.FormatLen(drop) +
                " | Yield: " + yld.ToString("F1") + "%";

            _dieGrid.Rows.Clear();
            _dieGrid.ClearSelection();
            string curFinish = null;
            int firstDataRow = -1;
            foreach (var r in _results)
            {
                // group-header row whenever the Finish changes (results are already finish-ordered)
                if (r.Finish != curFinish)
                {
                    curFinish = r.Finish;
                    int gi = _dieGrid.Rows.Add(curFinish, "", "", "", "", "", "", "", "");
                    var gr = _dieGrid.Rows[gi];
                    gr.Tag = "GROUP";
                    gr.DefaultCellStyle.BackColor = _t.GroupBg;
                    gr.DefaultCellStyle.SelectionBackColor = _t.GroupBg;
                    gr.DefaultCellStyle.ForeColor = _t.Accent;
                    gr.DefaultCellStyle.SelectionForeColor = _t.Accent;
                    gr.DefaultCellStyle.Font = new Font(FontName, 9f, FontStyle.Bold);
                }

                int idx;
                if (r.Error != null)
                {
                    idx = _dieGrid.Rows.Add(r.Die, r.Desc ?? "", "—", "", "", "", "", "", r.Error);
                    _dieGrid.Rows[idx].DefaultCellStyle.ForeColor = _t.Bad;
                }
                else
                {
                    double yd = r.Purchased != 0 ? r.TotalReq / r.Purchased * 100 : 0;
                    // Same source (PerLengthBreakdown) drives both Stock and Bars text so the two
                    // columns stay in matching length order.
                    var lens = PerLengthBreakdown(r);
                    string stockText = lens.Count > 1
                        ? string.Join(" + ", lens.Select(l => StockOptimizer.FormatLen(l.StockIn)))
                        : StockOptimizer.FormatLen(r.StockIn);
                    // Two lengths -> per-length breakdown "12 @ 18'-0" + 8 @ 26'-0""; one length ->
                    // the plain total, so a purchase order knows how many of EACH length to buy.
                    object barsCell = lens.Count > 1
                        ? (object)string.Join(" + ", lens.Select(l => l.Bars + " @ " + StockOptimizer.FormatLen(l.StockIn)))
                        : r.Bars;
                    string note = r.RecommendedTwoLengths
                        ? "★ 2 lengths — saves " + StockOptimizer.FormatLen(r.DropSavingsVsSingle)
                        : (r.Recommended ? ("★ best of " + r.Analysis.Count) : "");
                    idx = _dieGrid.Rows.Add(
                        r.Die, r.Desc ?? "", stockText, barsCell,
                        StockOptimizer.FormatLen(r.TotalReq), StockOptimizer.FormatLen(r.Purchased),
                        StockOptimizer.FormatLen(r.Purchased - r.TotalReq),
                        yd.ToString("F1", CultureInfo.InvariantCulture) + "%",
                        note);
                    _dieGrid.Rows[idx].Cells["Yield"].Style.ForeColor = YieldColor(yd);
                }
                _dieGrid.Rows[idx].Tag = r;
                if (firstDataRow < 0) firstDataRow = idx;
            }
            if (firstDataRow >= 0)
            {
                _dieGrid.CurrentCell = _dieGrid.Rows[firstDataRow].Cells[0];
                _dieGrid.Rows[firstDataRow].Selected = true;
            }
            ShowPatternsForSelectedDie();
        }

        private void ShowPatternsForSelectedDie()
        {
            if (_patGrid == null) return;
            _patGrid.Rows.Clear();
            if (_dieGrid.CurrentRow == null) return;
            if (!(_dieGrid.CurrentRow.Tag is OptimizeResult r) || r.Error != null) return;
            foreach (var p in r.Patterns)
            {
                double sum = p.Cuts.Sum();
                string pieces = string.Join("   +   ", p.Cuts.Select(StockOptimizer.FormatLen));
                // Drop is relative to THIS pattern's own stock length, not the die's primary length —
                // matters once a die mixes 2 lengths (each pattern belongs to only one of them).
                _patGrid.Rows.Add(p.Count, StockOptimizer.FormatLen(p.StockIn), pieces, p.Cuts.Count,
                    StockOptimizer.FormatLen(sum), StockOptimizer.FormatLen(p.StockIn - sum));
            }
        }

        private Color YieldColor(double y) => y >= 85 ? _t.Good : y >= 70 ? _t.Warn : _t.Bad;

        // ============================ export ============================

        private void ExportExcel()
        {
            if (_results == null) return;
            using (var dlg = new SaveFileDialog
            {
                Filter = "Excel Workbook (*.xlsx)|*.xlsx",
                FileName = "stock-optimization-" + DateTime.Now.ToString("yyyy-MM-dd") + ".xlsx"
            })
            {
                if (dlg.ShowDialog(this) != DialogResult.OK) return;
                try
                {
                    XlsxWriter.Write(BuildWorkbook(), dlg.FileName);
                    GMSRevitAddin.GmsUi.Show("Saved:\n" + dlg.FileName, "Stock Length Optimizer");
                }
                catch (Exception ex)
                {
                    GMSRevitAddin.GmsUi.ShowError(ex, "Excel Export Error");
                }
            }
        }

        // Groups a result's patterns by their own stock length (1 group for a single-length die, 2 for
        // a two-length die) so the Excel Summary sheet can emit one row per length actually purchased.
        private static List<(double StockIn, int Bars, double Used, double Purchased)> PerLengthBreakdown(OptimizeResult r)
        {
            return r.Patterns
                .GroupBy(p => p.StockIn)
                .Select(g => (
                    StockIn: g.Key,
                    Bars: g.Sum(p => p.Count),
                    Used: g.Sum(p => p.Cuts.Sum() * p.Count),
                    Purchased: g.Sum(p => p.Count) * g.Key))
                .OrderByDescending(x => x.StockIn)
                .ToList();
        }

        // Ports exportXlsx — Summary / Cut List / Stock Length Comparison / Requirements sheets.
        private List<XlsxWriter.Sheet> BuildWorkbook()
        {
            Func<string[], List<XlsxWriter.Cell>> H = hdr => hdr.Select(h => XlsxWriter.Txt(h, 1)).ToList();
            Func<double, double, double> wt = (inch, wpf) => wpf > 0 ? Math.Round(inch / 12 * wpf * 10) / 10 : 0;
            Func<double, double> ftN = XlsxWriter.FeetN;

            var sheets = new List<XlsxWriter.Sheet>();

            // ---- Summary ----
            // One row PER STOCK LENGTH actually used, so a 2-length die gets 2 rows (bars/used/drop
            // computed from just that length's patterns) — keeps the sheet flat/filterable instead of
            // cramming two lengths into one cell.
            var sum = new XlsxWriter.Sheet { Name = "Summary" };
            sum.Rows.Add(H(new[] { "Finish", "Mark #", "Description", "Stock Length", "Bars Needed", "Required (ft)", "Purchased (ft)", "Drop (ft)", "Yield %", "Wt/ft (lb)", "Purchased Wt (lb)", "Required Wt (lb)", "2 Lengths" }));
            int tBars = 0; double tReq = 0, tPur = 0, tPurWt = 0, tReqWt = 0;
            foreach (var r in _results)
            {
                if (r.Error != null)
                {
                    sum.Rows.Add(new List<XlsxWriter.Cell> { XlsxWriter.Txt(r.Finish), XlsxWriter.Txt(r.Die), XlsxWriter.Txt(r.Desc ?? ""), XlsxWriter.Txt("— " + r.Error), XlsxWriter.Txt(""), XlsxWriter.Txt(""), XlsxWriter.Txt(""), XlsxWriter.Txt(""), XlsxWriter.Txt(""), XlsxWriter.Txt(""), XlsxWriter.Txt(""), XlsxWriter.Txt(""), XlsxWriter.Txt("") });
                    continue;
                }
                var lens = PerLengthBreakdown(r);
                bool first = true;
                foreach (var L in lens)
                {
                    double yd = L.Purchased != 0 ? L.Used / L.Purchased * 100 : 0;
                    double pWt = wt(L.Purchased, r.Wpf), rWt = wt(L.Used, r.Wpf);
                    tBars += L.Bars; tReq += L.Used; tPur += L.Purchased; tPurWt += pWt; tReqWt += rWt;
                    sum.Rows.Add(new List<XlsxWriter.Cell>
                    {
                        XlsxWriter.Txt(first ? r.Finish : ""), XlsxWriter.Txt(first ? r.Die : ""), XlsxWriter.Txt(first ? (r.Desc ?? "") : ""),
                        XlsxWriter.Txt(StockOptimizer.FormatLen(L.StockIn)), XlsxWriter.Num(L.Bars, 4),
                        XlsxWriter.Num(ftN(L.Used)), XlsxWriter.Num(ftN(L.Purchased)), XlsxWriter.Num(ftN(L.Purchased - L.Used)),
                        XlsxWriter.Num(Math.Round(yd * 10) / 10, 3), XlsxWriter.Num(r.Wpf), XlsxWriter.Num(pWt, 3), XlsxWriter.Num(rWt, 3),
                        XlsxWriter.Txt(first && r.RecommendedTwoLengths ? ("Yes — saves " + StockOptimizer.FormatLen(r.DropSavingsVsSingle)) : "")
                    });
                    first = false;
                }
            }
            double tYld = tPur != 0 ? tReq / tPur * 100 : 0;
            sum.Rows.Add(new List<XlsxWriter.Cell>
            {
                XlsxWriter.Txt("TOTAL", 5), XlsxWriter.Txt("", 5), XlsxWriter.Txt("", 5), XlsxWriter.Txt("", 5), XlsxWriter.Num(tBars, 7),
                XlsxWriter.Num(ftN(tReq), 6), XlsxWriter.Num(ftN(tPur), 6), XlsxWriter.Num(ftN(tPur - tReq), 6), XlsxWriter.Num(Math.Round(tYld * 10) / 10, 3),
                XlsxWriter.Txt("", 5), XlsxWriter.Num(Math.Round(tPurWt * 10) / 10, 6), XlsxWriter.Num(Math.Round(tReqWt * 10) / 10, 6), XlsxWriter.Txt("", 5)
            });
            sheets.Add(sum);

            // ---- Cut List ----
            var cut = new XlsxWriter.Sheet { Name = "Cut List" };
            cut.Rows.Add(H(new[] { "Finish", "Mark #", "Description", "Stock Length", "Bar Qty", "Cuts / Bar", "Cut Pattern", "Used (ft)", "Drop (ft)" }));
            foreach (var r in _results)
            {
                if (r.Error != null) continue;
                foreach (var p in r.Patterns)
                {
                    // Each pattern's own StockIn (not r.StockIn) — a 2-length die's patterns can belong
                    // to either purchased length.
                    double used = p.Cuts.Sum();
                    cut.Rows.Add(new List<XlsxWriter.Cell>
                    {
                        XlsxWriter.Txt(r.Finish), XlsxWriter.Txt(r.Die), XlsxWriter.Txt(r.Desc ?? ""), XlsxWriter.Txt(StockOptimizer.FormatLen(p.StockIn)),
                        XlsxWriter.Num(p.Count, 4), XlsxWriter.Num(p.Cuts.Count, 4),
                        XlsxWriter.Txt(string.Join("  +  ", p.Cuts.Select(StockOptimizer.FormatLen))),
                        XlsxWriter.Num(ftN(used)), XlsxWriter.Num(ftN(p.StockIn - used))
                    });
                }
            }
            sheets.Add(cut);

            // ---- Stock Length Comparison ---- (single-length candidates only — see Summary's "2
            // Lengths" column for dies where a 2-length combo won instead of one of these rows)
            if (_results.Any(r => r.Analysis != null))
            {
                var cmp = new XlsxWriter.Sheet { Name = "Stock Length Comparison" };
                cmp.Rows.Add(H(new[] { "Finish", "Mark #", "Stock Length", "Bars", "Purchased (ft)", "Drop (ft)", "Yield %", "Best" }));
                foreach (var r in _results)
                {
                    if (r.Analysis == null) continue;
                    foreach (var a in r.Analysis)
                    {
                        // Suppress the star when 2 lengths won overall — none of these single-length
                        // rows is actually what was used, even if one happens to match r.StockIn.
                        bool win = !r.RecommendedTwoLengths && Math.Abs(a.StockIn - r.StockIn) < 1e-6;
                        cmp.Rows.Add(new List<XlsxWriter.Cell>
                        {
                            XlsxWriter.Txt(r.Finish), XlsxWriter.Txt(r.Die), XlsxWriter.Txt(StockOptimizer.FormatLen(a.StockIn)), XlsxWriter.Num(a.Bars, 4),
                            XlsxWriter.Num(ftN(a.Purchased)), XlsxWriter.Num(ftN(a.Drop)), XlsxWriter.Num(Math.Round(a.Yield * 10) / 10, 3), XlsxWriter.Txt(win ? "★" : "")
                        });
                    }
                }
                sheets.Add(cmp);
            }

            // ---- Requirements (input breakdown by length) ----
            var req = new XlsxWriter.Sheet { Name = "Requirements" };
            req.Rows.Add(H(new[] { "Finish", "Mark #", "Description", "Length", "Length (ft)", "Qty", "Wt/ft (lb)" }));
            foreach (var g in _dies)
            {
                foreach (var kv in g.Lens.OrderByDescending(e => e.Key))
                    req.Rows.Add(new List<XlsxWriter.Cell>
                    {
                        XlsxWriter.Txt(g.Finish), XlsxWriter.Txt(g.Die), XlsxWriter.Txt(g.Desc ?? ""), XlsxWriter.Txt(StockOptimizer.FormatLen(kv.Key)),
                        XlsxWriter.Num(ftN(kv.Key)), XlsxWriter.Num(kv.Value, 4), XlsxWriter.Num(g.Wpf)
                    });
            }
            sheets.Add(req);

            string exportedAt = DateTime.Now.ToString();
            foreach (var s in sheets) s.Title = "Stock Length Optimizer · " + s.Name + " · Exported " + exportedAt;
            return sheets;
        }

        // ============================ print ============================

        private void PrintResults()
        {
            if (_results == null) return;
            using (var doc = new PrintDocument())
            {
                var lines = BuildPrintLines();
                int line = 0;
                doc.PrintPage += (s, e) =>
                {
                    var font = new Font("Consolas", 9f);
                    float y = e.MarginBounds.Top;
                    float lh = font.GetHeight(e.Graphics);
                    while (line < lines.Count && y + lh < e.MarginBounds.Bottom)
                    {
                        e.Graphics.DrawString(lines[line], font, Brushes.Black, e.MarginBounds.Left, y);
                        y += lh; line++;
                    }
                    e.HasMorePages = line < lines.Count;
                };
                using (var pd = new PrintDialog { Document = doc })
                    if (pd.ShowDialog(this) == DialogResult.OK)
                    {
                        line = 0;
                        doc.Print();
                    }
            }
        }

        private List<string> BuildPrintLines()
        {
            var lines = new List<string> { "GMS — Stock Length Optimizer", _summaryText, "" };
            string curFinish = null;
            foreach (var r in _results)
            {
                if (r.Finish != curFinish) { curFinish = r.Finish; lines.Add(""); lines.Add("== " + curFinish + " =="); }
                if (r.Error != null) { lines.Add("  " + r.Die + " : " + r.Error); continue; }
                double yd = r.Purchased != 0 ? r.TotalReq / r.Purchased * 100 : 0;
                var lens = PerLengthBreakdown(r);
                string stockText = lens.Count > 1
                    ? string.Join(" + ", lens.Select(l => StockOptimizer.FormatLen(l.StockIn)))
                    : StockOptimizer.FormatLen(r.StockIn);
                string barsText = lens.Count > 1
                    ? string.Join(" + ", lens.Select(l => l.Bars + " @ " + StockOptimizer.FormatLen(l.StockIn)))
                    : r.Bars + "";
                lines.Add("  " + r.Die.PadRight(12) + " stock " + stockText.PadRight(10) +
                          barsText + " bars   yield " + yd.ToString("F1") + "%   drop " + StockOptimizer.FormatLen(r.Purchased - r.TotalReq) +
                          (r.RecommendedTwoLengths ? "   (2 lengths, saves " + StockOptimizer.FormatLen(r.DropSavingsVsSingle) + ")" : ""));
                foreach (var p in r.Patterns)
                    lines.Add("      [" + StockOptimizer.FormatLen(p.StockIn) + "] " + p.Count + " ×  " + string.Join(" + ", p.Cuts.Select(StockOptimizer.FormatLen)));
            }
            return lines;
        }

        // ============================ styled control factories ============================

        private static double ParseDouble(string s, double dflt)
            => double.TryParse((s ?? "").Trim(), NumberStyles.Any, CultureInfo.InvariantCulture, out double v) ? v : dflt;

        private static string FmtWt(double lb) => (lb <= 0) ? "—" : Math.Round(lb).ToString("N0") + " lb";

        private Label MutedLabel(string text) => new Label { Text = text, AutoSize = true, ForeColor = _t.Muted, Font = new Font(FontName, 8.5f) };

        private TextBox AddField(FlowLayoutPanel host, string label, string value)
        {
            var p = new FlowLayoutPanel { FlowDirection = FlowDirection.TopDown, AutoSize = true, Margin = new Padding(6, 0, 6, 0) };
            p.Controls.Add(MutedLabel(label));
            // Kept square (not RoundControl'd): a rounded Region clips the corners off the native
            // single-line border, leaving gaps.
            var tb = new TextBox { Text = value, Width = 96, BorderStyle = BorderStyle.FixedSingle, Font = new Font(FontName, 9.5f), BackColor = _t.PanelBg, ForeColor = _t.Text };
            p.Controls.Add(tb);
            host.Controls.Add(p);
            return tb;
        }

        // Flat stat card: big value over a muted caption, thin themed border.
        private Panel StatCard(string value, string caption, Color? valueColor)
        {
            var card = new Panel { Width = 210, Height = 58, Margin = new Padding(5), BackColor = _t.CardBg };
            card.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = RoundedPath(new Rectangle(0, 0, card.Width - 1, card.Height - 1), CardCornerRadius))
                using (var pen = new Pen(_t.Line))
                    e.Graphics.DrawPath(pen, path);
            };
            RoundControl(card, CardCornerRadius);
            card.Controls.Add(new Label
            {
                Text = value,
                AutoSize = false,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 6, 8, 0),
                Font = new Font(FontName, 15f, FontStyle.Bold),
                ForeColor = valueColor ?? _t.Text
            });
            card.Controls.Add(new Label
            {
                Text = caption.ToUpperInvariant(),
                AutoSize = false,
                Dock = DockStyle.Bottom,
                Height = 18,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(12, 0, 6, 5),
                Font = new Font(FontName, 7.5f),
                ForeColor = _t.Muted
            });
            return card;
        }

        // Shared button font size / height with the rest of the add-in (DarkTheme.UiButton*), so buttons
        // look consistent everywhere. AutoSize keeps the width fit to text; MinimumSize pins the height.
        private static readonly float BtnFontSize = GMSRevitAddin.DarkTheme.UiButtonFontSize;
        private static readonly int BtnHeight = GMSRevitAddin.DarkTheme.UiButtonHeight;

        private Button AccentButton(string text)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = _t.Accent,
                ForeColor = _t.AccentText,
                Font = new Font(FontName, BtnFontSize, FontStyle.Bold),
                Padding = new Padding(12, 0, 12, 0),
                MinimumSize = new Size(90, BtnHeight),
                Cursor = Cursors.Hand,
                Margin = new Padding(6)
            };
            b.FlatAppearance.BorderSize = 0;
            b.FlatAppearance.MouseOverBackColor = _t.AccentHover;
            RoundControl(b, ButtonCornerRadius);
            return b;
        }

        private Button SecondaryButton(string text)
        {
            var b = new Button
            {
                Text = text,
                AutoSize = true,
                FlatStyle = FlatStyle.Flat,
                BackColor = _t.PanelBg,
                ForeColor = _t.Text,
                Font = new Font(FontName, BtnFontSize),
                Padding = new Padding(12, 0, 12, 0),
                MinimumSize = new Size(90, BtnHeight),
                Cursor = Cursors.Hand,
                Margin = new Padding(6)
            };
            // Border drawn along the rounded path (not the native square FlatAppearance border, whose
            // corners the Region would clip into gaps).
            b.FlatAppearance.BorderSize = 0;
            b.Paint += (s, e) =>
            {
                e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                using (var path = RoundedPath(new Rectangle(0, 0, b.Width - 1, b.Height - 1), ButtonCornerRadius))
                using (var pen = new Pen(_t.Line))
                    e.Graphics.DrawPath(pen, path);
            };
            RoundControl(b, ButtonCornerRadius);
            return b;
        }

        private DataGridView MakeGrid()
        {
            var g = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                MultiSelect = false,
                // Autofit every column to its widest cell (including header) so long values (Note/skip
                // messages, cut patterns) aren't clipped and short numeric columns stay narrow.
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.AllCells,
                BorderStyle = BorderStyle.None,
                CellBorderStyle = DataGridViewCellBorderStyle.SingleHorizontal,
                ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.None,
                EnableHeadersVisualStyles = false,
                BackgroundColor = _t.GridBg,
                GridColor = _t.GridLine,
                Font = new Font(FontName, 9f),
                RowTemplate = { Height = 24 },
                ColumnHeadersHeight = 30
            };
            g.DefaultCellStyle.BackColor = _t.GridBg;
            g.DefaultCellStyle.ForeColor = _t.Text;
            g.DefaultCellStyle.SelectionBackColor = _t.GridSelBg;
            g.DefaultCellStyle.SelectionForeColor = _t.GridSelText;
            g.DefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            g.AlternatingRowsDefaultCellStyle.BackColor = _t.GridAltRow;
            g.AlternatingRowsDefaultCellStyle.SelectionBackColor = _t.GridSelBg;
            g.ColumnHeadersDefaultCellStyle.BackColor = _t.GridHeaderBg;
            g.ColumnHeadersDefaultCellStyle.ForeColor = _t.GridHeaderText;
            g.ColumnHeadersDefaultCellStyle.Font = new Font(FontName, 8.5f, FontStyle.Bold);
            g.ColumnHeadersDefaultCellStyle.Padding = new Padding(4, 0, 4, 0);
            return g;
        }

        // MinimumWidth (not Width) so AllCells autofit can still grow these — it just won't shrink
        // below the given size for short/empty columns.
        private DataGridViewTextBoxColumn TextCol(string name, string header, int width)
            => new DataGridViewTextBoxColumn { Name = name, HeaderText = header, MinimumWidth = width };

        private DataGridViewTextBoxColumn NumCol(string name, string header)
            => new DataGridViewTextBoxColumn { Name = name, HeaderText = header, MinimumWidth = 60, DefaultCellStyle = new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight } };
    }
}
