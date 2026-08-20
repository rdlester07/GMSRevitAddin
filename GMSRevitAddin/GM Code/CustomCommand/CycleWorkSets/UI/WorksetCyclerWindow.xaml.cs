using GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets;
using System.Windows.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Markup;
using System;
using System.Collections.Generic;
using System.Linq;

namespace GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.UI
{
    // ================================================================================================
    // == WorksetCyclerWindow - WPF UI for workset visibility control
    // ================================================================================================
    /// <summary>
    /// Modeless, always-on-top WPF window listing the document's user worksets as checkboxes, plus
    /// Select All / Select None / Fit All buttons. Its entire visual tree is built in code (no XAML
    /// file) to avoid a separate XAML build dependency for this module. Checkbox changes are
    /// debounced (150 ms) before being pushed to <see cref="CycleWorksetsController"/>, which raises
    /// the shared <c>ExternalEvent</c> so the actual workset-visibility Revit API calls run on
    /// Revit's own thread via <see cref="CycleWorksetsExternalEvent"/>. Owned/shown by
    /// <see cref="Helpers.WindowManager"/>, which is also responsible for keeping at most one
    /// instance alive per Revit session.
    /// </summary>
    public partial class WorksetCyclerWindow : Window
    {
        // ============================================================================================
        // == Fields
        // ============================================================================================
        private readonly DispatcherTimer _debounceTimer;
        private List<WorksetItem> _items = new List<WorksetItem>();
        private StackPanel _itemsPanel;
        private ScrollViewer _scrollViewer;
        private Button _selectNoneButton;
        private Button _selectAllButton;
        private Button _zoomButton;

        // Theme brushes/templates — resolved once per window instance from Revit's current UI theme.
        // Neutral gray scale (matches DarkTheme.cs): 50 f9fafb … 950 030912, no blue accent. These are
        // instance (not static) fields so reopening the window after a Revit theme switch re-resolves
        // the colors instead of reusing a stale first-run cache. Field names kept (legacy).
        private static bool ThemeDark
        {
            get { try { return Autodesk.Revit.UI.UIThemeManager.CurrentTheme == Autodesk.Revit.UI.UITheme.Dark; } catch { return false; } }
        }
        private static SolidColorBrush Br(int darkRgb, int lightRgb)
        {
            int v = ThemeDark ? darkRgb : lightRgb;
            return new SolidColorBrush(Color.FromRgb((byte)((v >> 16) & 255), (byte)((v >> 8) & 255), (byte)(v & 255)));
        }
        // Active-theme color as an "#FFrrggbb" string, for baking into the XamlReader template strings.
        private static string Hex(int darkRgb, int lightRgb)
            => string.Format("#FF{0:X6}", (ThemeDark ? darkRgb : lightRgb) & 0xFFFFFF);

        private readonly SolidColorBrush _darkFormBack   = Br(0x111b27, 0xf3f4f6);
        private readonly SolidColorBrush _darkPanelBack  = Br(0x1f2c37, 0xf9fafb);
        private readonly SolidColorBrush _darkButtonBack = Br(0x374451, 0xe5e8eb);
        private readonly SolidColorBrush _darkForeText   = Br(0xf3f4f6, 0x111b27);
        private readonly SolidColorBrush _darkBorder     = Br(0x4b5863, 0xd1d6db);

