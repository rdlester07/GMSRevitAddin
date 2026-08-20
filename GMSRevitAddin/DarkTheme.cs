using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace GMSRevitAddin
{
    /// <summary>
    /// Recursively themes a WinForms form to match Revit's current UI theme. Despite the legacy
    /// name, it now follows <c>UIThemeManager.CurrentTheme</c> — dark slate when Revit is dark,
    /// near-native light when Revit is light. Font style/size are never modified. The
    /// <c>Apply(Form)</c> signature is unchanged so existing call sites keep working.
    /// </summary>
    internal static class DarkTheme
    {
        /// <summary>Single source of truth for the add-in's UI font. Targets the crisp, neutral
        /// grotesque-sans look of the reference design (closer to Inter/SF than a rounded system font):
        /// "Segoe UI Variable Text" — Windows 11's modern UI font, a close system-font match — with a
        /// fallback to plain "Segoe UI" on machines that don't have it (Windows 10). Previously preferred
        /// "Segoe UI Rounded" for an iPadOS-like bubble look; dropped because the reference design's
        /// terminals are sharp, not rounded. Change here to re-font every form + the self-themed
        /// surfaces (StockOptimizerForm, WorksetCyclerWindow, DetailItemPalettePane all reference it).</summary>
        internal static readonly string UiFontName = ResolveUiFontName();

        private static string ResolveUiFontName()
        {
            const string preferred = "Segoe UI Variable Text";
            try
            {
                foreach (var fam in System.Drawing.FontFamily.Families)
                    if (string.Equals(fam.Name, preferred, System.StringComparison.OrdinalIgnoreCase))
                        return preferred;
            }
            catch { /* enumeration failed — fall back */ }
            return "Segoe UI";
        }

        // Corner radii for the "rounded card" look — buttons get a tighter radius than card-style
        // group boxes so small controls don't look overly pill-shaped.
        private const int ButtonCornerRadius = 6;
        private const int CardCornerRadius = 10;

        // Uniform button sizing across the whole add-in, so every dialog's buttons look the same height
        // and font size regardless of how the individual designer laid them out. Referenced by the
        // self-themed StockOptimizerForm too.
        internal const int UiButtonHeight = 30;
        internal const float UiButtonFontSize = 9f;

        /// <summary>Primary text color for the active Revit theme. Use this instead of
        /// <c>SystemColors.ControlText</c> when a form has to set a ForeColor itself (e.g. a
        /// placeholder that toggles color on focus) — system colors don't follow the theme.</summary>
        internal static Color CurrentForeText => Palette.Current().ForeText;

        /// <summary>De-emphasized text color (placeholder/hint text) for the active Revit theme.
        /// The themed replacement for <c>SystemColors.ControlDark</c>.</summary>
        internal static Color CurrentMutedText => Palette.Current().MutedText;

        /// <summary>Semantic error/invalid-state text color (e.g. inline field-validation messages).
        /// Deliberately not on the gray scale — same reasoning and same hex pair as
        /// <c>StockOptimizerForm.Theme.Bad</c>: this encodes a fail state, not surface color.</summary>
        internal static Color CurrentErrorText => Palette.Current().ErrorText;

        /// <summary>
        /// The dialog's one primary/call-to-action button, marked via <see cref="MarkPrimary"/>, gets
        /// painted with this instead of the neutral button gray. No hue — stays on the gray scale (an
        /// earlier version used GMS's ribbon-icon red as a true accent color; removed by request). The
        /// "primary" look is achieved by contrast alone: the near-inverse of the form background (the
        /// lightest gray step in dark mode, the darkest in light mode), the same "solid ink surface"
        /// convention plenty of monochrome UIs use for a single emphasized action.
        /// </summary>
        internal static Color CurrentPrimaryFill => Palette.Current().PrimaryFill;
        internal static Color CurrentPrimaryText => Palette.Current().PrimaryText;

        /// <summary>Marker used to distinguish this codebase's Tag values from a genuine sentinel like
        /// null or another form's own Tag usage (grep shows no Button.Tag use elsewhere today).</summary>
        private const string PrimaryButtonTag = "GmsPrimaryButton";

        /// <summary>
        /// Marks <paramref name="btn"/> as the dialog's one primary action (e.g. "Create", "Save") so
        /// the next <see cref="Apply"/> paints it with <see cref="CurrentPrimaryFill"/> instead of the
        /// neutral button gray. Call this BEFORE <see cref="Apply"/> — Apply reads the marker, it
        /// doesn't push a live update. Mark at most one button per dialog.
        /// </summary>
        internal static void MarkPrimary(Button btn)
        {
            btn.Tag = PrimaryButtonTag;
        }

        // A disabled native Edit control (TextBox.Enabled = false) ignores any assigned ForeColor/
        // BackColor and paints using the OS "grayed out" visual style instead — unreadable against a
        // dark background. Opting the control out of visual styles makes Windows fall back to classic
        // (unthemed) rendering, which honors WM_CTLCOLORSTATIC and therefore our assigned colors even
        // while disabled. See ApplyControl's TextBox case.
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        private static extern int SetWindowTheme(System.IntPtr hWnd, string pszSubAppName, string pszSubIdList);

        // ── Palette (chosen per Apply from the active Revit theme) ──────────────
        private class Palette
        {
            public Color FormBack, PanelBack, ControlBack, ButtonBack, ForeText, BorderColor,
                         SelectionBack, AltRowBack, ButtonHover, ButtonDown, MutedText, ErrorText,
                         PrimaryFill, PrimaryHover, PrimaryPressed, PrimaryText;
            public bool Dark;

            public static Palette Current()
            {
                bool dark = false;
                try { dark = Autodesk.Revit.UI.UIThemeManager.CurrentTheme == Autodesk.Revit.UI.UITheme.Dark; }
                catch { /* older/headless Revit — default light */ }

                // Neutral gray scale (Tailwind-gray-style): 50 f9fafb, 100 f3f4f6, 200 e5e8eb,
                // 300 d1d6db, 400 9ca5af, 500 6f7985, 600 4b5863, 700 374451, 800 1f2c37,
                // 900 111b27, 950 030912. No blue/purple accent — selection/hover/pressed all sit
                // on distinct gray steps so they stay distinguishable without hue.
                return dark ? new Palette
                {
                    Dark = true,
                    // ControlBack sits one step past PanelBack (not level with it) so a field stays
                    // visible whether it's placed directly on the form OR inside a GroupBox/Panel —
                    // both of which paint PanelBack. A field level with PanelBack disappears completely
                    // inside a grouped section (see DimensionNoteForm, whose text boxes live in
                    // GroupBoxes) since there'd be no drawn border left to distinguish them.
                    FormBack = C(0x111b27), PanelBack = C(0x1f2c37), ControlBack = C(0x374451),
                    ButtonBack = C(0x374451), ForeText = C(0xf3f4f6), BorderColor = C(0x4b5863),
                    SelectionBack = C(0x6f7985), AltRowBack = C(0x030912),
                    ButtonHover = C(0x4b5863), ButtonDown = C(0x6f7985), MutedText = C(0x9ca5af),
                    ErrorText = C(0xF1746B),
                    // On a dark surface the highest-contrast "ink" is the lightest gray step, so the
                    // primary button inverts toward white instead of getting darker.
                    PrimaryFill = C(0xf3f4f6), PrimaryHover = C(0xd1d6db), PrimaryPressed = C(0x9ca5af), PrimaryText = C(0x111b27)
                } : new Palette
                {
                    Dark = false,
                    // ControlBack sits one step past PanelBack (same reasoning as the dark palette
                    // above) so a borderless field stays visible against a bare form background AND
                    // inside a GroupBox/Panel, instead of only the former.
                    FormBack = C(0xf9fafb), PanelBack = C(0xf3f4f6), ControlBack = C(0xe5e8eb),
                    ButtonBack = C(0xe5e8eb), ForeText = C(0x111b27), BorderColor = C(0xd1d6db),
                    SelectionBack = C(0xd1d6db), AltRowBack = C(0xf3f4f6),
                    ButtonHover = C(0xd1d6db), ButtonDown = C(0x9ca5af), MutedText = C(0x6f7985),
                    ErrorText = C(0xC0392B),
                    // On a light surface the highest-contrast "ink" is the darkest gray step — the
                    // mirror image of the dark palette's choice above.
                    PrimaryFill = C(0x111b27), PrimaryHover = C(0x1f2c37), PrimaryPressed = C(0x374451), PrimaryText = C(0xf9fafb)
                };
            }

            private static Color C(int rgb) => Color.FromArgb((rgb >> 16) & 255, (rgb >> 8) & 255, rgb & 255);
        }

        /// <summary>
        /// Applies the theme (matching Revit's current light/dark setting) to a form and all of its
        /// controls recursively. Font style and size are never modified.
        /// </summary>
        internal static void Apply(Form form)
        {
            var p = Palette.Current();
            form.BackColor = p.FormBack;
            form.ForeColor = p.ForeText;
            ApplyFont(form);
            ApplyToControls(form.Controls, p);
        }

        /// <summary>
        /// Re-fonts a control to <see cref="UiFontName"/> while preserving its current size, unit, and
        /// style (bold/italic). Theme-independent — every form gets a consistent font in both light and
        /// dark. No-op if already the target font.
        /// </summary>
        private static void ApplyFont(Control ctrl)
        {
            var f = ctrl.Font;
            if (f == null || f.FontFamily.Name == UiFontName) return;
            try { ctrl.Font = new Font(UiFontName, f.Size, f.Style, f.Unit); }
            catch { /* font substitution failed — leave the control's original font */ }
        }

        // Builds a UI-font instance at a specific point size, preserving style (bold/italic). Falls back
        // to the original font if the family can't be realized.
        private static Font MakeFont(Font from, float size)
        {
            try { return new Font(UiFontName, size, from?.Style ?? FontStyle.Regular); }
            catch { return from; }
        }

        // Draws an anti-aliased border that follows a button's rounded silhouette, so the border stays
        // continuous around the corners (a native square border would be clipped into gaps by the Region).
        private static void ApplyRoundedButtonBorder(Button btn, Color borderColor)
        {
            void Draw(object s, PaintEventArgs e)
            {
                if (btn.Width <= 1 || btn.Height <= 1) return;
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path = RoundedRect(new Rectangle(0, 0, btn.Width - 1, btn.Height - 1), ButtonCornerRadius))
                using (var pen = new Pen(borderColor))
                    e.Graphics.DrawPath(pen, path);
            }
            btn.Paint -= Draw;
            btn.Paint += Draw;
        }

        // Clips a control to a rounded rectangle by assigning its Region, recomputing on resize since
        // Region is pixel-sized. Used for card-style panels/group boxes and (via FlatButton paint) buttons.
        private static void ApplyRegionRounding(Control ctrl, int radius)
        {
            void SetRegion()
            {
                if (ctrl.Width <= 0 || ctrl.Height <= 0) return;
                ctrl.Region = new Region(RoundedRect(new Rectangle(0, 0, ctrl.Width, ctrl.Height), radius));
            }
            ctrl.Resize -= RoundingResize;
            ctrl.Resize += RoundingResize;
            SetRegion();
            void RoundingResize(object s, System.EventArgs e) => SetRegion();
        }

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            int d = System.Math.Max(1, radius * 2);
            d = System.Math.Min(d, System.Math.Min(r.Width, r.Height));
            var path = new GraphicsPath();
            if (d <= 1) { path.AddRectangle(r); path.CloseFigure(); return path; }
            path.AddArc(r.X, r.Y, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Y, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.X, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private static void ApplyToControls(Control.ControlCollection controls, Palette p)
        {
            foreach (Control ctrl in controls)
            {
                ApplyControl(ctrl, p);

                // Recurse into children (GroupBox, Panel, FlowLayoutPanel, SplitContainer, TabPage, etc.)
                if (ctrl.HasChildren)
                    ApplyToControls(ctrl.Controls, p);
            }
        }

        private static void ApplyControl(Control ctrl, Palette p)
        {
            ApplyFont(ctrl);   // consistent Arial on every control (size/style preserved)

            switch (ctrl)
            {
                case Button btn:
                    // A button marked via MarkPrimary gets the dialog's one emphasized fill (still on
                    // the gray scale — see CurrentPrimaryFill); every other button stays neutral.
                    bool isPrimary = PrimaryButtonTag.Equals(btn.Tag as string, System.StringComparison.Ordinal);
                    Color buttonBorder;
                    if (isPrimary)
                    {
                        btn.BackColor = p.PrimaryFill;
                        btn.ForeColor = p.PrimaryText;
                        btn.FlatAppearance.MouseOverBackColor = p.PrimaryHover;
                        btn.FlatAppearance.MouseDownBackColor = p.PrimaryPressed;
                        // A step between the fill and its pressed shade, so the border reads as part of
                        // the same surface rather than the usual neutral BorderColor ring.
                        buttonBorder = p.PrimaryHover;
                    }
                    else
                    {
                        btn.BackColor = p.ButtonBack;
                        btn.ForeColor = p.ForeText;
                        btn.FlatAppearance.MouseOverBackColor = p.ButtonHover;
                        btn.FlatAppearance.MouseDownBackColor = p.ButtonDown;
                        buttonBorder = p.BorderColor;
                    }
                    btn.FlatStyle = FlatStyle.Flat;
                    // Suppress the native flat (square) border — a rounded Region would clip its corners
                    // and leave gaps. We draw our own border along the rounded path in Paint instead.
                    btn.FlatAppearance.BorderSize = 0;
                    // Uniform height + font size across every dialog (skip AutoSize buttons, which size
                    // themselves to content).
                    if (!btn.AutoSize)
                        btn.Height = UiButtonHeight;
                    btn.Font = MakeFont(btn.Font, UiButtonFontSize);
                    ApplyRoundedButtonBorder(btn, buttonBorder);
                    ApplyRegionRounding(btn, ButtonCornerRadius);
                    break;

                case TextBox tb:
                    tb.BackColor = p.ControlBack;
                    tb.ForeColor = p.ForeText;
                    // Flat/borderless field: ControlBack now carries its own contrast against the form
                    // background (see the light-Palette comment above), so a drawn border is no longer
                    // needed to make the field legible. Still deliberately NOT region-rounded — text
                    // inputs are the one control where a Region's hard-edged corner clip would be more
                    // noticeable than a square corner, since there's no drawn border to hide the seam.
                    tb.BorderStyle = BorderStyle.None;
                    // Applied regardless of the control's current Enabled state: some forms disable a
                    // TextBox after DarkTheme.Apply runs, and this needs to already be in effect for
                    // that later disabled state to still render with our colors.
                    if (tb.IsHandleCreated)
                        SetWindowTheme(tb.Handle, "", "");
                    break;

                case Label lbl:
                    lbl.BackColor = Color.Transparent;
                    lbl.ForeColor = p.ForeText;
                    break;

                case GroupBox gb:
                    gb.BackColor = p.PanelBack;
                    gb.ForeColor = p.ForeText;
                    // Not region-rounded: a GroupBox draws its own etched frame, whose corners a rounded
                    // Region would clip into gaps (same issue as text inputs).
                    break;

                case System.Windows.Forms.FlowLayoutPanel flp:
                    flp.BackColor = p.PanelBack;
                    flp.ForeColor = p.ForeText;
                    break;

                case SplitContainer sc:
                    sc.BackColor = p.BorderColor;          // splitter bar
                    sc.Panel1.BackColor = p.FormBack;
                    sc.Panel2.BackColor = p.FormBack;
                    break;

                case TabControl tc:
                    tc.BackColor = p.PanelBack;
                    tc.ForeColor = p.ForeText;
                    break;

                case TabPage tp:
                    tp.BackColor = p.PanelBack;
                    tp.ForeColor = p.ForeText;
                    break;

                case Panel pnl:
                    pnl.BackColor = p.PanelBack;
                    pnl.ForeColor = p.ForeText;
                    // Round free-standing card panels, but not ones docked to fill/edge a form (rounding
                    // those would just clip stray corners off the whole surface).
                    if (pnl.Dock == DockStyle.None)
                        ApplyRegionRounding(pnl, CardCornerRadius);
                    break;

                case CheckBox cb:
                    cb.BackColor = Color.Transparent;
                    cb.ForeColor = p.ForeText;
                    break;

                case RadioButton rb:
                    rb.BackColor = Color.Transparent;
                    rb.ForeColor = p.ForeText;
                    break;

                case ComboBox cmb:
                    cmb.BackColor = p.ControlBack;
                    cmb.ForeColor = p.ForeText;
                    cmb.FlatStyle = FlatStyle.Flat;
                    if (cmb.IsHandleCreated)
                        SetWindowTheme(cmb.Handle, "", "");
                    break;

                case NumericUpDown nud:
                    nud.BackColor = p.ControlBack;
                    nud.ForeColor = p.ForeText;
                    if (nud.IsHandleCreated)
                        SetWindowTheme(nud.Handle, "", "");
                    break;

                case ListBox lb:                            // also covers CheckedListBox
                    lb.BackColor = p.ControlBack;
                    lb.ForeColor = p.ForeText;
                    lb.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case DataGridView dgv:
                    ApplyDataGridView(dgv, p);
                    break;

                case System.Windows.Forms.ProgressBar pb:
                    pb.BackColor = p.ControlBack;
                    pb.ForeColor = p.SelectionBack;
                    break;

                case DateTimePicker dtp:
                    dtp.BackColor = p.ControlBack;
                    dtp.ForeColor = p.ForeText;
                    break;

                default:
                    ctrl.BackColor = p.FormBack;
                    ctrl.ForeColor = p.ForeText;
                    break;
            }
        }

        private static void ApplyDataGridView(DataGridView dgv, Palette p)
        {
            dgv.BackgroundColor = p.FormBack;
            dgv.GridColor = p.BorderColor;
            dgv.BorderStyle = BorderStyle.FixedSingle;

            // Column headers
            dgv.ColumnHeadersDefaultCellStyle.BackColor = p.PanelBack;
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = p.ForeText;
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = p.SelectionBack;
            dgv.ColumnHeadersDefaultCellStyle.SelectionForeColor = p.ForeText;
            dgv.EnableHeadersVisualStyles = false;

            // Default (odd) rows
            dgv.DefaultCellStyle.BackColor = p.ControlBack;
            dgv.DefaultCellStyle.ForeColor = p.ForeText;
            dgv.DefaultCellStyle.SelectionBackColor = p.SelectionBack;
            dgv.DefaultCellStyle.SelectionForeColor = p.ForeText;

            // Alternate (even) rows
            dgv.AlternatingRowsDefaultCellStyle.BackColor = p.AltRowBack;
            dgv.AlternatingRowsDefaultCellStyle.ForeColor = p.ForeText;
            dgv.AlternatingRowsDefaultCellStyle.SelectionBackColor = p.SelectionBack;
            dgv.AlternatingRowsDefaultCellStyle.SelectionForeColor = p.ForeText;

            // Row headers
            dgv.RowHeadersDefaultCellStyle.BackColor = p.PanelBack;
            dgv.RowHeadersDefaultCellStyle.ForeColor = p.ForeText;
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = p.SelectionBack;

            dgv.ForeColor = p.ForeText;
        }
    }
}
