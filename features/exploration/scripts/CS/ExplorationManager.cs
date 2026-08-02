using Godot;

/// <summary>
/// 横向卷轴探索场景管理器。
/// 每个地图场景以此为根节点，实例化 Player 和交互物即可。
/// </summary>
[GlobalClass]
public partial class ExplorationManager : Node2D
{
    [Export] public PlayerController Player { get; set; }

    /// <summary>地图唯一 ID（持久化用，如 "forest_01"）</summary>
    [Export] public string MapId { get; set; } = "";

    [ExportGroup("地图边界")]
    [Export] public int MapLeft { get; set; } = -2000;
    [Export] public int MapRight { get; set; } = 3500;

    public override void _Ready()
    {
        // 地图边界 → 玩家不可走出
        if (Player != null)
        {
            Player.MapLeft = MapLeft;
            Player.MapRight = MapRight;
        }

        // 向所有持久化物注入 MapId 并恢复状态
        if (!string.IsNullOrEmpty(MapId))
            InitPersistence(this, MapId);

        GD.Print("探索场景已就绪。A/D 移动，鼠标靠近交互物变亮。");
    }

    /// <summary>递归查找 IPersistable 节点，注入 MapId 恢复状态</summary>
    private static void InitPersistence(Node node, string mapId)
    {
        if (node is IPersistable p && !string.IsNullOrEmpty(p.PersistenceId))
            p.InitPersistence(mapId);

        foreach (Node child in node.GetChildren())
            InitPersistence(child, mapId);
    }

    public override void _Input(InputEvent @event)
    {
        if (@event is InputEventKey keyEvent)
        {
            if (keyEvent.Keycode == Key.A && keyEvent.Pressed)
                Player.OnMovePressed(-1);
            else if (keyEvent.Keycode == Key.D && keyEvent.Pressed)
                Player.OnMovePressed(1);
            else if ((keyEvent.Keycode == Key.A || keyEvent.Keycode == Key.D) && !keyEvent.Pressed)
                Player.OnMoveReleased(0);
        }
    }
}
