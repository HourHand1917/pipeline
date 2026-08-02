using Godot;

/// <summary>
/// 工作台：用于合成/升级物品（当前仅输出日志）。
/// </summary>
public partial class WorkbenchInteractable : InteractableBase
{
    public override void HandleInteract()
    {
        GD.Print($"与「{DisplayName}」互动 —— 打开合成界面（待实现）。");
    }

}
