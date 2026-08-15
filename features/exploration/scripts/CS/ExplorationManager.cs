using Godot;

[GlobalClass]
public partial class ExplorationManager : Node2D
{
    [Export] public PlayerController Player { get; set; }
    [Export] public InputRouter Input { get; set; }
    [Export] public string MapId { get; set; } = "";
    [Export] private ExplorationHUD _hud;

    [ExportGroup("玩家边界")]
    [Export] public int PlayerLeft { get; set; } = -2000;
    [Export] public int PlayerRight { get; set; } = 3500;

    [ExportGroup("相机边界")]
    [Export] public int CamLeft { get; set; } = -2100;
    [Export] public int CamRight { get; set; } = 3600;


    public override void _Ready()
    {
        // 从 MapManager 读当前地图（覆盖场景里写死的 MapId）
        if (MapManager.Instance != null)
        {
            if (string.IsNullOrEmpty(MapManager.Instance.CurrentMapId))
                MapManager.Instance.SetCurrentMap(MapId);  // 首次进入，注册当前地图
            else
                MapId = MapManager.Instance.CurrentMapId.ToString();
        }

        // 定位玩家到生成点（跨场景穿梭时）
        if (MapManager.Instance != null)
        {
            var spawnId = MapManager.Instance.ConsumePendingSpawn();
            if (!string.IsNullOrEmpty(spawnId))
                PlacePlayerAtSpawn(spawnId);
        }

        // 输入 → 玩家
        if (Input != null && Player != null)
        {
            Input.MovePressed += dir => Player.OnMovePressed(dir);
            Input.MoveReleased += dir => Player.OnMoveReleased(dir);
        }

        if (Player != null)
        {
            Player.MapLeft = PlayerLeft;
            Player.MapRight = PlayerRight;
        }

        var cam = Player?.GetNode<Camera2D>("Camera");
        if (cam != null)
        {
            cam.LimitLeft = CamLeft;
            cam.LimitRight = CamRight;
            cam.LimitSmoothed = true;
        }

        if (!string.IsNullOrEmpty(MapId))
            InitPersistence(this, MapId);

        GD.Print("探索场景已就绪。A/D 移动，鼠标靠近交互物变亮。");

        _hud?.SetMode(PlayerTV.TVMode.Exploration);
    }

    private static void InitPersistence(Node node, string mapId)
    {
        if (node is IPersistable p && !string.IsNullOrEmpty(p.PersistenceId))
            p.InitPersistence(mapId);
        foreach (Node child in node.GetChildren())
            InitPersistence(child, mapId);
    }

    /// <summary>找到对应 SpawnPoint 节点，把玩家定位过去。</summary>
    private void PlacePlayerAtSpawn(StringName spawnId)
    {
        var spawn = FindSpawn(this, spawnId);
        if (spawn != null && Player != null)
        {
            Player.GlobalPosition = spawn.GlobalPosition;
            GD.Print($"探索场景：定位到生成点 {spawnId}");
        }
        else
        {
            GD.Print($"探索场景：未找到生成点 {spawnId}，保持默认位置");
        }
    }

    private static SpawnPoint FindSpawn(Node node, StringName spawnId)
    {
        if (node is SpawnPoint sp && sp.SpawnId == spawnId)
            return sp;
        foreach (Node child in node.GetChildren())
        {
            var found = FindSpawn(child, spawnId);
            if (found != null) return found;
        }
        return null;
    }

}
