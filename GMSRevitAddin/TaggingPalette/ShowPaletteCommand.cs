using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace GMS.Tools.TaggingPalette
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ShowPaletteCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;

            TaggingPaletteModule.EnsureEvents(uiApp);

            var pane = uiApp.GetDockablePane(TaggingPaletteModule.PaneId);
            pane.Show();
            TaggingPaletteModule.RefreshTheme();
            TaggingPaletteModule.Refresh(uiApp);

            return Result.Succeeded;
        }
    }
}
