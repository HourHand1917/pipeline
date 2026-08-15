using Godot;

/// <summary>
/// 工作台交互物。点击后隐藏 HUD 并打开 WorkbenchUI。
/// </summary>
[GlobalClass]
public partial class WorkbenchInteractable : InteractableBase
{
    [Export] public WorkbenchUI WorkbenchUI { get; set; }
    [Export] public ExplorationHUD Hud { get; set; }

    public override void _Ready()
    {
        base._Ready();
        if (WorkbenchUI != null)
            WorkbenchUI.Closed += () => Hud?.ShowHUD();
    }

    public override void HandleInteract()
    {
        Hud?.HideHUD();
        WorkbenchUI?.Open();
    }
}
