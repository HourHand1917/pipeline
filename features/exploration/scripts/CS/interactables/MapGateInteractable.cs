using Godot;

/// <summary>
/// 跨场景门/传送点。点击后切到另一个地图场景。
/// TargetMapId：目标地图 id（对应 MapData.id）
/// TargetSpawnId：在目标地图哪个生成点出现
/// </summary>
[GlobalClass]
public partial class MapGateInteractable : InteractableBase
{
    [Export] public StringName TargetMapId { get; set; } = "";
    [Export] public StringName TargetSpawnId { get; set; } = "";

    public override void HandleInteract()
    {
        if (string.IsNullOrEmpty(TargetMapId))
        {
            GD.PrintErr("MapGateInteractable: 未设置 TargetMapId");
            return;
        }
        MapManager.Instance?.TravelTo(TargetMapId, TargetSpawnId);
    }
}
