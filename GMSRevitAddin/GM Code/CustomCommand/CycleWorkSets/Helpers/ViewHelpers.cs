using Autodesk.Revit.DB;
using Autodesk.Revit.UI;
using System.Linq;

namespace GMSRevitAddin.GMCode.CustomCommand.CycleWorkSets.Helpers
{
    // ================================================================================================
    // == ViewHelpers - view creation and preparation helpers
    // ================================================================================================
    /// <summary>
    /// Creates and configures the dedicated 3D view the workset cycler drives. Used only by
    /// <see cref="CycleWorksetsController.EnsureWorksetCycleView"/> when no existing per-user
    /// "Workset Cycle 3D View" is found.
    /// </summary>
    public static class ViewHelpers
    {
        // ============================================================================================
        // == CreateNew3DView
        // ============================================================================================
        /// <summary>Creates a new isometric 3D view named <paramref name="viewName"/>, using the
        /// document's default 3D <see cref="ViewFamilyType"/>. Caller is responsible for further
        /// preparing it (see <see cref="Prepare3DView"/>).</summary>
        public static View CreateNew3DView(Document doc, string viewName)
        {
            using (Transaction tr = new Transaction(doc, "Create Workset Cycle 3D View"))
            {
                tr.Start();

                ViewFamilyType vft = new FilteredElementCollector(doc)
                    .OfClass(typeof(ViewFamilyType))
                    .Cast<ViewFamilyType>()
                    .FirstOrDefault(v => v.ViewFamily == ViewFamily.ThreeDimensional);

                View3D view = View3D.CreateIsometric(doc, vft.Id);
                view.Name = viewName;

                tr.Commit();
                return view;
            }
        }

        // ============================================================================================
        // == Prepare3DView - template, graphics, categories, base points, zoom
        // ============================================================================================
        /// <summary>
        /// One-time setup for a freshly created workset-cycle 3D view: strips any view template,
        /// switches the graphics style to Consistent Colors, sets "View Set Group" to COORDINATION
        /// where present, turns on every category/subcategory that allows visibility control, hides
        /// the survey and project base points, zooms to fit, and makes the view active.
        /// </summary>
        public static void Prepare3DView(Document doc, View view, UIDocument uiDoc)
        {
            using (Transaction tr = new Transaction(doc, "Prepare Workset Cycle 3D View"))
            {
                tr.Start();

                // Remove template
                view.ViewTemplateId = ElementId.InvalidElementId;

                // Graphics style: Consistent Colors (7) — the MODEL_GRAPHICS_STYLE parameter takes a
                // raw integer enum value rather than a named Revit API constant.
                Parameter graphicsParam = view.get_Parameter(BuiltInParameter.MODEL_GRAPHICS_STYLE);
                if (graphicsParam != null && !graphicsParam.IsReadOnly)
                {
                    graphicsParam.Set(7);
                }

                // Set "View Set Group" to COORDINATION if present — a project-specific shared
                // parameter, so it's located by name rather than a BuiltInParameter.
                foreach (Parameter p in view.Parameters)
                {
                    if (p.Definition != null &&
                        p.Definition.Name == "View Set Group" &&
                        !p.IsReadOnly &&
                        p.AsString() != "COORDINATION")
                    {
                        p.Set("COORDINATION");
                    }
                }

                // Turn on all categories and subcategories where allowed — some categories throw when
                // queried/set for a given view type, so each is wrapped individually and ignored.
                foreach (Category c in doc.Settings.Categories)
                {
                    try
                    {
                        if (c.get_AllowsVisibilityControl(view))
                        {
                            c.set_Visible(view, true);
                        }

                        foreach (Category sc in c.SubCategories)
                        {
                            if (sc.get_AllowsVisibilityControl(view))
                            {
                                sc.set_Visible(view, true);
                            }
                        }
                    }
                    catch
                    {
                        // Ignore categories that throw
                    }
                }

                // Hide survey and project base points — these got turned on by the "all categories"
                // loop above, but clutter the coordination view, so hide them explicitly afterward.
                Category surveyPoint = Category.GetCategory(doc, BuiltInCategory.OST_SharedBasePoint);
                Category projectBasePoint = Category.GetCategory(doc, BuiltInCategory.OST_ProjectBasePoint);

                if (surveyPoint != null)
                {
                    surveyPoint.set_Visible(view, false);
                }
                if (projectBasePoint != null)
                {
                    projectBasePoint.set_Visible(view, false);
                }

                // Zoom to fit — requires the open UIView for this view, not just the View element.
                UIView uiView = null;
                var uiViews = uiDoc.GetOpenUIViews();
                foreach (UIView uv in uiViews)
                {
                    if (uv.ViewId == view.Id)
                    {
                        uiView = uv;
                        break;
                    }
                }

                if (uiView != null)
                {
                    uiView.ZoomToFit();
                }

                tr.Commit();
            }

            uiDoc.ActiveView = view;
        }
    }
}
