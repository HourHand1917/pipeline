using Godot;
using Godot.Collections;

/// <summary>
/// TV 版地图面板。显示地图网络，高亮当前地图。
/// set_mode(0=Exploration) → 点击地图砖穿梭；set_mode(1=Battle) → 只读。
/// </summary>
[GlobalClass]
public partial class MapPanel : Control
{
    [Signal] public delegate void RouteSelectedEventHandler(string routeId);

    /// <summary>地图砖容器</summary>
    [Export] private GridContainer _mapContainer;
    [Export] private int _columns = 3;
    [Export] private string _registryPath = "res://features/exploration/resources/map_registry.tres";

    private bool _interactive;
    private StringName _currentMapId;

    public override void _Ready()
    {
        if (MapManager.Instance != null)
        {
            _currentMapId = MapManager.Instance.CurrentMapId;
            MapManager.Instance.MapChanged += OnMapChanged;
        }
        RefreshMap();
    }

    public override void _ExitTree()
    {
        if (MapManager.Instance != null)
            MapManager.Instance.MapChanged -= OnMapChanged;
    }

    /// <summary>0 = Exploration, 1 = Battle</summary>
    public void SetMode(int mode)
    {
        _interactive = mode == 0;
        RefreshMap();
    }

    private void OnMapChanged(StringName mapId)
    {
        _currentMapId = mapId;
        RefreshMap();
    }

    private void RefreshMap()
    {
        if (_mapContainer == null) return;

        foreach (Node child in _mapContainer.GetChildren())
            child.QueueFree();

        _mapContainer.Columns = _columns;

        var maps = LoadMaps();
        foreach (var map in maps)
        {
            var id = map.Get("id").AsStringName();
            var displayName = map.Get("display_name").AsString();
            var defaultSpawn = map.Get("default_spawn_id").AsStringName();

            var btn = new Button();
            btn.Text = displayName;
            btn.CustomMinimumSize = new Vector2(90, 56);

            bool isCurrent = id == _currentMapId;
            if (isCurrent)
            {
                btn.Modulate = Colors.White;
                btn.Disabled = false;
            }
            else
            {
                btn.Modulate = new Color(0.6f, 0.6f, 0.6f);
                // 非当前地图：探索模式可点击穿梭，战斗模式禁用
                btn.Disabled = !_interactive;
            }

            btn.Pressed += () =>
            {
                if (!_interactive || id == _currentMapId) return;
                MapManager.Instance?.TravelTo(id, defaultSpawn);
            };

            _mapContainer.AddChild(btn);
        }
    }

    private Array<GodotObject> LoadMaps()
    {
        var result = new Array<GodotObject>();
        var registry = GD.Load<Resource>(_registryPath);
        if (registry == null) return result;

        return registry.Get("maps").AsGodotArray<GodotObject>();
    }
}
