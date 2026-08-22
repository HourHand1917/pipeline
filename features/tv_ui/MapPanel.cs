using Godot;

/// <summary>
/// TV 版地图面板（只读）。房间方块直接摆放在场景里（可在编辑器里拖动/缩放），
/// 按「层」分组：Home / Floor1 / Floor2 / Floor3 / Floor4。
/// 运行时只显示当前层，并高亮当前房间（方块节点名 = 地图 id）。
/// </summary>
[GlobalClass]
public partial class MapPanel : Control
{
    /// <summary>层容器（Home / Floor1~4）的父节点。</summary>
    [Export] private Control _mapContainer;
    /// <summary>房间方块文字字号。</summary>
    [Export] private int _roomFontSize = 18;

    private StringName _currentMapId;

    public override void _Ready()
    {
        if (MapManager.Instance != null)
        {
            _currentMapId = MapManager.Instance.CurrentMapId;
            MapManager.Instance.MapChanged += OnMapChanged;
        }

        // 所有房间方块设为不可交互、统一字号（纯显示）
        if (_mapContainer != null)
        {
            foreach (Node floor in _mapContainer.GetChildren())
            {
                foreach (Node room in floor.GetChildren())
                {
                    if (room is not Button btn) continue;
                    btn.MouseFilter = MouseFilterEnum.Ignore;
                    btn.FocusMode = FocusModeEnum.None;
                    btn.AddThemeFontSizeOverride("font_size", _roomFontSize);
                    btn.AddThemeColorOverride("font_color", Colors.White);
                }
            }
        }

        RefreshMap();
    }

    public override void _ExitTree()
    {
        if (MapManager.Instance != null)
            MapManager.Instance.MapChanged -= OnMapChanged;
    }

    /// <summary>只读面板：SetMode 不再区分交互，仅刷新。</summary>
    public void SetMode(int mode) => RefreshMap();

    private void OnMapChanged(StringName mapId)
    {
        _currentMapId = mapId;
        RefreshMap();
    }

    private void RefreshMap()
    {
        if (_mapContainer == null) return;

        string floor = FloorOf(_currentMapId);
        if (string.IsNullOrEmpty(floor)) return; // 当前地图 id 未就绪或不属于任何层

        string containerName = FloorContainerName(floor);
        string currentId = _currentMapId.ToString();

        foreach (Node child in _mapContainer.GetChildren())
        {
            if (child is not Control container) continue;
            bool isCurrentFloor = child.Name.ToString() == containerName;
            container.Visible = isCurrentFloor;
            if (!isCurrentFloor) continue;

            foreach (Node room in container.GetChildren())
            {
                if (room is not CanvasItem ci) continue;
                ci.Modulate = room.Name.ToString() == currentId
                    ? Colors.White
                    : new Color(0.6f, 0.6f, 0.6f);
            }
        }
    }

    /// <summary>从地图 id 推导层：home / f1 / f2 / f3 / f4，其它返回空串。</summary>
    private static string FloorOf(StringName id)
    {
        string s = id.ToString();
        if (s == "home") return "home";
        if (s.StartsWith("f1")) return "f1";
        if (s.StartsWith("f2")) return "f2";
        if (s.StartsWith("f3")) return "f3";
        if (s.StartsWith("f4")) return "f4";
        return "";
    }

    /// <summary>层名 → 场景里的容器节点名：home→Home，f1→Floor1……</summary>
    private static string FloorContainerName(string floor)
    {
        if (floor == "home") return "Home";
        if (floor.Length >= 2) return "Floor" + floor.Substring(1);
        return "";
    }
}
