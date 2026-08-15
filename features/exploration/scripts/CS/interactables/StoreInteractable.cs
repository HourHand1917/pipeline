using Godot;

/// <summary>
/// 商店交互物。点击后隐藏 HUD 并打开 ShopUI。
/// </summary>
[GlobalClass]
public partial class StoreInteractable : InteractableBase
{
    [Export] public ShopUI ShopUI { get; set; }
    [Export] public ExplorationHUD Hud { get; set; }

    public override void _Ready()
    {
        base._Ready();
        if (ShopUI != null)
            ShopUI.Closed += () => Hud?.ShowHUD();
    }

    public override void HandleInteract()
    {
        Hud?.HideHUD();
        ShopUI?.Open();
    }
}
