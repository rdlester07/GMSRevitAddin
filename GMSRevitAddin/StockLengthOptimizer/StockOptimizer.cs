using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;

namespace StockLengthOptimizer
{
    // ----------------------------------------------------------------------------------
    // Pure cutting-stock optimizer ported 1:1 from the Stock Length Optimizer index.html.
    // No Revit references — works internally in INCHES like the original JS. Groups by
    // Finish + Die/Mark, runs First-Fit-Decreasing bin packing across candidate stock
    // lengths, and picks the least-drop length per die.
    // ----------------------------------------------------------------------------------

    /// <summary>One optimization unit: a Finish + Die/Mark pair with its required lengths.</summary>
    public class DieGroup
    {
        public string Key;
        public string Finish;
        public string Die;
        public string Desc = "";
        public double Wpf;                                  // weight per foot (lb); 0 if unknown
        // length (inches) -> quantity required
        public readonly Dictionary<double, int> Lens = new Dictionary<double, int>();
    }

    /// <summary>An aggregated cut pattern: <c>Count</c> bars — each of length <c>StockIn</c> — are cut
    /// as <c>Cuts</c>. <c>StockIn</c> lets a die's patterns be grouped/labeled by stock length when a
    /// die uses two lengths (see <see cref="OptimizeResult.StockLengths"/>).</summary>
    public class PatternInfo
    {
        public int Count;
        public double StockIn;
        public List<double> Cuts = new List<double>();      // inches, longest first
    }

    /// <summary>One candidate stock length evaluated for a die (for the comparison table).</summary>
    public class AnalysisRow
    {
        public double StockIn;
        public int Bars;
        public double Purchased;
        public double TotalReq;
        public double Drop;
        public double Yield;        // percent
    }

    /// <summary>Result for a single die at the recommended stock length(s).</summary>
    public class OptimizeResult
    {
        public string Die;
        public string Finish;
        public string Desc;
        public double Wpf;

        public int Bars;
        public double Purchased;
        public double TotalReq;
        public List<PatternInfo> Patterns = new List<PatternInfo>();
        public List<KeyValuePair<double, int>> Oversize = new List<KeyValuePair<double, int>>(); // len->cnt
        public double StockIn;                 // the primary (most-bars) length; scalar-friendly callers use this
        public List<double> StockLengths = new List<double>();  // every distinct length actually purchased (1 or 2)
        public double Usable;
        public double Kerf;
        public double Trim;

        public List<AnalysisRow> Analysis;     // single-length candidate comparison (null if not recommended)
        public bool Recommended;

        // Set when a 2-length combination beat the best single length by at least the savings
        // threshold — see StockOptimizer.MinTwoLengthSavingsIn. DropSavingsVsSingle is the drop
        // (inches) saved vs. the best single-length result; 0 when RecommendedTwoLengths is false.
        public bool RecommendedTwoLengths;
        public double DropSavingsVsSingle;

        public string Error;                   // set instead of a result when infeasible
    }

    /// <summary>Optimizer settings mirroring the HTML's Settings card.</summary>
    public class OptimizerSettings
    {
        public double KerfIn = 0.188;
        public double TrimIn = 3;
        public double RangeMinIn = 12 * 12;    // 12'
        public double RangeMaxIn = 24 * 12;    // 24'
        public double StepIn = 2;              // 2"
    }

    public static class StockOptimizer
    {
        /* ---------- length parsing / formatting (INCHES) — ports parseLen/fmtLen/gcd ---------- */

