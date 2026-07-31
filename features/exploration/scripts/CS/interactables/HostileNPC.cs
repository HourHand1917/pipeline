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

    protected override void SetupPlaceholder()
    {
        sprite.Modulate = new Color(0.85f, 0.15f, 0.15f); // 红色
    }

    public override void _Ready()
    {
        base._Ready();
        // 缩小检测范围等于仇恨半径
        DetectionRadius = AggroRadius;
        if (detectionShape.Shape is CircleShape2D circle)
            circle.Radius = DetectionRadius;
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
