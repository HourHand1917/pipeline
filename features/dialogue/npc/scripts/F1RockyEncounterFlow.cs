using Godot;

/// <summary>
/// Controls only the presentation of the F1 Rocky encounter. Before victory,
/// Mousy is visible but cannot be clicked; Rocky owns the automatic trigger.
/// After victory, Rocky is replaced by his final death frame and Mousy becomes
/// interactive. Leaving the map naturally removes both scene-local actors.
/// </summary>
[GlobalClass]
public partial class F1RockyEncounterFlow : Node
{
    [Export] public Node2D RockyPrebattle { get; set; }
    [Export] public Area2D MousyPostbattle { get; set; }
    [Export] public CanvasItem DeadRocky { get; set; }
    [Export] public string MapId { get; set; } = "f1_2";
    [Export] public string RockyPersistenceId { get; set; } = "enemy_rocky_dialogue";

    public override void _Ready() => Callable.From(RefreshState).CallDeferred();

    public void RefreshState()
    {
        var state = GameState.Instance?.GetObjectState(MapId, RockyPersistenceId);
        bool defeated = state != null
            && state.TryGetValue("defeated", out var value)
            && value.AsBool();

        if (RockyPrebattle != null) RockyPrebattle.Visible = !defeated;
        if (DeadRocky != null) DeadRocky.Visible = defeated;
        SetMousyInteractive(defeated);
    }

    private void SetMousyInteractive(bool enabled)
    {
        if (MousyPostbattle == null) return;
        MousyPostbattle.SetProcess(enabled);
        MousyPostbattle.SetPhysicsProcess(enabled);
        MousyPostbattle.Monitoring = enabled;
        MousyPostbattle.Monitorable = enabled;

        var detection = MousyPostbattle.GetNodeOrNull<CollisionShape2D>("DetectionRange");
        if (detection != null) detection.SetDeferred(CollisionShape2D.PropertyName.Disabled, !enabled);
        var clickZone = MousyPostbattle.GetNodeOrNull<Area2D>("ClickZone");
        if (clickZone != null)
        {
            clickZone.InputPickable = enabled;
            clickZone.Monitoring = enabled;
            clickZone.Monitorable = enabled;
        }
        var blink = MousyPostbattle.GetNodeOrNull<BlinkComponent>("Blink");
        if (blink != null) blink.Enabled = enabled;
    }
}
