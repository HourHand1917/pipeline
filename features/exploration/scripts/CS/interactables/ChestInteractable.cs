using Godot;

/// <summary>
/// 宝箱：打开获取物品（当前仅输出日志）。
/// </summary>
public partial class ChestInteractable : InteractableBase
{
    public override void HandleInteract()
    {
        GD.Print($"与「{DisplayName}」互动 —— 打开宝箱（待实现）。");
    }

    protected override void SetupPlaceholder()
    {
        sprite.Modulate = new Color(0.2f, 0.45f, 0.8f); // 蓝色
    }
}