        // ============================================================================================
        // == Constructor
        // ============================================================================================
        /// <summary>
        /// Builds the window's entire WPF visual tree in code (root grid, scrollable checkbox panel,
        /// and the button row), applies the theme-aware brushes/templates and shared UI font, and
        /// wires the lifecycle (position persistence) and button-click handlers. The checkbox list
        /// itself is populated later by <see cref="LoadWorksets"/>.
        /// </summary>
        public WorksetCyclerWindow()
        {
            // Build WPF UI in code to avoid XAML build dependencies
            Title = "Worksets";
            Width = 304;
            Height = 500;
            WindowStyle = WindowStyle.ToolWindow;
            ResizeMode = ResizeMode.CanResize;
            Topmost = true;
            Background = _darkFormBack;
            Foreground = _darkForeText;

            var rootGrid = new Grid { Margin = new Thickness(10), Background = _darkFormBack };
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            rootGrid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            _itemsPanel = new StackPanel { Background = _darkFormBack };
            _scrollViewer = new ScrollViewer { Content = _itemsPanel, Background = _darkFormBack };
            Grid.SetRow(_scrollViewer, 0);
            rootGrid.Children.Add(_scrollViewer);

            var buttonPanel = new StackPanel { Orientation = Orientation.Horizontal, HorizontalAlignment = HorizontalAlignment.Stretch, Margin = new Thickness(0,10,0,0), Background = _darkFormBack };
            _selectNoneButton = new Button { Content = "Select None", Width = 76, Margin = new Thickness(0,0,10,0) };
            _selectAllButton  = new Button { Content = "Select All",  Width = 76, Margin = new Thickness(0,0,10,0) };
            _zoomButton       = new Button { Content = "Fit All",     Width = 76 };
            foreach (var btn in new[] { _selectNoneButton, _selectAllButton, _zoomButton })
            {
                btn.Background      = _darkButtonBack;
                btn.Foreground      = _darkForeText;
                btn.BorderBrush     = _darkBorder;
                btn.BorderThickness = new Thickness(1);
                btn.Template        = GetDarkButtonTemplate();
            }
            buttonPanel.Children.Add(_selectNoneButton);
            buttonPanel.Children.Add(_selectAllButton);
            buttonPanel.Children.Add(_zoomButton);
            Grid.SetRow(buttonPanel, 1);
            rootGrid.Children.Add(buttonPanel);

            Content = rootGrid;

            // Consistent UI font across the add-in (child FontSizes are preserved).
            FontFamily = new FontFamily(GMSRevitAddin.DarkTheme.UiFontName);

            _debounceTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(150) };
            _debounceTimer.Tick += DebounceTimer_Tick;

            Loaded += WorksetCyclerWindow_Loaded;
            LocationChanged += WorksetCyclerWindow_LocationChanged;
            Closed += WorksetCyclerWindow_Closed;

            _selectNoneButton.Click += SelectNoneButton_Click;
            _selectAllButton.Click += SelectAllButton_Click;
            _zoomButton.Click += ZoomButton_Click;
        }

        /// <summary>Restores the window's last on-screen position from <see cref="Globals"/>, if any was saved.</summary>
        private void WorksetCyclerWindow_Loaded(object sender, RoutedEventArgs e)
        {
            if (Globals.WorksetCyclerWindowLeft.HasValue)
                Left = Globals.WorksetCyclerWindowLeft.Value;

            if (Globals.WorksetCyclerWindowTop.HasValue)
                Top = Globals.WorksetCyclerWindowTop.Value;
        }

        /// <summary>Persists the window's current position to <see cref="Globals"/> as the user drags it
        /// (skipped before <see cref="Window.Loaded"/> fires, to avoid overwriting with transient startup values).</summary>
        private void WorksetCyclerWindow_LocationChanged(object sender, EventArgs e)
        {
            if (!IsLoaded)
                return;

            Globals.WorksetCyclerWindowLeft = Left;
            Globals.WorksetCyclerWindowTop = Top;
        }

        /// <summary>Persists the window's final position to <see cref="Globals"/> on close, so it reopens there next time.</summary>
        private void WorksetCyclerWindow_Closed(object sender, EventArgs e)
        {
            Globals.WorksetCyclerWindowLeft = Left;
            Globals.WorksetCyclerWindowTop = Top;
        }

