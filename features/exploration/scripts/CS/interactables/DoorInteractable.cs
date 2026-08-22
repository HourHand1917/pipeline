using Godot;

/// <summary>
/// 门交互物：点击后门消失，并触发目标 NPC 的对话。
/// 消失状态通过 PersistInteraction 持久化（下次进图门不再出现）。
/// </summary>
[GlobalClass]
public partial class DoorInteractable : InteractableBase
{
    /// <summary>点击门后要触发对话的目标 NPC。</summary>
    [Export] public NPCBase TargetNpc { get; set; }

    public override void HandleInteract()
    {
        // 门消失并禁用互动
        SetBlinkEnabled(false);
        if (sprite != null) sprite.Visible = false;
        Monitoring = false;
        if (clickZone != null)
        {
            clickZone.Monitoring = false;
            clickZone.Monitorable = false;
        }

        // 持久化「已开过」，LoadState 会 QueueFree 掉这扇门
        PersistInteraction(_mapId);

        // 触发目标 NPC 的对话
        TargetNpc?.HandleInteract();
    }
}
