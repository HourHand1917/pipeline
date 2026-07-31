using Godot;

/// <summary>
/// NPC 基类。在可互动基础上添加对话、任务等通用 NPC 功能。
/// </summary>
[GlobalClass]
public abstract partial class NPCBase : InteractableBase
{
    /// <summary>默认对话文本</summary>
    [Export] public string DefaultDialog { get; set; } = "";

    /// <summary>NPC 名字标签</summary>
    [Export] public string NpcName { get; set; } = "NPC";

    public override void HandleInteract()
    {
        if (!string.IsNullOrEmpty(DefaultDialog))
            GD.Print($"[{NpcName}] {DefaultDialog}");
        else
            base.HandleInteract();
    }
}
