#nullable enable
using System.Collections.Generic;
using System.Linq;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace GMS.Tools.DetailItemPalette
{
    public class NavigateToSheetHandler : IExternalEventHandler
    {
        public ElementId? TargetSheetId { get; set; }
        public ElementId? TargetSymbolId { get; set; }
        public bool IsGroup { get; set; }

        public void Execute(UIApplication uiApp)
        {
            if (TargetSheetId == null) return;

            var uiDoc = uiApp.ActiveUIDocument;
            if (uiDoc == null) return;

            var doc = uiDoc.Document;
            var sheet = doc.GetElement(TargetSheetId) as ViewSheet;
            if (sheet == null) return;

            uiDoc.ActiveView = sheet;

            if (TargetSymbolId == null) return;

            var instanceIds = new List<ElementId>();
            foreach (var vpId in sheet.GetAllViewports())
            {
                if (doc.GetElement(vpId) is not Viewport vp) continue;

                if (IsGroup)
                {
                    instanceIds.AddRange(
                        new FilteredElementCollector(doc, vp.ViewId)
                            .OfClass(typeof(Group))
                            .Cast<Group>()
                            .Where(g => g.GroupType?.Id.Value == TargetSymbolId.Value)
                            .Select(g => g.Id));
                }
                else
                {
                    instanceIds.AddRange(
                        new FilteredElementCollector(doc, vp.ViewId)
                            .OfCategory(BuiltInCategory.OST_DetailComponents)
                            .OfClass(typeof(FamilyInstance))
                            .Cast<FamilyInstance>()
                            .Where(fi => fi.Symbol?.Id.Value == TargetSymbolId.Value)
                            .Select(fi => fi.Id));
                }
            }

            if (instanceIds.Count > 0)
                uiDoc.Selection.SetElementIds(instanceIds);
        }

        public string GetName() => "NavigateToSheet";
    }
}