        /// <summary>Parse a length string to inches. Accepts decimal feet (24.5), feet (24'),
        /// feet-inches (25'-6", 25' 6 1/2"), or bare inches (6", 1/2"). Returns NaN if unparseable.</summary>
        public static double ParseLen(string raw)
        {
            if (raw == null) return double.NaN;
            string s = Regex.Replace(raw.Trim(), @"[, ]+", " ").Trim();
            if (s == "") return double.NaN;

            // pure number => feet
            if (Regex.IsMatch(s, @"^-?\d+(\.\d+)?$"))
                return double.Parse(s, CultureInfo.InvariantCulture) * 12;

            double inches = 0; bool matched = false;

            // feet:  12'
            Match fm = Regex.Match(s, @"(-?\d+(?:\.\d+)?)\s*'");
            if (fm.Success) { inches += double.Parse(fm.Groups[1].Value, CultureInfo.InvariantCulture) * 12; matched = true; }

            // inches: 6"  or  6 1/2"  or fraction 1/2"
            string rest = Regex.Replace(s, @"-?\d+(?:\.\d+)?\s*'", "");
            rest = Regex.Replace(rest, @"[-]", " ");
            Match im = Regex.Match(rest, @"(\d+)?\s*(\d+/\d+)?\s*""");
            if (im.Success && (im.Groups[1].Success || im.Groups[2].Success))
            {
                if (im.Groups[1].Success && im.Groups[1].Value != "")
                    inches += double.Parse(im.Groups[1].Value, CultureInfo.InvariantCulture);
                if (im.Groups[2].Success && im.Groups[2].Value != "")
                {
                    string[] p = im.Groups[2].Value.Split('/');
                    inches += double.Parse(p[0], CultureInfo.InvariantCulture) / double.Parse(p[1], CultureInfo.InvariantCulture);
                }
                matched = true;
            }
            else
            {
                // trailing bare inches without quote after feet, e.g. 12' 6
                Match bm = Regex.Match(rest.Trim(), @"^(\d+(?:\.\d+)?)$");
                if (matched && bm.Success)
                    inches += double.Parse(bm.Groups[1].Value, CultureInfo.InvariantCulture);
            }
            return matched ? inches : double.NaN;
        }

        /// <summary>Format inches as feet-inches rounded to the nearest 1/16" (e.g. 6'-6 1/2").</summary>
        public static string FormatLen(double inch)
        {
            if (double.IsNaN(inch)) return "—";
            bool neg = inch < 0; inch = Math.Abs(inch);
            int feet = (int)Math.Floor(inch / 12 + 1e-7);
            double rem = inch - feet * 12;
            int s16 = (int)Math.Round(rem * 16);
            if (s16 == 192) { feet += 1; s16 = 0; }      // 12" rolled over
            int whole = s16 / 16;
            int frac = s16 - whole * 16;
            string fr = "";
            if (frac > 0) { int g = Gcd(frac, 16); fr = " " + (frac / g) + "/" + (16 / g); }
            string inPart = (whole > 0 || fr != "") ? whole + fr + "\"" : "0\"";
            return (neg ? "-" : "") + feet + "'-" + inPart;
        }

        private static int Gcd(int a, int b) { return b != 0 ? Gcd(b, a % b) : a; }

        /* ---------- optimizer: First-Fit-Decreasing bin packing ---------- */

        // Each bin now carries its own stock length, so a die's bins can mix up to 2 sizes
        // (see OptimizeDieMulti) instead of all sharing one implicit length.
        private class Bin { public double StockIn; public double Rem; public List<double> Cuts = new List<double>(); public bool Main; }

        private static string PatternSig(List<double> cuts)
        {
            return string.Join("|", cuts.OrderByDescending(x => x).Select(x => x.ToString("F4", CultureInfo.InvariantCulture)));
        }

        // Opens a new bin for a piece that didn't fit any existing bin: picks whichever length in
        // `stockLengths` fits the piece with the LEAST leftover slack. This is what routes short
        // pieces toward the shorter stock length and long pieces toward the longer one when two
        // lengths are allowed. Returns null if no provided length fits (piece truly oversize).
        private static Bin OpenBestFitBin(List<double> stockLengths, double trim, double kerf, double pieceLen)
        {
            double need = pieceLen + kerf;
            double bestSlack = double.PositiveInfinity; double bestStock = double.NaN;
            foreach (var s in stockLengths)
            {
                double usable = s - trim;
                if (usable + 1e-6 < need) continue;
                double slack = usable - need;
                if (slack < bestSlack) { bestSlack = slack; bestStock = s; }
            }
            if (double.IsNaN(bestStock)) return null;
            return new Bin { StockIn = bestStock, Rem = bestSlack, Cuts = new List<double> { pieceLen } };
        }

