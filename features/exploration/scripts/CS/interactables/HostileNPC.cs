using Godot;

/// <summary>
/// 敌对 NPC。玩家足够靠近时自动触发战斗。
/// </summary>
[GlobalClass]
public partial class HostileNPC : NPCBase
{
    /// <summary>自动触发战斗的距离阈值</summary>
    [Export] public float AggroRadius { get; set; } = 80.0f;

    private bool hasTriggered;

    public override void _Ready()
    {
        base._Ready();
        if (detectionShape.Shape is CircleShape2D circle)
            circle.Radius = AggroRadius;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (hasTriggered || !IsPlayerInRange)
            return;

        hasTriggered = true;
        GD.Print($"遭遇「{NpcName}」！进入战斗（待实现）。");
    }

    public override void HandleInteract()
    {
        // 敌对 NPC 不响应点击，靠近即触发
    }
}
