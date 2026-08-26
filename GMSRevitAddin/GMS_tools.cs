using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Reflection;
using Autodesk.Revit.Attributes;
using Autodesk.Revit.UI;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI.Selection;
using System.Windows.Media.Imaging;
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Visual;
using System.Windows.Forms;
using System.IO;
using System.Xml;
using System.Xml.Linq;
using GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers;

//https://archi-lab.net/create-your-own-tab-and-buttons-in-revit/
//http://www.icoconverter.com/ 32pxs 32bit // Resources>Add Existing Item> *.png // Select png in list>Build Action = Resource

namespace GMSRevitAddin
{
    /// <summary>
    /// The add-in's single <c>IExternalApplication</c> entry point — the class named as
    /// <c>FullClassName</c> in the generated <c>GMSRevitAddin.addin</c> manifest. Revit instantiates
    /// this once per session; <see cref="OnStartup"/> builds the "GMS" ribbon tab/panels/buttons and
    /// registers application-level events (document opened/saved/synced, view activated, etc.),
    /// and <see cref="OnShutdown"/> unwinds those subscriptions.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class GMS_tools : IExternalApplication
    {
        static string _path = typeof(GMS_tools).Assembly.Location;

        RibbonItem _button;

        internal static GMS_tools _app = null;

        // For the Detail Item Palette: the UIControlledApplication (to unsubscribe the one-shot
        // Idling handler) and the UIApplication captured at first Idling (to unsubscribe SelectionChanged
        // at shutdown). See OnStartup / OnFirstIdle_DetailItemPalette / OnShutdown.
        private static UIControlledApplication _paletteControlledApp = null;
        private static UIApplication _paletteUiApp = null;

        /// <summary>The single running instance of the add-in application, captured in <see cref="AddRibbonPanel"/>.</summary>
        public static GMS_tools Instance
        {
            get { return _app; }
        }

        // One-shot Idling handler: UIApplication is unavailable at OnStartup, so initialize the
        // Detail Item Palette's events/subscription here, then unsubscribe so Idling stops firing.
        private void OnFirstIdle_DetailItemPalette(object sender, IdlingEventArgs e)
        {
            try
            {
                if (sender is UIApplication uiApp)
                {
                    _paletteUiApp = uiApp;
                    // Second chance at Revit's main window handle if OnStartup couldn't get one.
                    if (GMSRevitAddin.GmsUi.RevitMainWindowHandle == IntPtr.Zero)
                        GMSRevitAddin.GmsUi.RevitMainWindowHandle = uiApp.MainWindowHandle;
                    GMS.Tools.DetailItemPalette.PaletteModule.EnsureEvents(uiApp);
                    GMS.Tools.DetailItemPalette.PaletteModule.SubscribeSelectionChanged(uiApp);
                }
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("OnFirstIdle_DetailItemPalette", __ex); }
            finally
            {
                if (_paletteControlledApp != null)
                    _paletteControlledApp.Idling -= OnFirstIdle_DetailItemPalette;
            }
        }

#if REVIT2024
        // Probe the add-in's own directory for a dependency Revit's appbase-based binding can't find
        // (e.g. System.Resources.Extensions). Returning the assembly here bypasses strict version
        // binding, so the deployed 8.0.0.0 satisfies a 4.0.0.0 request. Returns null for unknown
        // names so normal resolution still applies.
        private static Assembly ResolveFromAddinFolder(object sender, ResolveEventArgs args)
        {
            try
            {
                string simpleName = new AssemblyName(args.Name).Name;
                string dir = Path.GetDirectoryName(typeof(GMS_tools).Assembly.Location);
                string candidate = Path.Combine(dir, simpleName + ".dll");
                if (File.Exists(candidate))
                    return Assembly.LoadFrom(candidate);
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("ResolveFromAddinFolder", __ex); }
            return null;
        }
#endif

        /// <summary>
        /// Called once when Revit unloads the add-in (session close). Unregisters the
        /// application-level event subscriptions that OnStartup added, so nothing keeps firing
        /// (or holding a stale UIApplication reference) after shutdown.
        /// </summary>
        public Result OnShutdown(UIControlledApplication application)
        {
            // Unregister application events for Cycle Worksets window manager
            try
            {
                WindowManager.UnregisterApplicationEvents(application);
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("GMS_tools", __ex); }

            // Detail Item Palette: drop the SelectionChanged subscription.
            try
            {
                if (_paletteUiApp != null)
                    GMS.Tools.DetailItemPalette.PaletteModule.UnsubscribeSelectionChanged(_paletteUiApp);
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("GMS_tools.PaletteUnsubscribe", __ex); }

            // Tag Leader defaults: drop the persistent Idling subscription (DocumentChanged is
            // unsubscribed implicitly with the add-in unload; Idling is the one that keeps firing).
            try
            {
                application.Idling -= GMS.Tools.TaggingPalette.TagLeaderDefaults.OnIdling;
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("GMS_tools.TagLeaderDefaultsUnsubscribe", __ex); }

            // Tagging Palette: drop its persistent Idling subscription too.
            try
            {
                application.Idling -= GMS.Tools.TaggingPalette.TaggingPaletteModule.OnIdling;
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("GMS_tools.TaggingPaletteIdlingUnsubscribe", __ex); }

            return Result.Succeeded;
        }

        /// <summary>
        /// Called once when Revit loads the add-in (session start). Wires up the net48
        /// assembly-resolution workaround (Revit 2024 only), installs last-resort exception loggers,
        /// updates the keyboard-shortcut XML, builds the "GMS" ribbon (<see cref="AddRibbonPanel"/>),
        /// registers the Detail Item Palette dockable pane, and subscribes every command's
        /// application-level event handler (document opened/saved/synced, view activated, etc.).
        /// </summary>
        public Result OnStartup(UIControlledApplication application)
        {
#if REVIT2024
            // Revit 2024 runs on .NET Framework 4.8 and loads this add-in in-process; the CLR
            // probes Revit's own install folder (the appbase) for dependencies, NOT the add-in's
            // folder, and applies strict version binding. The net48 build's preserialized WinForms
            // resources depend on System.Resources.Extensions, which is deployed beside this DLL but
            // is otherwise unresolvable (and the requested 4.0.0.0 differs from the shipped 8.0.0.0).
            // Resolve any such sibling dependency by simple name from the add-in's own directory.
            // (Same in-process-resolution gap as the System.Data.Odbc note in CLAUDE.md.)
            AppDomain.CurrentDomain.AssemblyResolve += ResolveFromAddinFolder;
#endif
            // Capture otherwise-silent exceptions (e.g. the intermittent "Improper argument" that
            // locks the UI) with a full stack trace so the source can be identified. These run
            // inside Revit's process; do NOT call Application.SetUnhandledExceptionMode here —
            // Revit's windows already exist, so it would throw InvalidOperationException.
            System.Windows.Forms.Application.ThreadException += (s, e) =>
                GMSRevitAddin.GmsLog.Error("WinForms.ThreadException", e.Exception);
            AppDomain.CurrentDomain.UnhandledException += (s, e) =>
                GMSRevitAddin.GmsLog.Error("AppDomain.UnhandledException", e.ExceptionObject as Exception);

            // Capture Revit's real main window handle for GmsUi to parent dialogs to. This is the
            // Revit-API-correct source; without it GmsUi falls back to Process.MainWindowHandle, which
            // can resolve to some other top-level window in the process (see GmsUi.OwnerHandle).
            try
            {
                GMSRevitAddin.GmsUi.RevitMainWindowHandle = application.MainWindowHandle;
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("GMS_tools.MainWindowHandle", __ex); }

            UpdateKeyboardShortcuts.UpdateKeyboardShortcuts.updateXML();

            //call method to load GMS ribbon tab and buttons
            AddRibbonPanel(application);

            // Detail Item Palette: dockable panes MUST be registered during OnStartup. UIApplication
            // isn't available yet, so grab it on the first Idling event to wire up the palette's
            // ExternalEvent + SelectionChanged listener (the ribbon command also does this defensively).
            try
            {
                GMS.Tools.DetailItemPalette.PaletteModule.RegisterPane(application);
                _paletteControlledApp = application;
                application.Idling += OnFirstIdle_DetailItemPalette;
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("GMS_tools.RegisterPane", __ex); }

            // Tagging Palette: dockable panes MUST be registered during OnStartup. Unlike the Detail
            // Item Palette it has no persistent event subscription to wire up later (no
            // SelectionChanged listener — it refreshes on demand instead), so no Idling handler is
            // needed here; ShowPaletteCommand creates the ExternalEvent lazily on first use.
            try
            {
                GMS.Tools.TaggingPalette.TaggingPaletteModule.RegisterPane(application);
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("GMS_tools.RegisterTaggingPane", __ex); }

            // Tagging Palette: keep button enablement in sync as the user switches views (a
            // Drafting View has no model geometry, so Generic Model / Curtain Wall Panel tag
            // buttons grey out there — see TaggingPaletteModule.ApplyEnablement). Idling, not
            // ViewActivated — ViewActivated misses "Activate View" inside a sheet (see
            // TaggingPaletteModule.OnIdling for why).
            try
            {
                application.Idling += GMS.Tools.TaggingPalette.TaggingPaletteModule.OnIdling;
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("GMS_tools.RegisterTaggingPaneIdling", __ex); }

            // Tag Leader defaults ("Free End" for Detail Item Tags / Generic Model Tags — see
            // TagLeaderDefaults.cs): a persistent Idling subscription, unlike the one-shot handlers
            // above, since it needs to keep correcting newly placed tags for the whole session.
            try
            {
                application.ControlledApplication.DocumentChanged += GMS.Tools.TaggingPalette.TagLeaderDefaults.ControlledApplication_DocumentChanged;
                application.Idling += GMS.Tools.TaggingPalette.TagLeaderDefaults.OnIdling;
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("GMS_tools.RegisterTagLeaderDefaults", __ex); }
            // Register Cycle Worksets application-level events (WPF window behavior)
            try
            {
                WindowManager.RegisterApplicationEvents(application);
            }
            catch (System.Exception __ex) { GMSRevitAddin.GmsLog.Error("GMS_tools", __ex); }
            if (Properties.Settings.Default.AutoWarningSuppress == true)
            {
                RevitWarningSuppresion.registerWarningSuppresion.updatedbool(true);
            }
            if (Properties.Settings.Default.UnitLines == false)
            {
                NotaReference.RevitStartup.updatedbool(false);
            }
            // Application-level event registrations: each feature module owns its own nested
            // RevitStartup class holding the actual handler; GMS_tools just wires it to the
            // matching Revit event here at startup.
            application.ControlledApplication.ApplicationInitialized += RevitWarningSuppresion.RevitStartup.ControlledApplication_ApplicationInitialized;
            application.ControlledApplication.DocumentOpened += SettingsForm.RevitStartup.ControlledApplication_DocumentOpened;
            application.ControlledApplication.DocumentSynchronizedWithCentral += SettingsForm.RevitStartup.ControlledApplication_DocumentSynchronizedWithCentral;
            application.ControlledApplication.DocumentSaved += SettingsForm.RevitStartup.ControlledApplication_DocumentSaved;
            application.ControlledApplication.DocumentChanged += DuplicateViewOptions.RevitStartup.ControlledApplication_DocumentChanged;
            application.ViewActivated += DuplicateViewOptions.RevitStartup.ControlledApplication_ViewActivated;
            application.ControlledApplication.DocumentOpened += ViewNames.RevitStartup.ControlledApplication_DocumentOpened;
            application.ControlledApplication.DocumentChanged += NotaReference.RevitStartup.ControlledApplication_DocumentChanged;
            application.ControlledApplication.DocumentOpened += TagParameterUpdater.RevitStartup.ControlledApplication_DocumentOpened;
            application.ViewActivated += UpdateOrigins.updateTags.onViewChange;


            return Result.Succeeded;
        }

        // Returns the ribbon-button image for a Resources png, swapping in the monochrome light-gray
        // "<name>_dark.png" variant when Revit is in dark theme so the GMS icons read on the dark
        // ribbon. The ribbon is built once at OnStartup, so the choice is fixed at Revit startup —
        // switching Revit's theme mid-session needs a restart to re-tint the GMS icons.
        private static BitmapImage Icon(string file)
        {
            string name = file;
            try
            {
                if (Autodesk.Revit.UI.UIThemeManager.CurrentTheme == Autodesk.Revit.UI.UITheme.Dark)
                    name = System.IO.Path.GetFileNameWithoutExtension(file) + "_dark" + System.IO.Path.GetExtension(file);
            }
            catch { /* older/headless Revit — use original */ }
            return new BitmapImage(new Uri("pack://application:,,,/GMSRevitAddin;component/Resources/" + name));
        }

        //static void AddRibbonPanel(UIControlledApplication application)
        /// <summary>
        /// Builds the "GMS" ribbon tab: creates each panel, then for every command a
        /// <c>PushButtonData</c> (or <c>PulldownButtonData</c> with nested <c>PushButtonData</c>
        /// sub-items) pointing at the add-in assembly and a <c>"Namespace.Class"</c> string.
        /// ⚠️ Those strings are NOT compile-checked — renaming a targeted command's namespace/class
        /// compiles fine but silently breaks the button at runtime; update the string here and
        /// smoke-test the button if you rename anything a button targets. Called once from
        /// <see cref="OnStartup"/>; the ribbon (and its icon set — see <see cref="Icon"/>) is fixed
        /// for the rest of the Revit session.
        /// </summary>
        void AddRibbonPanel(UIControlledApplication application)
        {
            _app = this;
            string tabName = "GMS";
            application.CreateRibbonTab(tabName);
            // One RibbonPanel per logical grouping shown on the GMS tab.
            RibbonPanel GMSSettings = application.CreateRibbonPanel(tabName, "Settings");
            RibbonPanel GMSPieceExtraction = application.CreateRibbonPanel(tabName, "Piece Extraction");
            RibbonPanel GMSGraphicsOverrides = application.CreateRibbonPanel(tabName, "Graphical Overrides");
            RibbonPanel GMSTagging = application.CreateRibbonPanel(tabName, "Tagging Tools");
            RibbonPanel GMSUnitTools = application.CreateRibbonPanel(tabName, "Unit Drawing Tools");
            RibbonPanel GMSDetailItems = application.CreateRibbonPanel(tabName, "Detail Items");
            RibbonPanel GMSToolsribbonPanel = application.CreateRibbonPanel(tabName, "GMS Tools");

            string thisAssemblyPath = Assembly.GetExecutingAssembly().Location;

            // Simple PushButtonData example: id, display text, assembly path, "Namespace.Class"
            // command string. pbSettings.LargeImage is set via the Icon() helper (below), which
            // swaps in the "_dark" icon variant when Revit is in dark theme.
            PushButtonData SettingsData = new PushButtonData("Settings", "Settings", thisAssemblyPath, "SettingsForm.LaunchForm");
            PushButton pbSettings = GMSSettings.AddItem(SettingsData) as PushButton;
            pbSettings.ToolTip = "Project and Personal Settings Menu.";
            BitmapImage pbSettingsImage = Icon("settings.png");
            pbSettings.LargeImage = pbSettingsImage;

            // "Show Tags per Sheet" / "Hide Tags per Sheet" buttons.
            PushButtonData TagData = new PushButtonData("Show Tags per Sheet", "Show Tags" + System.Environment.NewLine + "Per View", _path, "ShowTagsPerView.ShowTags");
            _button = GMSTagging.AddItem(TagData);
            PushButton pbTag = _button as PushButton;
            pbTag.ToolTip = "Make tags visible in view.";
            BitmapImage pbTagImage = Icon("showTags.png");
            pbTag.LargeImage = pbTagImage;

            PushButtonData HideTagData = new PushButtonData("Hide Tags per Sheet", "Hide Tags" + System.Environment.NewLine + "Per View", _path, "ShowTagsPerView.HideTags");
            _button = GMSTagging.AddItem(HideTagData);
            PushButton pbHideTag = _button as PushButton;
            pbHideTag.ToolTip = "Make tags invisible in view.";
            BitmapImage pbHideTagImage = Icon("hideTags.png");
            pbHideTag.LargeImage = pbHideTagImage;

            // Pulldown example: a PulldownButtonData is the dropdown container; its own
            // Icon/ToolTip are set first, then each dropdown entry is a separate PushButtonData
            // added via AddPushButton (no icon/tooltip of its own — inherits the pulldown's look).
            PulldownButtonData showHidePulldownData = new PulldownButtonData("ShowHideTagsByType", "Tag Visibility" + System.Environment.NewLine + "By Set");
            PulldownButton showHidePulldownButton = GMSTagging.AddItem(showHidePulldownData) as PulldownButton;
            showHidePulldownButton.ToolTip = "Show or Hide tags in views by set.";
            BitmapImage showHidePulldownImage = Icon("showhidetags.png");
            showHidePulldownButton.LargeImage = showHidePulldownImage;
            PushButtonData showPBdata = new PushButtonData("ShowAllTags", "Show Tags", thisAssemblyPath, "ShowTags.ShowTags");
            PushButtonData hidePBdata = new PushButtonData("HideAllTags", "Hide Tags", thisAssemblyPath, "ShowTags.HideTags");
            RibbonItem showTags = showHidePulldownButton.AddPushButton(showPBdata);
            RibbonItem hideTags = showHidePulldownButton.AddPushButton(hidePBdata);

            // "Update Tags" pulldown: Current Sheet / By Set / All.
            PulldownButtonData TagSheetData = new PulldownButtonData("Update Tags", "Update Tags");
            PulldownButton pbTagSheet = GMSTagging.AddItem(TagSheetData) as PulldownButton;
            pbTagSheet.ToolTip = "Update tag data.";
            BitmapImage pbTagSheetImage = Icon("sheetUpdate.png");
            pbTagSheet.LargeImage = pbTagSheetImage;
            PushButtonData utCurrent = new PushButtonData("Current Sheet", "Current Sheet", thisAssemblyPath, "TagUpdateCurrentSheet.CurrentSheetOriginUpdate");
            PushButtonData utBySet = new PushButtonData("By Set", "By Set", thisAssemblyPath, "BySetForm.LaunchForm");
            PushButtonData utAll = new PushButtonData("All", "All", thisAssemblyPath, "ManualUpdateOrigins.ManualUpdate");
            RibbonItem current = pbTagSheet.AddPushButton(utCurrent);
            RibbonItem byset = pbTagSheet.AddPushButton(utBySet);
            RibbonItem allTags = pbTagSheet.AddPushButton(utAll);


            // "Halftone" button.
            PushButtonData MHData = new PushButtonData("Halftone", "Halftone", _path, "MakeHalftone.MakeHalftone");
            PushButton pbMH = GMSGraphicsOverrides.AddItem(MHData) as PushButton;
            pbMH.ToolTip = "Change selected items to halftone.";
            BitmapImage pbMHImage = Icon("mhf.png");
            pbMH.LargeImage = pbMHImage;

            // "Halftone & Dashed" button.
            PushButtonData NBGData = new PushButtonData("Halftone & Dashed", "Halftone" + System.Environment.NewLine + "& Dashed", _path, "NotByGMS.NotByGMS");
            PushButton pbNBG = GMSGraphicsOverrides.AddItem(NBGData) as PushButton;
            pbNBG.ToolTip = "Change selected items to halftone and dashed.";
            BitmapImage pbNBGImage = Icon("NBG.png");
            pbNBG.LargeImage = pbNBGImage;

            // "Reset Graphical Overrides" button.
            PushButtonData GOResetData = new PushButtonData("Reset Graphical Overrides", "Reset" + System.Environment.NewLine + "Overrides", _path, "ResetOverrides.ResetOverrides");
            PushButton pbGOReset = GMSGraphicsOverrides.AddItem(GOResetData) as PushButton;
            pbGOReset.ToolTip = "Resets all graphical overrides of selected items.";
            BitmapImage pbGOResetImage = Icon("reset.png");
            pbGOReset.LargeImage = pbGOResetImage;

            // "Extract Pieces" button — wired to ExportParts.ExportPieces (the pre-flight +
            // tag-update orchestrator), not ExportParts.Export directly (see CLAUDE.md).
            PushButtonData b2Data = new PushButtonData("Extract Pieces", "Export" + System.Environment.NewLine + "Pieces", thisAssemblyPath, "ExportParts.ExportPieces");
            PushButton pb2 = GMSPieceExtraction.AddItem(b2Data) as PushButton;
            pb2.ToolTip = "Export Piece Schedule for Parts Collection";
            BitmapImage pb2Image = Icon("Export.png");
            pb2.LargeImage = pb2Image;

            // "Tag Units" button.
            PushButtonData UnitTagData = new PushButtonData("Tag Units", "Tag" + System.Environment.NewLine + "Units", _path, "TagUnits.TagUnits");
            PushButton pbUnitTag = GMSTagging.AddItem(UnitTagData) as PushButton;
            pbUnitTag.ToolTip = "Select bounding box containing units to be tagged.";
            BitmapImage pbUnitTagImage = Icon("tagunits.png");
            pbUnitTag.LargeImage = pbUnitTagImage;

            // "New Unit Sheet" button.
            PushButtonData CreateUnitData = new PushButtonData("New Unit Sheet", "New Unit" + System.Environment.NewLine + "Sheet", _path, "CreateUnitSheet.CreateUnitSheet");
            PushButton pbCreateUnit = GMSUnitTools.AddItem(CreateUnitData) as PushButton;
            pbCreateUnit.ToolTip = "Create a new Unit Drawing Sheet with schedule.";
            BitmapImage pbCreateUnitImage = Icon("newUnit.png");
            pbCreateUnit.LargeImage = pbCreateUnitImage;

            // "Dimension Note" button.
            PushButtonData DimNoteData = new PushButtonData("Dimension Note", "Dimension" + System.Environment.NewLine + "Note", _path, "DimensionNotes.DimNotes");
            PushButton pbDimNote = GMSToolsribbonPanel.AddItem(DimNoteData) as PushButton;
            pbDimNote.ToolTip = "Add text below selected dimension.";
            BitmapImage DimNoteImage = Icon("dimnotes.png");
            pbDimNote.LargeImage = DimNoteImage;

            // "Build Number" button.
            PushButtonData BuildData = new PushButtonData("BuildNumber", "Build" + System.Environment.NewLine + "Number", thisAssemblyPath, "UnitBuildNumber.UnitBuildNumber");
            PushButton pbBuildNumber = GMSTagging.AddItem(BuildData) as PushButton;
            pbBuildNumber.ToolTip = "Tool to Update Unit Build Numbers";
            BitmapImage pbBuildImage = Icon("build.png");
            pbBuildNumber.LargeImage = pbBuildImage;

            // "Bunk Number" button.
            PushButtonData BunkData = new PushButtonData("BunkNumber", "Bunk" + System.Environment.NewLine + "Number", thisAssemblyPath, "UnitBunkNumber.UnitBunkNumber");
            PushButton pbBunkNumber = GMSTagging.AddItem(BunkData) as PushButton;
            pbBunkNumber.ToolTip = "Tool to Update Unit Bunk Numbers";
            BitmapImage pbBunkImage = Icon("bunk.png");
            pbBunkNumber.LargeImage = pbBunkImage;

            // "Level Number" button.
            PushButtonData LevelData = new PushButtonData("LevelNumber", "Level" + System.Environment.NewLine + "Number", thisAssemblyPath, "UnitLevelNumber.UnitLevelNumber");
            PushButton pbLevelNumber = GMSTagging.AddItem(LevelData) as PushButton;
            pbLevelNumber.ToolTip = "Tool to Update Unit Level Numbers";
            BitmapImage pbLevelImage = Icon("level.png");
            pbLevelNumber.LargeImage = pbLevelImage;

            // "Unit Release Date" button.
            PushButtonData UnitReleaseData = new PushButtonData("Unit Release Date", "Unit Release" + System.Environment.NewLine + "Date", _path, "UnitRelease.UnitRelease");
            PushButton pbUnitRelease = GMSUnitTools.AddItem(UnitReleaseData) as PushButton;
            pbUnitRelease.ToolTip = "Batch add released date to unit drawings";
            BitmapImage UnitReleaseImage = Icon("calendar.png");
            pbUnitRelease.LargeImage = UnitReleaseImage;

            // Help button hidden at user request (2026-06-30). The HelpMenu.Help command still exists;
            // to restore, uncomment this block (and re-add the "GMS"/Help keyboard shortcut if desired).
            //PushButtonData HelpData = new PushButtonData("Help", "Help", thisAssemblyPath, "HelpMenu.Help");
            //PushButton pbHelp = GMSSettings.AddItem(HelpData) as PushButton;
            //pbHelp.ToolTip = "GMS Standards and Help Information.";
            //BitmapImage pbHelpImage = Icon("help.png");
            //pbHelp.LargeImage = pbHelpImage;

            // "Collect Detail Items" button.
            PushButtonData CollectDiesData = new PushButtonData("Collect Detail Items", "Collect" + System.Environment.NewLine + "Detail Items", thisAssemblyPath, "CollectDiesForm.LaunchForm");
            PushButton cdSettings = GMSDetailItems.AddItem(CollectDiesData) as PushButton;
            cdSettings.ToolTip = "Collect detail items from selected sheets to a new drafting view";
            BitmapImage cdSettingsImage = Icon("collect.png");
            cdSettings.LargeImage = cdSettingsImage;

            // "ShowDetailItemPalette" id and "Detail Items" panel must match the DP keyboard-shortcut
            // CommandId registered in UpdateKeyboardShortcuts.updateXML().
            PushButtonData dipData = new PushButtonData("ShowDetailItemPalette", "Detail Item" + System.Environment.NewLine + "Palette", thisAssemblyPath, "GMS.Tools.DetailItemPalette.ShowPaletteCommand");
            PushButton pbDip = GMSDetailItems.AddItem(dipData) as PushButton;
            pbDip.ToolTip = "Show / focus the Detail Item Palette. Shortcut: DP";
            pbDip.LargeImage = Icon("palette_32.png");
            pbDip.Image = Icon("palette_16.png");

            // "Tagging Palette" button — dockable pane with per-category tag/component type
            // buttons, in three stacked panels (Detail Item Tags / Unit / Piece Tags / Generic Model
            // Tags). See TaggingPalette/.
            // Placed right after the Detail Item Palette button, same panel, by request.
            PushButtonData taggingPaletteData = new PushButtonData("ShowTaggingPalette", "Tagging" + System.Environment.NewLine + "Palette", thisAssemblyPath, "GMS.Tools.TaggingPalette.ShowPaletteCommand");
            PushButton pbTaggingPalette = GMSDetailItems.AddItem(taggingPaletteData) as PushButton;
            pbTaggingPalette.ToolTip = "Show / focus the Tagging Palette (Detail Item Tags, Unit / Piece Tags, Generic Model Tags).";
            pbTaggingPalette.LargeImage = Icon("tagpalette_32.png");
            pbTaggingPalette.Image = Icon("tagpalette_16.png");

            // "Cycle Worksets" button.
            PushButtonData cycleWorksetsData = new PushButtonData("Cycle Worksets", "Cycle" + System.Environment.NewLine + "Worksets", thisAssemblyPath, "CycleWorksets.LaunchForm");
            PushButton cwSettings = GMSToolsribbonPanel.AddItem(cycleWorksetsData) as PushButton;
            cwSettings.ToolTip = "Allow easy cycling of worksets in a 3D view.";
            BitmapImage cwSettingsImage = Icon("cycle.png");
            cwSettings.LargeImage = cwSettingsImage;

            // "Purge Family" button.
            PushButtonData purgeFamilyData = new PushButtonData("Purge Family", "Purge" + System.Environment.NewLine + "Family", thisAssemblyPath, "PurgeFamily.PurgeFamily");
            PushButton pfSettings = GMSToolsribbonPanel.AddItem(purgeFamilyData) as PushButton;
            pfSettings.ToolTip = "Purge Unused Elements in Family";
            BitmapImage pfSettingsImage = Icon("purgeFamily.png");
            pfSettings.LargeImage = pfSettingsImage;

            // Update generic-model framing families' weight + alloy/temper from the Extrusion DB.
            PushButtonData ufwData = new PushButtonData("UpdateFramingWeights", "Update" + System.Environment.NewLine + "Weights", thisAssemblyPath, "UpdateFramingWeights.UpdateWeights");
            PushButton pbUfw = GMSToolsribbonPanel.AddItem(ufwData) as PushButton;
            pbUfw.ToolTip = "Pull Weight + Alloy/Temper from the GMS Extrusion Database into each generic-model framing family type (by Die Number).";
            pbUfw.LargeImage = Icon("framingWeight.png");

            // End of the ribbon: Stock Length Optimizer (reads the "(DO NOT OPEN) Framing Stock
            // Lengths" schedule). "StockLengthOptimizer.LaunchForm" string is not compile-checked.
            PushButtonData sloData = new PushButtonData("StockLengthOptimizer", "Stock Length" + System.Environment.NewLine + "Optimizer", thisAssemblyPath, "StockLengthOptimizer.LaunchForm");
            PushButton pbSlo = GMSToolsribbonPanel.AddItem(sloData) as PushButton;
            pbSlo.ToolTip = "Optimize stock cut lengths from the Framing Stock Lengths schedule.";
            pbSlo.LargeImage = Icon("stockopt_32.png");
            pbSlo.Image = Icon("stockopt_16.png");

            // ---------------------------------------------------------------------------------------
            // DISABLED 2026-06-26: "Update Schedules" pulldown (Extrusions/Fasteners/Components/All)
            // removed from the GMS ribbon at user request. The UpdateSchedules.* command classes still
            // exist but are no longer reachable from the UI (and ExportParts no longer calls UpdateAll).
            // To re-enable, uncomment this block.
            // ---------------------------------------------------------------------------------------
            //PulldownButtonData updateSchedules = new PulldownButtonData("Update Schedules", "Update" + System.Environment.NewLine + "Schedules");
            //PulldownButton usSettings = GMSDetailItems.AddItem(updateSchedules) as PulldownButton;
            //usSettings.ToolTip = "Update Extrusion, Fastener and Component schedules with weights, alloys and images.";
            //BitmapImage usSettingsImage = Icon("updateSchedules.png");
            //usSettings.LargeImage = usSettingsImage;
            //PushButtonData usEx = new PushButtonData("Extrusions Updater", "Extrusions", thisAssemblyPath, "UpdateSchedules.UpdateExtrusions");
            //PushButtonData usFa = new PushButtonData("Fasteners Updater", "Fasteners", thisAssemblyPath, "UpdateSchedules.UpdateFasteners");
            //PushButtonData usCo = new PushButtonData("Components Updater", "Components", thisAssemblyPath, "UpdateSchedules.UpdateComponents");
            //PushButtonData usAll = new PushButtonData("All Updater", "All", thisAssemblyPath, "UpdateSchedules.UpdateAll");

            //RibbonItem extr = usSettings.AddPushButton(usEx);
            //RibbonItem fast = usSettings.AddPushButton(usFa);
            //RibbonItem comp = usSettings.AddPushButton(usCo);
            //RibbonItem all = usSettings.AddPushButton(usAll);
        }
    }
}


