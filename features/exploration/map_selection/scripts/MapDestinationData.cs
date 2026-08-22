using Godot;

/// <summary>
/// One selectable destination shown by the home gate map selector.
/// Designers only edit this resource; the selector never hard-codes route text.
/// </summary>
[GlobalClass]
public partial class MapDestinationData : Resource
{
    public enum DestinationCategory
    {
        Exploration,
        Supplies,
        Event,
        Challenge,
        Boss,
    }

    public enum DestinationRisk
    {
        Low,
        Medium,
        High,
        Extreme,
    }

    [ExportGroup("Display")]
    [Export] public string Title { get; set; } = "Unnamed destination";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "";
    [Export] public DestinationCategory Category { get; set; } = DestinationCategory.Exploration;
    [Export] public string RewardHint { get; set; } = "Unknown reward";
    [Export] public DestinationRisk Risk { get; set; } = DestinationRisk.Medium;

    [ExportGroup("Travel")]
    [Export] public StringName MapId { get; set; } = "";
    [Export] public StringName SpawnId { get; set; } = "";

    [ExportGroup("Lock")]
    [Export] public bool Unlocked { get; set; } = true;
    [Export] public string LockedHint { get; set; } = "Locked";

    public string CategoryText => Category switch
    {
        DestinationCategory.Exploration => "探索",
        DestinationCategory.Supplies => "补给",
        DestinationCategory.Event => "事件",
        DestinationCategory.Challenge => "挑战",
        DestinationCategory.Boss => "首领",
        _ => "未知",
    };

    public string RiskText => Risk switch
    {
        DestinationRisk.Low => "低",
        DestinationRisk.Medium => "中",
        DestinationRisk.High => "高",
        DestinationRisk.Extreme => "极高",
        _ => "未知",
    };
}
