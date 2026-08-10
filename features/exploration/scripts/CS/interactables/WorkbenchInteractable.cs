using Godot;

/// <summary>
/// 工作台交互物。点击后打开 WorkbenchUI。
/// </summary>
[GlobalClass]
public partial class WorkbenchInteractable : InteractableBase
{
    [Export] public WorkbenchUI WorkbenchUI { get; set; }

    public override void HandleInteract()
    {
        WorkbenchUI?.Open();
    }
}
