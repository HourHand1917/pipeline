using Godot;

/// <summary>
/// 房间边界数据。挂到 Room_* 节点上。
/// 进入房间时由 RoomTransitionManager 推给 ExplorationManager。
/// </summary>
[GlobalClass]
public partial class RoomBounds : Node
{
    [Export] public int PlayerLeft { get; set; }
    [Export] public int PlayerRight { get; set; } = 3500;
    [Export] public int CamLeft { get; set; }
    [Export] public int CamRight { get; set; } = 3600;

    /// <summary>推边界给 Manager。无条件覆盖全部 4 个值——0 也是合法边界，不能用 0 当「不覆盖」哨兵。</summary>
    public void ApplyTo(ExplorationManager mgr)
    {
        mgr.PlayerLeft = PlayerLeft;
        mgr.PlayerRight = PlayerRight;
        mgr.CamLeft = CamLeft;
        mgr.CamRight = CamRight;
        // 关键：只改字段没用，必须再推到 Player.MapLeft/MapRight 和 Camera.LimitLeft/LimitRight，
        // 否则切房间时玩家/相机仍停留在旧边界。
        mgr.ApplyBounds();
    }
}
