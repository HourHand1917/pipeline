using Godot;

/// <summary>
/// 敌对 NPC：靠近自动触发战斗（当前仅输出日志）。
/// 不需要点击互动，靠近即触发。
/// </summary>
public partial class HostileNPCInteractable : InteractableBase
{
    /// <summary>自动触发战斗的距离阈值</summary>
    [Export] public float AggroRadius { get; set; } = 80.0f;

    private bool hasTriggered;

    public override void _Ready()
    {
        base._Ready();
        sprite.Modulate = new Color(0.85f, 0.15f, 0.15f); // 红色

        // 缩小互动半径等于仇恨半径
        if (rangeShape.Shape is CircleShape2D circle)
        {
            circle.Radius = AggroRadius;
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (hasTriggered || !IsPlayerInRange)
            return;

        hasTriggered = true;
        GD.Print($"遭遇「{DisplayName}」！进入战斗（待实现）。");
    }

    public override void HandleInteract()
    {
        // 敌对 NPC 不支持点击互动，靠近即触发
    }

    /// <summary>
    /// 重置触发状态（离开范围后）
    /// </summary>
    public void ResetTrigger()
    {
        hasTriggered = false;
    }
}
