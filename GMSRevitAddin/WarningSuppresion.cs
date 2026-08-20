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
using Autodesk.Revit.UI.Events;
using Autodesk.Revit.ApplicationServices;
using Autodesk.Revit.DB.Visual;


namespace RevitWarningSuppresion
{
    /// <summary>
    /// Ribbon command that turns OFF the auto-dismiss behavior registered by
    /// <see cref="registerWarningSuppresion"/> (persists the setting and clears the in-memory flag
    /// via <see cref="registerWarningSuppresion.updatedbool"/>). The event handler itself stays
    /// subscribed for the life of the session; it just no-ops once <c>enabled</c> is false.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class disableregisterWarningSuppresion : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            RevitWarningSuppresion.registerWarningSuppresion.updatedbool(false);
            return Result.Succeeded;

        }
    }

    /// <summary>
    /// Ribbon command / startup hook that auto-dismisses a specific recurring Revit TaskDialog (the
    /// "This change will be applied to all elements of type" prompt) by subscribing to
    /// <see cref="UIApplication.DialogBoxShowing"/> and overriding the result to OK, so routine
    /// operations aren't interrupted by that confirmation every time. The subscription is installed
    /// once (from <see cref="RevitStartup"/> on application init, or when this command is run
    /// manually) and left in place; the static <see cref="enabled"/> flag — persisted to
    /// <c>Settings.Default.AutoWarningSuppress</c> — gates whether the handler actually acts.
    /// </summary>
    [Transaction(TransactionMode.Manual)]
    [Regeneration(RegenerationOption.Manual)]
    public class registerWarningSuppresion : IExternalCommand
    {
        public static bool enabled = false;

        /// <summary>Sets the in-memory suppression flag and persists it to user settings.</summary>
        public static void updatedbool(bool en)
        {
            enabled = en;
            GMSRevitAddin.Properties.Settings.Default.AutoWarningSuppress = RevitWarningSuppresion.registerWarningSuppresion.enabled;
            GMSRevitAddin.Properties.Settings.Default.Save();
        }

        // Ribbon-invoked entry point: turns suppression on, then delegates to the UIApplication
        // overload below to (re)wire the DialogBoxShowing subscription.
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            UIApplication uiApp = commandData.Application;
            Autodesk.Revit.DB.Document doc = uiApp.ActiveUIDocument.Document;
            Application app = doc.Application;
            UIApplication uiapp = new UIApplication(app);

            updatedbool(true);

            return Execute(commandData.Application);
        }

        /// <summary>
        /// Subscribes <see cref="dismissTaskDialog"/> to <c>DialogBoxShowing</c> when suppression is
        /// enabled. Called both from the ribbon command and from <see cref="RevitStartup"/>'s
        /// one-shot application-initialized hook.
        /// </summary>
        public Result Execute(UIApplication uiapp)
        {
            if (enabled)
            {
                uiapp.DialogBoxShowing += new EventHandler<Autodesk.Revit.UI.Events.DialogBoxShowingEventArgs>(dismissTaskDialog);
            }

            return Result.Succeeded;
        }

        // DialogBoxShowing fires for every Revit dialog, not just TaskDialogs, so the args must be
        // downcast and null-checked before use.
        private void dismissTaskDialog(object sender, DialogBoxShowingEventArgs args)
        {
            if (enabled)
            {
                TaskDialogShowingEventArgs e = args as TaskDialogShowingEventArgs;
                if (e == null)
                    return;
                // Match only the specific recurring confirmation prompt by its message text —
                // other TaskDialogs are left alone and show normally.
                if (e.Message.StartsWith("This change will be applied to all elements of type"))
                {
                    e.OverrideResult((int)TaskDialogCommonButtons.Ok);
                }
            }
        }
    }

    /// <summary>
    /// <see cref="IExternalApplication"/> that wires up warning suppression at Revit startup, so the
    /// auto-dismiss behavior (if previously enabled via <see cref="registerWarningSuppresion"/>) is
    /// active from the start of the session rather than only after the ribbon command is run.
    /// </summary>
    public class RevitStartup : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            application.ControlledApplication.ApplicationInitialized += ControlledApplication_ApplicationInitialized;
            return Result.Succeeded;

        }

        // Runs once the Revit application has finished initializing; a UIApplication isn't
        // constructible any earlier, so the subscription is deferred to this event.
        public static void ControlledApplication_ApplicationInitialized(object sender, Autodesk.Revit.DB.Events.ApplicationInitializedEventArgs e)
        {
            var command = new registerWarningSuppresion();

            command.Execute(new UIApplication(sender as Application));
        }
    }
}