        // Consolidation: any cut pattern that runs on fewer than `threshold` bars has its pieces
        // pulled out and re-fit into the slack of the die's highest-bar-count pattern (and other
        // large-run bars) where they physically fit; the remainder is re-packed. Ports
        // consolidateSmallPatterns. `stockLengths`/`trim` (rather than a single scalar `usable`)
        // let the re-pack step open a best-fit bin of either allowed length.
        private static void ConsolidateSmallPatterns(List<Bin> bins, List<double> stockLengths, double trim, double kerf, int threshold)
        {
            if (threshold <= 0) threshold = 10;
            var counts = new Dictionary<string, int>();
            foreach (var b in bins) { string s = PatternSig(b.Cuts); counts[s] = (counts.TryGetValue(s, out int c) ? c : 0) + 1; }

            // dominant (most-used) pattern — the target run to absorb small patterns into
            string mainSig = null; int mainCount = -1;
            foreach (var kv in counts) { if (kv.Value > mainCount) { mainCount = kv.Value; mainSig = kv.Key; } }
            if (mainCount < threshold) return;           // no large enough run to absorb into

            var small = new List<Bin>(); var keep = new List<Bin>();
            foreach (var b in bins)
            {
                if (counts[PatternSig(b.Cuts)] < threshold) small.Add(b);
                else { b.Main = (PatternSig(b.Cuts) == mainSig); keep.Add(b); }
            }
            if (small.Count == 0) return;

            // loose pieces from the small-run bars, largest first
            var loose = new List<double>();
            foreach (var b in small) foreach (var c in b.Cuts) loose.Add(c);
            loose.Sort((a, b) => b.CompareTo(a));

            var leftover = new List<double>();
            foreach (var len in loose)
            {
                double need = len + kerf;
                // best-fit (tightest slack) into existing large-run bars; prefer the main pattern
                Bin best = null;
                foreach (var b in keep) { if (b.Main && b.Rem + 1e-6 >= need && (best == null || b.Rem < best.Rem)) best = b; }
                if (best == null) { foreach (var b in keep) { if (!b.Main && b.Rem + 1e-6 >= need && (best == null || b.Rem < best.Rem)) best = b; } }
                if (best != null) { best.Rem -= need; best.Cuts.Add(len); }
                else leftover.Add(len);
            }

            // whatever didn't fit is re-packed (FFD) into its own bars
            var newBins = new List<Bin>();
            foreach (var len in leftover)
            {
                double need = len + kerf;
                bool placed = false;
                foreach (var b in newBins) { if (b.Rem + 1e-6 >= need) { b.Rem -= need; b.Cuts.Add(len); placed = true; break; } }
                if (!placed) { var nb = OpenBestFitBin(stockLengths, trim, kerf, len); if (nb != null) newBins.Add(nb); }
            }

            bins.Clear();
            foreach (var b in keep) { b.Main = false; bins.Add(b); }
            foreach (var b in newBins) bins.Add(b);
        }

        /// <summary>Pack one die's required lengths into a single stock length. Thin wrapper over
        /// <see cref="OptimizeDieMulti"/> with a one-element allowed-length set. Ports optimizeDie.</summary>
        public static OptimizeResult OptimizeDie(Dictionary<double, int> lenMap, double stockIn, double kerf, double trim)
            => OptimizeDieMulti(lenMap, new List<double> { stockIn }, kerf, trim);

