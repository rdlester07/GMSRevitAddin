#nullable enable
using System.Collections.Generic;
using System.ComponentModel;
using Autodesk.Revit.DB;
using Autodesk.Revit.UI;

namespace GMS.Tools.TaggingPalette
{
    /// <summary>One family, grouping the button for each of its types (a family with a single type
    /// still gets its own group, with one button).</summary>
    public sealed class TagFamilyGroup
    {
        public string FamilyName { get; set; } = string.Empty;
        public List<TagTypeButton> Types { get; set; } = new();
    }

    /// <summary>One clickable type button: which family symbol it activates, which native
    /// Revit placement command applies to its category (see
    /// <see cref="TaggingPaletteModule.ResolveCommand"/>), and whether it's currently enabled —
    /// disabled when it targets a real model category but the active view is a Drafting View,
    /// which never shows model geometry (see <see cref="TaggingPaletteModule.UpdateEnablement"/>).
    /// <see cref="IsEnabled"/>/<see cref="DisabledReason"/> raise <see cref="PropertyChanged"/> so
    /// the pane's bound buttons grey out live as the user switches views, without needing to
    /// rebuild/reassign the bound list.</summary>
    public sealed class TagTypeButton : INotifyPropertyChanged
    {
        public string TypeName { get; set; } = string.Empty;
        public ElementId TypeId { get; set; } = ElementId.InvalidElementId;
        public ElementId CategoryId { get; set; } = ElementId.InvalidElementId;
        public PostableCommand TargetCommand { get; set; } = PostableCommand.TagByCategory;

        /// <summary>True for a type that tags/places a genuine 3D model element (Generic Models,
        /// Curtain Wall Panels) rather than a view-specific annotation (Detail Items, "GAIT -
        /// Piece Tag") — set once at scan time in <see cref="TaggingPaletteModule"/>.</summary>
        public bool RequiresModelView { get; set; }

        private bool _isEnabled = true;
        public bool IsEnabled
        {
            get => _isEnabled;
            set
            {
                if (_isEnabled == value) return;
                _isEnabled = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsEnabled)));
            }
        }

        private string? _disabledReason;
        /// <summary>Tooltip text while disabled; null while enabled (a null/empty WPF ToolTip
        /// simply shows nothing).</summary>
        public string? DisabledReason
        {
            get => _disabledReason;
            set
            {
                if (_disabledReason == value) return;
                _disabledReason = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(DisabledReason)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }
}
