using Godot;

/// <summary>
/// 地图边缘触发区：玩家走到地图边缘即无缝切到目标地图。
/// 与 MapGateInteractable（点击门）对称，但用 BodyEntered 检测「走进」而非「点击」。
///
/// 用法：
///   1. 在地图边缘放一个 Area2D，挂此脚本；
///   2. 加一个 CollisionShape2D 覆盖边缘（竖条矩形即可）；
///   3. 设 TargetMapId / TargetSpawnId（目标地图 id + 该地图的生成点）。
/// 注意：目标地图的出生点要放在地图内部、别落在它自己的边缘触发区里，否则会立即切回。
/// </summary>
[GlobalClass]
public partial class MapEdgeTrigger : Area2D
{
    [Export] public StringName TargetMapId { get; set; } = "";
    [Export] public StringName TargetSpawnId { get; set; } = "";

    /// <summary>场景加载后忽略触发的秒数，防止出生点落在边缘导致立即切回。</summary>
    [Export] public float GraceSeconds { get; set; } = 0.0f;

    private ulong _readyMs;

    public override void _Ready()
    {
        _readyMs = Time.GetTicksMsec();
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node2D body)
    {
        if (body is not PlayerController) return;
        if (Time.GetTicksMsec() - _readyMs < (ulong)(GraceSeconds * 1000)) return;

        if (string.IsNullOrEmpty(TargetMapId))
        {
            GD.PrintErr($"MapEdgeTrigger「{Name}」: 未设置 TargetMapId");
            return;
        }

        MapManager.Instance?.TravelTo(TargetMapId, TargetSpawnId);
    }
}
