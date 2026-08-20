using Autodesk.Revit.DB;

namespace GMS.Tools.DetailItemPalette
{
    public class SheetInfo
    {
        public ElementId SheetId { get; set; } = ElementId.InvalidElementId;
        public string SheetNumber { get; set; } = string.Empty;
        public string SheetName { get; set; } = string.Empty;
        public int InstanceCount { get; set; }
        public string DisplayName => $"({InstanceCount}) {SheetNumber} - {SheetName}";
    }
}
