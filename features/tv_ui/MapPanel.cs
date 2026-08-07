using Godot;

/// <summary>
/// TV 版地图面板。嵌入 PlayerTV.PanelStack。
/// set_mode(0=Exploration) → 可点击交互选择路线
/// set_mode(1=Battle) → 只读显示当前位置
/// </summary>
[GlobalClass]
public partial class MapPanel : Control
{
    [Signal] public delegate void RouteSelectedEventHandler(string routeId);

    [Export] private Label _placeholderLabel;
    [Export] private Button[] _routeButtons;

    private bool _interactive;

    public override void _Ready()
    {
        if (_routeButtons != null)
        {
            foreach (var btn in _routeButtons)
            {
                if (btn == null) continue;
                btn.Pressed += () =>
                {
                    if (!_interactive) return;
                    EmitSignal(SignalName.RouteSelected, btn.Name);
                };
            }
        }
        UpdateDisplay();
    }

    /// <summary>0 = Exploration, 1 = Battle</summary>
    public void SetMode(int mode)
    {
        _interactive = mode == 0;
        UpdateDisplay();
    }

    private void UpdateDisplay()
    {
        if (_routeButtons != null)
        {
            foreach (var btn in _routeButtons)
                if (btn != null) btn.Disabled = !_interactive;
        }
        if (_placeholderLabel != null)
        {
            _placeholderLabel.Text = _interactive ? "地图 — 点击路线选择" : "地图 — 当前位置";
        }
    }
}
