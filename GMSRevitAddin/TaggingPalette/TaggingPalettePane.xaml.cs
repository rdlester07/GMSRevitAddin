#nullable enable
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Autodesk.Revit.UI;
using WpfVisibility = System.Windows.Visibility;

namespace GMS.Tools.TaggingPalette
{
    public partial class TaggingPalettePane : UserControl, IDockablePaneProvider
    {
        private ExternalEvent? _event;
        private TaggingPaletteEventHandler? _handler;
        private UITheme? _appliedTheme;

        public TaggingPalettePane()
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

        internal void SetExternalEvent(ExternalEvent evt, TaggingPaletteEventHandler handler)
        {
            _event = evt;
            _handler = handler;
        }

        /// <summary>Re-reads Revit's current light/dark theme and re-applies the palette brushes
        /// when it has changed. Skips the work (and brush churn) when the theme is unchanged.</summary>
        internal void ApplyCurrentTheme()
        {
            var theme = UIThemeManager.CurrentTheme;
            if (_appliedTheme == theme) return;
            ApplyTheme(theme);
        }

        internal void LoadGroups(List<TagFamilyGroup> detailItems, List<TagFamilyGroup> genericModels, List<TagFamilyGroup> unitPieces)
        {
            ApplyCurrentTheme();

            DetailItemsList.ItemsSource = detailItems;
            DetailItemsPlaceholder.Visibility = detailItems.Count == 0 ? WpfVisibility.Visible : WpfVisibility.Collapsed;

            GenericModelsList.ItemsSource = genericModels;
            GenericModelsPlaceholder.Visibility = genericModels.Count == 0 ? WpfVisibility.Visible : WpfVisibility.Collapsed;

            UnitPiecesList.ItemsSource = unitPieces;
            UnitPiecesPlaceholder.Visibility = unitPieces.Count == 0 ? WpfVisibility.Visible : WpfVisibility.Collapsed;
        }

        internal void ApplyTheme(UITheme theme)
        {
            // Neutral gray scale (matches DarkTheme.Palette / DetailItemPalettePane): 50 f9fafb …
            // 950 030912. No blue accent — chip affordance is the flat gray fill, not hue.
            bool dark = theme == UITheme.Dark;
            SetBrush("PaletteBg",         dark ? "#111b27" : "#f9fafb");
            SetBrush("PaletteHeaderBg",   dark ? "#374451" : "#e5e8eb");
            SetBrush("PaletteHeaderText", dark ? "#f3f4f6" : "#111b27");
            SetBrush("PaletteSubText",    dark ? "#d1d6db" : "#374451");
            SetBrush("PaletteMutedText",  dark ? "#6f7985" : "#6f7985");
            SetBrush("TypeButtonBg",      dark ? "#243040" : "#e5e8eb");
            SetBrush("TypeButtonBgHover", dark ? "#374451" : "#d1d6db");
            SetBrush("TypeButtonBgPress", dark ? "#4b5863" : "#9ca5af");
            SetBrush("TypeButtonText",    dark ? "#f3f4f6" : "#111b27");
            _appliedTheme = theme;
        }

        // The DynamicResource brushes get frozen once the pane has rendered, so mutating Color
        // in place throws "...is in a read-only state". Replace the resource with a fresh brush
        // when the existing one is frozen; DynamicResource references pick up the swap.
        private void SetBrush(string key, string hex)
        {
            var color = (Color)ColorConverter.ConvertFromString(hex);
            if (Resources[key] is SolidColorBrush brush && !brush.IsFrozen)
                brush.Color = color;
            else
                Resources[key] = new SolidColorBrush(color);
        }

        private void TypeButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is TagTypeButton info && _handler != null && _event != null)
            {
                _handler.Action = TaggingPaletteAction.ActivateType;
                _handler.PendingTypeId = info.TypeId;
                _handler.PendingCategoryId = info.CategoryId;
                _handler.PendingCommand = info.TargetCommand;
                _event.Raise();
            }
        }

        private void RefreshButton_Click(object sender, RoutedEventArgs e)
        {
            if (_handler != null && _event != null)
            {
                _handler.Action = TaggingPaletteAction.Refresh;
                _event.Raise();
            }
        }
    }
}
