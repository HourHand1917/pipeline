using Godot;
using Godot.Collections;

/// <summary>
/// Inspector-editable content for the home destination selector.
/// The list is intentionally flat: the home gate is a shortcut menu, not a route tree.
/// </summary>
[GlobalClass]
public partial class MapSelectionConfig : Resource
{
    [ExportGroup("Copy")]
    [Export] public string Title { get; set; } = "选择下一站";
    [Export] public string Subtitle { get; set; } = "确认目的地后，大门将把你送往对应入口。";
    [Export] public string ConfirmText { get; set; } = "确认出发";
    [Export] public string CloseText { get; set; } = "返回";

    [ExportGroup("Validation")]
    [Export(PropertyHint.File, "*.tres")]
    public string MapRegistryPath { get; set; }
        = "res://features/exploration/resources/map_registry.tres";

    [ExportGroup("Destinations")]
    [Export] public Array<MapDestinationData> Destinations { get; set; } = new();
}
