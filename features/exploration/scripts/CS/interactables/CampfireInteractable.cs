using Godot;

/// <summary>
/// 篝火/休息回复状态（当前仅输出日志）。
/// </summary>
public partial class CampfireInteractable : InteractableBase
{
    public override void HandleInteract()
    {
        GD.Print($"与「{DisplayName}」互动 —— 休息回复（待实现）。");
    }

    protected override void SetupPlaceholder()
    {
        sprite.Modulate = new Color(1.0f, 0.5f, 0.1f); // 橙色
    }
}