        /// <summary>Packs one die's required lengths using whichever of the given (1 or 2) stock
        /// lengths fits each piece with the least waste — see <see cref="OpenBestFitBin"/>. A piece is
        /// oversize only if it fits none of the provided lengths.</summary>
        public static OptimizeResult OptimizeDieMulti(Dictionary<double, int> lenMap, List<double> stockLengths, double kerf, double trim)
        {
            var pieces = new List<double>();
            var oversize = new List<KeyValuePair<double, int>>();
            foreach (var kv in lenMap)
            {
                double len = kv.Key; int cnt = kv.Value;
                bool fitsAny = stockLengths.Any(s => (s - trim) + 1e-6 >= len + kerf);
                if (!fitsAny) { oversize.Add(new KeyValuePair<double, int>(len, cnt)); continue; }
                for (int k = 0; k < cnt; k++) pieces.Add(len);
            }
            pieces.Sort((a, b) => b.CompareTo(a));

            var bins = new List<Bin>();
            foreach (var len in pieces)
            {
                double need = len + kerf;
                bool placed = false;
                foreach (var b in bins) { if (b.Rem + 1e-6 >= need) { b.Rem -= need; b.Cuts.Add(len); placed = true; break; } }
                if (!placed) { var nb = OpenBestFitBin(stockLengths, trim, kerf, len); if (nb != null) bins.Add(nb); }
            }

            // fold small-run patterns (<10 bars) into the largest run where pieces fit
            ConsolidateSmallPatterns(bins, stockLengths, trim, kerf, 10);

            // aggregate identical patterns — keyed on cuts + stock length, so patterns from different
            // purchased lengths never merge even if (rarely) they share the same cut list
            var pat = new Dictionary<string, PatternInfo>();
            double totalReq = 0;
            foreach (var b in bins)
            {
                foreach (var c in b.Cuts) totalReq += c;
                string sig = PatternSig(b.Cuts) + "@" + b.StockIn.ToString("F4", CultureInfo.InvariantCulture);
                if (!pat.ContainsKey(sig))
                    pat[sig] = new PatternInfo { Count = 0, StockIn = b.StockIn, Cuts = b.Cuts.OrderByDescending(x => x).ToList() };
                pat[sig].Count++;
            }
            var patterns = pat.Values
                .OrderByDescending(p => p.Cuts.Sum())
                .ToList();

            int bars = bins.Count;
            double purchased = bins.Sum(b => b.StockIn);
            // Group bins by length, most-bars first, so "primary" (StockIn) is the length most of the
            // purchase uses — kept for callers that still want a single representative length.
            var byLength = bins.GroupBy(b => b.StockIn).OrderByDescending(gr => gr.Count()).ToList();
            double primaryStock = byLength.Count > 0 ? byLength[0].Key : (stockLengths.Count > 0 ? stockLengths[0] : 0);
            var stockLengthsUsed = byLength.Select(gr => gr.Key).OrderByDescending(x => x).ToList();

            return new OptimizeResult
            {
                Bars = bars,
                Purchased = purchased,
                TotalReq = totalReq,
                Patterns = patterns,
                Oversize = oversize,
                StockIn = primaryStock,
                StockLengths = stockLengthsUsed,
                Usable = primaryStock - trim,
                Kerf = kerf,
                Trim = trim
            };
        }

        /// <summary>Candidate stock lengths (inches) from the From/To/Step range. Ports candidateLengths.</summary>
        public static List<double> CandidateLengths(OptimizerSettings s)
        {
            if (double.IsNaN(s.RangeMinIn) || double.IsNaN(s.RangeMaxIn) || double.IsNaN(s.StepIn)
                || s.StepIn <= 0 || s.RangeMaxIn < s.RangeMinIn) return null;
            var outv = new List<double>();
            for (double v = s.RangeMinIn; v <= s.RangeMaxIn + 1e-6; v += s.StepIn)
                outv.Add(Math.Round(v * 1000) / 1000);
            return outv;
        }

        // A 2-length combination must clear BOTH bars before it's recommended over a single length —
        // avoids swapping to a second stock length (extra procurement complexity) for a gain that's
        // trivial either in absolute terms or relative to the die's total material. An absolute-only
        // floor is not enough: a big die can clear a small absolute floor from a practically
        // meaningless percentage improvement (observed: a near-uniform cut list saved ~500in / ~2.3%
        // and would otherwise have triggered "2 lengths" for no real benefit). Tune both once
        // validated against real cut lists.
        private const double MinTwoLengthSavingsIn = 24.0;          // ~2 ft absolute floor
        private const double MinTwoLengthSavingsFraction = 0.03;    // savings must also be >= 3% of the die's required length

