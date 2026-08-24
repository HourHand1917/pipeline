using Godot;

/// <summary>
/// 回血机器：靠近闪烁，点击互动后把玩家血量回满。可重复使用，不持久化。
/// </summary>
[GlobalClass]
public partial class HealMachineInteractable : InteractableBase
{
    public override void HandleInteract()
    {
        DataManager.Instance?.FullHeal();
        GD.Print($"「{DisplayName}」：已回满血。");
    }
}