        // ============================================================================================
        // == LoadWorksets - called by WindowManager
        // ============================================================================================
        /// <summary>
        /// Replaces the checkbox list with one <see cref="CheckBox"/> per <see cref="WorksetItem"/>
        /// in <paramref name="items"/> (themed via the dark checkbox template, initial state from
        /// <see cref="WorksetItem.IsChecked"/>). Each checkbox's <c>Tag</c> holds its backing
        /// <see cref="WorksetItem"/> so toggling it writes back <see cref="WorksetItem.IsChecked"/> and
        /// restarts the debounce timer before pushing the change to the controller.
        /// </summary>
        public void LoadWorksets(List<WorksetItem> items)
        {
            _items = items ?? new List<WorksetItem>();
            _itemsPanel.Children.Clear();

            foreach (var it in _items)
            {
                var cb = new CheckBox { Content = it.Name, IsChecked = it.IsChecked, Margin = new Thickness(0,2,0,2), Foreground = _darkForeText };
                cb.Template = GetDarkCheckBoxTemplate();
                cb.Tag = it;
                // Debounce rapid toggling (e.g. click-dragging across several checkboxes) so the
                // Revit-side visibility update only fires once the user pauses.
                cb.Checked += (s, e) => { ((WorksetItem)cb.Tag).IsChecked = true; _debounceTimer.Stop(); _debounceTimer.Start(); };
                cb.Unchecked += (s, e) => { ((WorksetItem)cb.Tag).IsChecked = false; _debounceTimer.Stop(); _debounceTimer.Start(); };
                _itemsPanel.Children.Add(cb);
            }
        }

        // ============================================================================================
        // == DebounceTimer_Tick
        // ============================================================================================
        /// <summary>Fires once the debounce interval elapses with no further checkbox changes: pushes
        /// the current selection to the controller and requests a visibility-only update (no zoom).</summary>
        private void DebounceTimer_Tick(object sender, EventArgs e)
        {
            _debounceTimer.Stop();
            PushSelectionToController();
            CycleWorksetsController.RequestUpdate(zoomAlso: false);
        }

        // ============================================================================================
        // == PushSelectionToController
        // ============================================================================================
        /// <summary>Collects the names of all currently checked worksets and hands them to
        /// <see cref="CycleWorksetsController.UpdateSelectionFromUI"/>.</summary>
        private void PushSelectionToController()
        {
            var selectedNames = _items
                .Where(i => i.IsChecked)
                .Select(i => i.Name)
                .ToList();

            CycleWorksetsController.UpdateSelectionFromUI(selectedNames);
        }

