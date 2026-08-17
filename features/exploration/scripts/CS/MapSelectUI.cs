using Godot;

/// <summary>
/// 地图选择 UI。大门点击后弹出，三个按钮分别传送到 f2/f3/f4 第 0 房间的左边。
/// </summary>
[GlobalClass]
public partial class MapSelectUI : Control
{
    [Signal] public delegate void ClosedEventHandler();

    [Export] private Button _f2Button;
    [Export] private Button _f3Button;
    [Export] private Button _f4Button;
    [Export] private Button _closeButton;

    public override void _Ready()
    {
        Visible = false;
        if (_f2Button != null) _f2Button.Pressed += () => Travel("f2_0", "f2_0left");
        if (_f3Button != null) _f3Button.Pressed += () => Travel("f3_0", "f3_0left");
        if (_f4Button != null) _f4Button.Pressed += () => Travel("f4", "f4entry");
        if (_closeButton != null) _closeButton.Pressed += Close;
    }

    public void Open()
    {
        Visible = true;
    }

    public void Close()
    {
        Visible = false;
        EmitSignal(SignalName.Closed);
    }

    private void Travel(string mapId, string spawnId)
    {
        MapManager.Instance?.TravelTo(new StringName(mapId), new StringName(spawnId));
    }
}
