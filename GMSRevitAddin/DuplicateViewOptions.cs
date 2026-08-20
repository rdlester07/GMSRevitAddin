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
using System.Windows.Forms;
using View = Autodesk.Revit.DB.View;
using Autodesk.Revit.DB.Events;

namespace DuplicateViewOptions
{
    /// <summary>
    /// Shared static state for the duplicate-view auto-open feature: the history of activated
    /// views, whether a new drafting view was just created, and the cached <see cref="UIApplication"/>/
    /// <see cref="UIDocument"/> used by the one-shot Idling handler in <see cref="IdleEvent"/>.
    /// </summary>
    public class Views
    {
        public static List<View> lastView = new List<View>();
        public static bool viewCreated = false;
        public static View NewView = null;
        public static UIApplication uiapp = null;
        public static UIDocument uidoc = null;
    }

    /// <summary>
    /// One-shot Idling handler that runs once a duplicated drafting view has actually become the
    /// active view. Revit view-change/close requests can't be made from inside the ViewActivated
    /// event itself, so the work is deferred to the next Idling tick (subscribed in
    /// <see cref="RevitStartup.ControlledApplication_ViewActivated"/>).
    /// </summary>
    public class IdleEvent
    {
        /// <summary>
        /// Runs once per duplicated view: unsubscribes itself, then—based on the user's
        /// "Open Original"/"Open New" settings—requests the view to switch back to (or stay on)
        /// and closes whichever of the original/new view's UI tabs should not remain open.
        /// </summary>
        public static void OnIdling(object sender, IdlingEventArgs e)
        {
            // One-shot: detach immediately so this only runs once per duplicated view.
            Views.uiapp.Idling -= new EventHandler<IdlingEventArgs>(IdleEvent.OnIdling);

            Document doc = Views.uidoc.Document;
            View active = Views.uidoc.ActiveView;
            IList<UIView> uiviews = Views.uidoc.GetOpenUIViews();
            View toView = null;
            UIView UIOrigView = null;
            UIView UInewView = null;

            if (Views.NewView != null && Views.lastView.Count() > 2)
            {
                toView = Views.lastView[Views.lastView.Count() - 3];

                if (GMSRevitAddin.Properties.Settings.Default.OpenOrig == false && GMSRevitAddin.Properties.Settings.Default.OpenNew == false)
                {
                    if (toView != null)
                    {
                        Views.uidoc.RequestViewChange(toView);
                    }
                }
                if (GMSRevitAddin.Properties.Settings.Default.OpenOrig == false && GMSRevitAddin.Properties.Settings.Default.OpenNew == true)
                {
                    Views.uidoc.RequestViewChange(Views.NewView);
                }
                if (GMSRevitAddin.Properties.Settings.Default.OpenOrig == true && GMSRevitAddin.Properties.Settings.Default.OpenNew == false)
                {
                    Views.uidoc.RequestViewChange(Views.lastView[Views.lastView.Count - 2]);
                }

                foreach (UIView uv in uiviews)
                {
                    ElementId uvId = uv.ViewId;
                    if (uvId == Views.lastView[Views.lastView.Count - 2].Id)
                    {
                        UIOrigView = uv;
                    }
                    if (uvId == Views.lastView.Last().Id)
                    {
                        UInewView = uv;
                    }
                    if (null != UIOrigView && null != UInewView)
                    {
                        break;
                    }
                }
                if (GMSRevitAddin.Properties.Settings.Default.OpenOrig == false)
                {
                    UIOrigView.Close();
                }
                if (GMSRevitAddin.Properties.Settings.Default.OpenNew == false && UInewView != null)
                {
                    UInewView.Close();
                }
            }
            Views.NewView = null;
            Views.viewCreated = false;
        }
    }

    /// <summary>
    /// Handles auto-opening the new/original view when a view is duplicated via
    /// <c>ViewDuplicateOption.Duplicate</c> et al. Registers two Revit application-level event
    /// handlers (wired from <c>GMS_tools.OnStartup</c>): <see cref="ControlledApplication_DocumentChanged"/>
    /// detects a newly-created drafting view, and <see cref="ControlledApplication_ViewActivated"/>
    /// arms the one-shot Idling handler (<see cref="IdleEvent"/>) once that view actually becomes active,
    /// which then applies the user's "Open Original"/"Open New" preferences from
    /// <c>Properties.Settings.Default</c>.
    /// </summary>
    public class RevitStartup : IExternalApplication
    {
        public Result OnShutdown(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        public Result OnStartup(UIControlledApplication application)
        {
            return Result.Succeeded;
        }

        /// <summary>
        /// Tracks every view activation. If the most recently activated view matches the drafting
        /// view flagged by <see cref="ControlledApplication_DocumentChanged"/> (i.e. the duplicate
        /// just became active), arms the one-shot Idling handler that applies the open/close logic.
        /// </summary>
        public static void ControlledApplication_ViewActivated(object sender, ViewActivatedEventArgs e)
        {
            Views.lastView.Add(e.CurrentActiveView);
            if (Views.viewCreated)
            {
                if (Views.NewView.Id == Views.lastView.Last().Id && Views.NewView.Name.Contains(Views.lastView[Views.lastView.Count - 2].Name))
                {
                    // Arm the one-shot Idling handler now that the duplicated view is confirmed active.
                    Views.uiapp.Idling += new EventHandler<IdlingEventArgs>(IdleEvent.OnIdling);
                }
            }
        }
        /// <summary>
        /// Watches for a newly-added drafting view (Revit's signal that a view was just duplicated)
        /// unless both "Open New" and "Open Original" are enabled (in which case nothing needs closing,
        /// so tracking is skipped). Caches the flagged view for <see cref="ControlledApplication_ViewActivated"/>
        /// to confirm.
        /// </summary>
        public static void ControlledApplication_DocumentChanged(object sender, Autodesk.Revit.DB.Events.DocumentChangedEventArgs e)
        {
            if (GMSRevitAddin.Properties.Settings.Default.OpenNew == false || GMSRevitAddin.Properties.Settings.Default.OpenOrig == false)
            {
                Document doc = e.GetDocument();

                // A new drafting view among the added elements is how a "duplicate view" is detected here.
                FilteredElementCollector collector = new FilteredElementCollector(doc, e.GetAddedElementIds()).OfClass(typeof(View));
                List<View> draftingviews = collector.Cast<View>().Where(sh => sh.ViewType == ViewType.DraftingView).ToList();

                if (draftingviews.Count > 0)
                {
                    Views.uiapp = new UIApplication(sender as Autodesk.Revit.ApplicationServices.Application);
                    Views.uidoc = Views.uiapp.ActiveUIDocument;

                    foreach (View v in draftingviews)
                    {
                        if (v != null && v.Id != ElementId.InvalidElementId)
                        {
                            Views.viewCreated = true;
                            Views.NewView = v;
                        }
                    }
                }
                else
                {
                    Views.viewCreated = false;
                }
            }
        }
    }
}