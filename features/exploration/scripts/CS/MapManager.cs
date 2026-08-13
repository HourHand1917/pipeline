using Godot;
using Godot.Collections;

/// <summary>
/// 地图管理器（CanvasLayer Autoload）。负责跨地图场景切换 + 淡入淡出。
/// 门调用 TravelTo，新场景的 ExplorationManager 读当前位置和生成点。
/// </summary>
[GlobalClass]
public partial class MapManager : CanvasLayer
{
    [Signal] public delegate void MapChangedEventHandler(StringName mapId);

    public static MapManager Instance { get; private set; }

    /// <summary>当前所在地图 id</summary>
    public StringName CurrentMapId { get; private set; } = "";
    /// <summary>当前生成点 id</summary>
    public StringName CurrentSpawnId { get; private set; } = "";

    [Export] private string _registryPath = "res://features/exploration/resources/map_registry.tres";
    [Export] private float _fadeDuration = 0.5f;

    private ColorRect _fadeRect;
    private StringName _pendingSpawnId = "";
    private bool _traveling;

    // id → scene_path
    private System.Collections.Generic.Dictionary<StringName, string> _scenePaths = new();

    public override void _Ready()
    {
        if (Instance != null) { GD.PushError("MapManager: 重复实例化"); return; }
        Instance = this;
        Layer = 100;

        BuildFadeRect();
        LoadRegistry();
    }

    private void BuildFadeRect()
    {
        _fadeRect = new ColorRect
        {
            Color = new Color(0, 0, 0, 0),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        _fadeRect.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(_fadeRect);
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
    /// 流程：淡入 → 切场景 → 淡出。
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

        // 切换前更新状态，让新场景 _Ready 能读到
        CurrentMapId = targetMapId;
        CurrentSpawnId = spawnId;
        _pendingSpawnId = spawnId;

        // 淡入
        await FadeTo(1f);

        // 切场景
        GetTree().ChangeSceneToFile(scenePath);

        // 等一帧，让新场景 _Ready 执行完
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        EmitSignal(SignalName.MapChanged, CurrentMapId);

        // 淡出
        await FadeTo(0f);

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

    private async System.Threading.Tasks.Task FadeTo(float alpha)
    {
        var tween = CreateTween();
        tween.TweenProperty(_fadeRect, "color:a", alpha, _fadeDuration);
        await ToSignal(tween, Tween.SignalName.Finished);
    }
}
