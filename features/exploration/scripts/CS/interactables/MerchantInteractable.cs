using Godot;

/// <summary>
/// 商人：购买/出售物品（当前仅输出日志）。
/// </summary>
public partial class MerchantInteractable : InteractableBase
{
    public override void HandleInteract()
    {
        GD.Print($"与「{DisplayName}」互动 —— 打开商店界面（待实现）。");
    }

    protected override void SetupPlaceholder()
    {
        sprite.Modulate = new Color(1.0f, 0.85f, 0.2f); // 金色
    }
}
