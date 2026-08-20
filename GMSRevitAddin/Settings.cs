namespace GMSRevitAddin.Properties {


    // This class allows you to handle specific events on the settings class:
    //  The SettingChanging event is raised before a setting's value is changed.
    //  The PropertyChanged event is raised after a setting's value is changed.
    //  The SettingsLoaded event is raised after the setting values are loaded.
    //  The SettingsSaving event is raised before the setting values are saved.
    /// <summary>
    /// Partial companion to the auto-generated <c>Settings.Designer.cs</c> (backed by
    /// <c>app.config</c>/<c>Settings.settings</c>). Holds WinForms/UI-level persisted state, e.g.
    /// <c>AutoWarningSuppress</c> read/written by <see cref="RevitWarningSuppresion.registerWarningSuppresion"/>.
    /// This half of the partial class exists only to optionally wire the event handlers below; the
    /// generated half declares the actual settings properties.
    /// </summary>
    internal sealed partial class Settings {

        public Settings() {
            // // To add event handlers for saving and changing settings, uncomment the lines below:
            //
            // this.SettingChanging += this.SettingChangingEventHandler;
            //
            // this.SettingsSaving += this.SettingsSavingEventHandler;
            //
        }
        
        private void SettingChangingEventHandler(object sender, System.Configuration.SettingChangingEventArgs e) {
            // Add code to handle the SettingChangingEvent event here.
        }
        
        private void SettingsSavingEventHandler(object sender, System.ComponentModel.CancelEventArgs e) {
            // Add code to handle the SettingsSaving event here.
        }
    }
}
