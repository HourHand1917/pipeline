using Godot;
using Godot.Collections;

/// <summary>
/// 地图管理器（Autoload）。负责跨地图场景切换，转场效果交给 SceneTransition。
/// 门调用 TravelTo，新场景的 ExplorationManager 读当前位置和生成点。
/// </summary>
[GlobalClass]
public partial class MapManager : Node
{
    [Signal] public delegate void MapChangedEventHandler(StringName mapId);

    public static MapManager Instance { get; private set; }

    /// <summary>当前所在地图 id</summary>
    public StringName CurrentMapId { get; private set; } = "";
    /// <summary>当前生成点 id</summary>
    public StringName CurrentSpawnId { get; private set; } = "";

    [Export] private string _registryPath = "res://features/exploration/resources/map_registry.tres";

    private StringName _pendingSpawnId = "";
    private bool _traveling;

    /// <summary>是否正在跨地图转场中（供新场景的玩家在 _Ready 里判断是否锁定移动）。</summary>
    public bool IsTraveling => _traveling;

    // id → scene_path
    private System.Collections.Generic.Dictionary<StringName, string> _scenePaths = new();

    public override void _Ready()
    {
        if (Instance != null) { GD.PushError("MapManager: 重复实例化"); return; }
        Instance = this;
        LoadRegistry();
    }

    private void LoadRegistry()
    {
        var registry = GD.Load<Resource>(_registryPath);
        if (registry == null)
        {
            GD.PrintErr($"MapManager: 找不到地图注册表 {_registryPath}");
            return;
        }

        var maps = registry.Get("maps").AsGodotArray<GodotObject>();
        foreach (var map in maps)
        {
            var id = map.Get("id").AsStringName();
            var scenePath = map.Get("scene_path").AsString();
            _scenePaths[id] = scenePath;
        }
    }

    /// <summary>
    /// 切到目标地图，在指定生成点出现。
    /// 流程：圈缩到中央 → 切场景 → 圈放大露出。
    /// </summary>
    public async void TravelTo(StringName targetMapId, StringName spawnId)
    {
        if (_traveling) return;

        if (!_scenePaths.TryGetValue(targetMapId, out string scenePath))
        {
            GD.PrintErr($"MapManager: 未知地图 id {targetMapId}");
            return;
        }

        _traveling = true;

        // 圈缩（IrisClose）前锁定旧场景玩家，避免转场时还能走动
        PlayerController.Instance?.LockMovement();

        // 切换前更新状态，让新场景 _Ready 能读到
        CurrentMapId = targetMapId;
        CurrentSpawnId = spawnId;
        _pendingSpawnId = spawnId;

        // 猫和老鼠转场
        await SceneTransition.Instance.IrisClose();
        GetTree().ChangeSceneToFile(scenePath);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        EmitSignal(SignalName.MapChanged, CurrentMapId);

        await SceneTransition.Instance.IrisOpen();
        PlayerController.Instance?.UnlockMovement(); // 入场完成，恢复移动

        _traveling = false;
    }

    /// <summary>
    /// 新场景 ExplorationManager 调用，取走待生成点（读后清空）。
    /// </summary>
    public StringName ConsumePendingSpawn()
    {
        var s = _pendingSpawnId;
        _pendingSpawnId = "";
        return s;
    }

    /// <summary>
    /// 首次进入地图（非 TravelTo）时由 ExplorationManager 调用，注册当前地图。
    /// </summary>
    public void SetCurrentMap(StringName mapId)
    {
        if (CurrentMapId == mapId) return;
        CurrentMapId = mapId;
        EmitSignal(SignalName.MapChanged, CurrentMapId);
    }
}
