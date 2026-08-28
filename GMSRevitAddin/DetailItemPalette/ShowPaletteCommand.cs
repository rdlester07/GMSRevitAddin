using Autodesk.Revit.Attributes;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace GMS.Tools.DetailItemPalette
{
    [Transaction(TransactionMode.ReadOnly)]
    public class ShowPaletteCommand : IExternalCommand
    {
        public Result Execute(ExternalCommandData commandData, ref string message, ElementSet elements)
        {
            var uiApp = commandData.Application;

            PaletteModule.EnsureEvents(uiApp);
            PaletteModule.SubscribeSelectionChanged(uiApp);
            // Stop the force-hide Idling/DocumentOpened handlers from closing the pane again —
            // the user just asked for it explicitly.
            PaletteModule.MarkUserRequestedShow();

            var pane = uiApp.GetDockablePane(PaletteModule.PaneId);
            pane.Show();
            PaletteModule.RefreshTheme();

            return Result.Succeeded;
        }
    }
}
