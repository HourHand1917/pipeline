using Godot;

/// <summary>
/// 门/传送点。点击后触发房间过渡。
/// TargetRoomId：要切换到的房间节点名（如 "Room_Back"）。
/// </summary>
[GlobalClass]
public partial class RoomGateInteractable : InteractableBase
{
    [Export] public string TargetRoomId { get; set; } = "Room_Back";
    [Export] public RoomTransitionManager Manager { get; set; }

    public override void HandleInteract()
    {
        if (Manager != null)
        {
            GD.Print($"→ 进入：{TargetRoomId}");
            Manager.TransitionTo(TargetRoomId);
        }
        else
        {
            GD.PrintErr($"RoomGateInteractable: 未设置 RoomTransitionManager");
        }
    }
}
