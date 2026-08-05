using Godot;

/// <summary>
/// 商店交互物。点击后打开 ShopUI。
/// </summary>
[GlobalClass]
public partial class StoreInteractable : InteractableBase
{
    [Export] public ShopUI ShopUI { get; set; }

    public override void HandleInteract()
    {
        ShopUI?.Open();
    }
}
