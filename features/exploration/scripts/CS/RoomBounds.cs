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

    /// <summary>推边界给 Manager。0 值表示不覆盖。</summary>
    public void ApplyTo(ExplorationManager mgr)
    {
        if (PlayerLeft != 0) mgr.PlayerLeft = PlayerLeft;
        if (PlayerRight != 0) mgr.PlayerRight = PlayerRight;
        if (CamLeft != 0) mgr.CamLeft = CamLeft;
        if (CamRight != 0) mgr.CamRight = CamRight;
    }
}
