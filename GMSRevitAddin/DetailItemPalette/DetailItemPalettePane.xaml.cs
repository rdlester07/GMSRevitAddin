#nullable enable
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using WpfVisibility = System.Windows.Visibility;

namespace GMS.Tools.DetailItemPalette
{
    public partial class DetailItemPalettePane : UserControl, IDockablePaneProvider
    {
        private ExternalEvent? _navigateEvent;
        private NavigateToSheetHandler? _navigateHandler;
        private ElementId? _currentSymbolId;
        private bool _currentIsGroup;
        private UITheme? _appliedTheme;

        public DetailItemPalettePane()
        {
            InitializeComponent();
        }

        public void SetupDockablePane(DockablePaneProviderData data)
        {
            data.FrameworkElement = this;
            data.InitialState = new DockablePaneState
            {
                DockPosition = DockPosition.Right,
                TabBehind = DockablePanes.BuiltInDockablePanes.ProjectBrowser
            };
        }

        internal void SetExternalEvent(ExternalEvent evt, NavigateToSheetHandler handler)
        {
            _navigateEvent = evt;
            _navigateHandler = handler;
        }

        /// <summary>Re-reads Revit's current light/dark theme and re-applies the palette brushes
        /// when it has changed, so a theme switch during the session is picked up the next time the
        /// pane updates. Skips the work (and brush churn) when the theme is unchanged.</summary>
        internal void ApplyCurrentTheme()
        {
            var theme = UIThemeManager.CurrentTheme;
            if (_appliedTheme == theme) return;
            ApplyTheme(theme);
        }

        internal void UpdateSheets(string typeName, ElementId? symbolId, bool isGroup, List<SheetInfo> sheets)
        {
            ApplyCurrentTheme();
            _currentSymbolId = symbolId;
            _currentIsGroup = isGroup;
            HeaderLabel.Text = typeName;

            if (sheets.Count == 0)
            {
                SubHeaderLabel.Text = string.Empty;
                PlaceholderText.Text = "This detail item is not placed on any sheet.";
                PlaceholderText.Visibility = WpfVisibility.Visible;
                SheetList.ItemsSource = null;
            }
            else
            {
                string label = sheets.Count == 1 ? "1 sheet" : $"{sheets.Count} sheets";
                SubHeaderLabel.Text = $"Found on {label}:";
                PlaceholderText.Visibility = WpfVisibility.Collapsed;
                SheetList.ItemsSource = sheets;
            }
        }

        internal void ClearSheets()
        {
            ApplyCurrentTheme();
            HeaderLabel.Text = "Select a detail item";
            SubHeaderLabel.Text = string.Empty;
            PlaceholderText.Text = "No detail item selected.";
            PlaceholderText.Visibility = WpfVisibility.Visible;
            SheetList.ItemsSource = null;
        }

        internal void ApplyTheme(UITheme theme)
        {
            // Neutral gray scale (matches DarkTheme.Palette): 50 f9fafb … 950 030912. No blue accent —
            // link affordance is carried by the underline/weight triggers in the XAML, not by hue.
            bool dark = theme == UITheme.Dark;
            SetBrush("PaletteBg",         dark ? "#111b27" : "#f9fafb");
            SetBrush("PaletteHeaderBg",   dark ? "#374451" : "#e5e8eb");
            SetBrush("PaletteHeaderText", dark ? "#f3f4f6" : "#111b27");
            SetBrush("PaletteSubText",    dark ? "#d1d6db" : "#374451");
            SetBrush("PaletteMutedText",  dark ? "#6f7985" : "#6f7985");
            SetBrush("LinkNormal",        dark ? "#9ca5af" : "#4b5863");
            SetBrush("LinkHover",         dark ? "#e5e8eb" : "#111b27");
            SetBrush("LinkPressed",       dark ? "#f9fafb" : "#030912");
            _appliedTheme = theme;
        }

        // The DynamicResource brushes get frozen once the pane has rendered, so mutating Color
        // in place throws "...is in a read-only state". Replace the resource with a fresh brush
        // when the existing one is frozen; DynamicResource references pick up the swap.
        private void SetBrush(string key, string hex)
        {
            var color = (System.Windows.Media.Color)ColorConverter.ConvertFromString(hex);
            if (Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
                brush.Color = color;
            else
                Resources[key] = new SolidColorBrush(color);
        }

        private void SheetButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn &&
                btn.Tag is SheetInfo info &&
                _navigateHandler != null &&
                _navigateEvent != null)
            {
                _navigateHandler.TargetSheetId = info.SheetId;
                _navigateHandler.TargetSymbolId = _currentSymbolId;
                _navigateHandler.IsGroup = _currentIsGroup;
                _navigateEvent.Raise();
            }
        }
    }
}