        // ============================================================================================
        // == WorksetCheckBox_Changed
        // ============================================================================================
        /// <summary>Unused by the code-built UI (checkboxes wire their own Checked/Unchecked handlers
        /// in <see cref="LoadWorksets"/>); kept only in case a XAML-based checkbox ever binds to it directly.</summary>
        private void WorksetCheckBox_Changed(object sender, RoutedEventArgs e)
        {
            // kept for compatibility if XAML used; start debounce
            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        // ============================================================================================
        // == SelectNoneButton_Click
        // ============================================================================================
        /// <summary>Unchecks every workset (both the model and the visible checkboxes) and restarts the debounce timer.</summary>
        private void SelectNoneButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].IsChecked = false;
                if (i < _itemsPanel.Children.Count && _itemsPanel.Children[i] is CheckBox cb)
                    cb.IsChecked = false;
            }

            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        // ============================================================================================
        // == SelectAllButton_Click
        // ============================================================================================
        /// <summary>Checks every workset (both the model and the visible checkboxes) and restarts the debounce timer.</summary>
        private void SelectAllButton_Click(object sender, RoutedEventArgs e)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                _items[i].IsChecked = true;
                if (i < _itemsPanel.Children.Count && _itemsPanel.Children[i] is CheckBox cb)
                    cb.IsChecked = true;
            }

            _debounceTimer.Stop();
            _debounceTimer.Start();
        }

        // ============================================================================================
        // == ZoomButton_Click - explicit zoom extents
        // ============================================================================================
        /// <summary>"Fit All" button: applies the current selection immediately (bypassing the debounce)
        /// and requests a visibility update followed by a zoom-to-fit.</summary>
        private void ZoomButton_Click(object sender, RoutedEventArgs e)
        {
            // Ensure selection is up to date
            PushSelectionToController();

            // Request visibility update + zoom
            CycleWorksetsController.RequestUpdate(zoomAlso: true);
        }

        // ============================================================================================
        // == Dark button ControlTemplate — hover and pressed states matching DarkTheme palette
        // ============================================================================================
        /// <summary>Builds a fresh theme-aware <see cref="Button"/> template for this window instance.</summary>
        private ControlTemplate GetDarkButtonTemplate()
            => BuildDarkButtonTemplate();

        /// <summary>
        /// Parses a hand-written XAML string into a rounded <see cref="ControlTemplate"/> for
        /// <see cref="Button"/>, with hover/pressed/disabled triggers colored from the active-theme
        /// gray scale (baked in via <see cref="Hex"/>, since XamlReader needs literal hex, not bindings).
        /// Built via <see cref="XamlReader"/> rather than a compiled XAML file, consistent with this
        /// window's code-only UI construction.
        /// </summary>
        private static ControlTemplate BuildDarkButtonTemplate()
        {
            // Gray scale: hover 600/300, pressed 500/400, disabled bg 950/200, disabled border 700/300,
            // disabled fg 500/400.
            string xaml =
                "<ControlTemplate" +
                "    xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'" +
                "    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'" +
                "    TargetType='{x:Type Button}'>" +
                "  <Border x:Name='border'" +
                "          Background='{TemplateBinding Background}'" +
                "          BorderBrush='{TemplateBinding BorderBrush}'" +
                "          BorderThickness='{TemplateBinding BorderThickness}'" +
                "          CornerRadius='6'" +
                "          SnapsToDevicePixels='True'>" +
                "    <ContentPresenter" +
                "        Margin='{TemplateBinding Padding}'" +
                "        HorizontalAlignment='{TemplateBinding HorizontalContentAlignment}'" +
                "        VerticalAlignment='{TemplateBinding VerticalContentAlignment}'" +
                "        RecognizesAccessKey='True'" +
                "        SnapsToDevicePixels='{TemplateBinding SnapsToDevicePixels}'/>" +
                "  </Border>" +
                "  <ControlTemplate.Triggers>" +
                "    <Trigger Property='IsMouseOver' Value='True'>" +
                "      <Setter TargetName='border' Property='Background' Value='" + Hex(0x4b5863, 0xd1d6db) + "'/>" +
                "    </Trigger>" +
                "    <Trigger Property='IsPressed' Value='True'>" +
                "      <Setter TargetName='border' Property='Background' Value='" + Hex(0x6f7985, 0x9ca5af) + "'/>" +
                "    </Trigger>" +
                "    <Trigger Property='IsEnabled' Value='False'>" +
                "      <Setter TargetName='border' Property='Background' Value='" + Hex(0x030912, 0xe5e8eb) + "'/>" +
                "      <Setter TargetName='border' Property='BorderBrush' Value='" + Hex(0x374451, 0xd1d6db) + "'/>" +
                "      <Setter Property='Foreground' Value='" + Hex(0x6f7985, 0x9ca5af) + "'/>" +
                "    </Trigger>" +
                "  </ControlTemplate.Triggers>" +
                "</ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }

        // ============================================================================================
        // == Dark CheckBox ControlTemplate — custom glyph matching DarkTheme palette
        // ============================================================================================
        /// <summary>Builds a fresh theme-aware <see cref="CheckBox"/> template for this window instance.</summary>
        private ControlTemplate GetDarkCheckBoxTemplate()
            => BuildDarkCheckBoxTemplate();

        /// <summary>
        /// Parses a hand-written XAML string into a <see cref="ControlTemplate"/> for
        /// <see cref="CheckBox"/>: a custom 13x13 rounded box with a hand-drawn checkmark
        /// <see cref="Path"/> (rather than the default WPF checkbox glyph). Colors are baked from the
        /// active-theme gray scale via <see cref="Hex"/> so it follows Revit's light/dark theme.
        /// </summary>
        private static ControlTemplate BuildDarkCheckBoxTemplate()
        {
            // Gray scale: box bg 800/50, box border 600/300, tick 100/900; hover bg 600/300, border 100/900;
            // disabled bg 950/200, border 700/300, tick+label 500/400.
            string xaml =
                "<ControlTemplate" +
                "    xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'" +
                "    xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml'" +
                "    TargetType='{x:Type CheckBox}'>" +
                "  <Grid SnapsToDevicePixels='True'>" +
                "    <Grid.ColumnDefinitions>" +
                "      <ColumnDefinition Width='16'/>" +
                "      <ColumnDefinition Width='*'/>" +
                "    </Grid.ColumnDefinitions>" +
                "    <Border x:Name='checkBoxBorder'" +
                "            Grid.Column='0'" +
                "            Width='13' Height='13'" +
                "            Background='" + Hex(0x1f2c37, 0xf9fafb) + "'" +
                "            BorderBrush='" + Hex(0x4b5863, 0xd1d6db) + "'" +
                "            BorderThickness='1'" +
                "            CornerRadius='4'" +
                "            VerticalAlignment='Center'" +
                "            HorizontalAlignment='Center'>" +
                "      <Path x:Name='checkMark'" +
                "            Data='M 1,4.5 L 4.5,8 L 10.5,2'" +
                "            Stroke='" + Hex(0xf3f4f6, 0x111b27) + "'" +
                "            StrokeThickness='1.5'" +
                "            StrokeStartLineCap='Round'" +
                "            StrokeEndLineCap='Round'" +
                "            StrokeLineJoin='Round'" +
                "            Stretch='None'" +
                "            Visibility='Collapsed'/>" +
                "    </Border>" +
                "    <ContentPresenter" +
                "        Grid.Column='1'" +
                "        Margin='4,0,0,0'" +
                "        VerticalAlignment='Center'" +
                "        HorizontalAlignment='Left'" +
                "        RecognizesAccessKey='True'" +
                "        SnapsToDevicePixels='{TemplateBinding SnapsToDevicePixels}'/>" +
                "  </Grid>" +
                "  <ControlTemplate.Triggers>" +
                "    <Trigger Property='IsChecked' Value='True'>" +
                "      <Setter TargetName='checkMark' Property='Visibility' Value='Visible'/>" +
                "    </Trigger>" +
                "    <Trigger Property='IsMouseOver' Value='True'>" +
                "      <Setter TargetName='checkBoxBorder' Property='BorderBrush' Value='" + Hex(0xf3f4f6, 0x111b27) + "'/>" +
                "      <Setter TargetName='checkBoxBorder' Property='Background' Value='" + Hex(0x4b5863, 0xd1d6db) + "'/>" +
                "    </Trigger>" +
                "    <Trigger Property='IsEnabled' Value='False'>" +
                "      <Setter TargetName='checkBoxBorder' Property='BorderBrush' Value='" + Hex(0x374451, 0xd1d6db) + "'/>" +
                "      <Setter TargetName='checkBoxBorder' Property='Background' Value='" + Hex(0x030912, 0xe5e8eb) + "'/>" +
                "      <Setter TargetName='checkMark' Property='Stroke' Value='" + Hex(0x6f7985, 0x9ca5af) + "'/>" +
                "      <Setter Property='Foreground' Value='" + Hex(0x6f7985, 0x9ca5af) + "'/>" +
                "    </Trigger>" +
                "  </ControlTemplate.Triggers>" +
                "</ControlTemplate>";
            return (ControlTemplate)XamlReader.Parse(xaml);
        }
    }
}