        /// <summary>For each die, evaluate every feasible single candidate length (kept for the
        /// "Stock Length Comparison" export) AND a heuristic 2-length combination, automatically
        /// picking whichever wins — single length unless 2 lengths save at least
        /// <see cref="MinTwoLengthSavingsIn"/>. Ports runRecommend. Dies are processed in the given order.</summary>
        public static List<OptimizeResult> RunRecommend(IEnumerable<DieGroup> dies, OptimizerSettings settings)
        {
            var cands = CandidateLengths(settings);
            var results = new List<OptimizeResult>();
            if (cands == null) return results;
            double kerf = settings.KerfIn, trim = settings.TrimIn;

            foreach (var g in dies)
            {
                var lm = g.Lens;
                double longest = 0; foreach (var len in lm.Keys) longest = Math.Max(longest, len);

                // ---- best single length (unchanged; also feeds the Excel comparison sheet) ----
                var analysis = new List<AnalysisRow>(); AnalysisRow best = null;
                foreach (var c in cands)
                {
                    double stockIn = c;
                    if (stockIn - trim < longest + kerf - 1e-6) continue;     // too short -> infeasible
                    var r = OptimizeDie(lm, stockIn, kerf, trim);
                    if (r.Oversize.Count > 0) continue;
                    double drop = r.Purchased - r.TotalReq;
                    var row = new AnalysisRow
                    {
                        StockIn = stockIn,
                        Bars = r.Bars,
                        Purchased = r.Purchased,
                        TotalReq = r.TotalReq,
                        Drop = drop,
                        Yield = r.Purchased != 0 ? r.TotalReq / r.Purchased * 100 : 0
                    };
                    analysis.Add(row);
                    if (best == null || row.Drop < best.Drop - 1e-6
                        || (Math.Abs(row.Drop - best.Drop) < 1e-6 && row.Bars < best.Bars))
                        best = row;
                }

                OptimizeResult bestSingleFull = best != null ? OptimizeDie(lm, best.StockIn, kerf, trim) : null;
                double singleDrop = bestSingleFull != null ? bestSingleFull.Purchased - bestSingleFull.TotalReq : double.PositiveInfinity;

                // ---- heuristic 2-length search: anchor on the single-length winner (or the longest
                // candidate if no single length is feasible) and search for the best complementary
                // second length. O(N) — not an exhaustive O(N^2) search over every pair — see plan notes. ----
                double anchor = best?.StockIn ?? cands.Max();
                OptimizeResult bestPair = null; double bestPairDrop = double.PositiveInfinity;
                foreach (var c in cands)
                {
                    if (Math.Abs(c - anchor) < 1e-6) continue;                        // degenerates to single length
                    double maxUsable = Math.Max(anchor, c) - trim;
                    if (maxUsable < longest + kerf - 1e-6) continue;                  // neither length covers the longest piece
                    var pairLens = new List<double> { anchor, c };
                    var r = OptimizeDieMulti(lm, pairLens, kerf, trim);
                    if (r.Oversize.Count > 0) continue;
                    double drop = r.Purchased - r.TotalReq;
                    if (drop < bestPairDrop - 1e-6) { bestPairDrop = drop; bestPair = r; }
                }

                OptimizeResult chosen; bool twoLengths = false; double savings = 0;
                if (bestSingleFull != null)
                {
                    double candidateSavings = singleDrop - bestPairDrop;
                    double savingsFraction = bestSingleFull.TotalReq > 0 ? candidateSavings / bestSingleFull.TotalReq : 0;
                    bool worthIt = candidateSavings >= MinTwoLengthSavingsIn && savingsFraction >= MinTwoLengthSavingsFraction;
                    if (bestPair != null && bestPair.StockLengths.Count > 1 && worthIt)
                    {
                        chosen = bestPair; twoLengths = true; savings = candidateSavings;
                    }
                    else
                    {
                        chosen = bestSingleFull;
                    }
                }
                else if (bestPair != null)
                {
                    // Single length was infeasible ("exceeds search range"), but two lengths together
                    // cover the die — rescue it instead of reporting an error.
                    chosen = bestPair;
                    twoLengths = bestPair.StockLengths.Count > 1;
                }
                else
                {
                    results.Add(new OptimizeResult
                    {
                        Die = g.Die,
                        Finish = g.Finish,
                        Desc = g.Desc,
                        Wpf = g.Wpf,
                        Error = "Longest piece " + FormatLen(longest) + " exceeds the search range — widen 'Search to'."
                    });
                    continue;
                }

                chosen.Die = g.Die; chosen.Finish = g.Finish; chosen.Desc = g.Desc; chosen.Wpf = g.Wpf;
                chosen.Analysis = analysis; chosen.Recommended = true;
                chosen.RecommendedTwoLengths = twoLengths;
                chosen.DropSavingsVsSingle = twoLengths ? savings : 0;
                results.Add(chosen);
            }
            return results;
        }
    }
}
