using Godot;

[GlobalClass]
public partial class ExplorationManager : Node2D
{
    [Export] public PlayerController Player { get; set; }
    [Export] public InputRouter Input { get; set; }
    [Export] public string MapId { get; set; } = "";
    [Export] private ExplorationHUD _hud;

    [ExportGroup("音乐")]
    [Export] public AudioStream MapMusic { get; set; }

    [ExportGroup("玩家边界")]
    [Export] public int PlayerLeft { get; set; } = -2000;
    [Export] public int PlayerRight { get; set; } = 3500;

    [ExportGroup("相机边界")]
    [Export] public int CamLeft { get; set; } = -2100;
    [Export] public int CamRight { get; set; } = 3600;

    public override void _Ready()
    {
        if (MapManager.Instance != null)
        {
            if (string.IsNullOrEmpty(MapManager.Instance.CurrentMapId))
                MapManager.Instance.SetCurrentMap(MapId);
            else
                MapId = MapManager.Instance.CurrentMapId.ToString();
        }

        if (MapManager.Instance != null)
        {
            var spawnId = MapManager.Instance.ConsumePendingSpawn();
            if (!string.IsNullOrEmpty(spawnId))
                PlacePlayerAtSpawn(spawnId);
        }

        if (Input != null && Player != null)
        {
            Input.MovePressed += dir => Player.OnMovePressed(dir);
            Input.MoveReleased += dir => Player.OnMoveReleased(dir);
        }

        ApplyBounds();

        if (!string.IsNullOrEmpty(MapId))
            InitPersistence(this, MapId);

        PlayMapMusic();

        GD.Print("探索场景已就绪。A/D 移动，鼠标靠近交互物变亮。");

        _hud?.SetMode(PlayerTV.TVMode.Exploration);
    }

    /// <summary>
    /// 播放当前地图音乐。如果和正在播放的音乐相同，跳过（连续播放）。
    /// 不同则淡入淡出切换。
    /// </summary>
    private void PlayMapMusic()
    {
        if (MapMusic == null) return;

        var audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
        if (audio == null) return;

        if (audio.IsMusicPlaying(MapMusic))
            return; // 同一层，继续播放

        audio.PlayMusicWithFade(MapMusic);
    }

    public void ApplyBounds()
    {
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
    }

    private static void InitPersistence(Node node, string mapId)
    {
        if (node is IPersistable p && !string.IsNullOrEmpty(p.PersistenceId))
            p.InitPersistence(mapId);
        foreach (Node child in node.GetChildren())
            InitPersistence(child, mapId);
    }

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