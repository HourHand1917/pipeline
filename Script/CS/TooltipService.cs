using Godot;
using System;

/// <summary>
/// 全局 Tooltip 服务（Autoload, CanvasLayer）。
/// 加载 tooltip_panel.tscn，管理显示/隐藏/定位。
/// 动画在编辑器的 AnimationPlayer 中制作。
///
/// 使用：
///   TooltipService.Instance.Show(data);
///   TooltipService.Instance.ShowFor(control, data);
///   TooltipService.Instance.HideTooltip();
/// </summary>
[GlobalClass]
public partial class TooltipService : CanvasLayer
{
    public static TooltipService Instance { get; private set; }

    /// <summary>显示延迟（秒），避免鼠标快速扫过时闪烁</summary>
    [Export] public float ShowDelay { get; set; } = 0.25f;

    /// <summary>tooltip 相对鼠标的偏移</summary>
    [Export] public Vector2 TooltipOffset { get; set; } = new(16, 16);

    private TooltipPanel _panel;
    private AnimationPlayer _anim;

    private TooltipData _pendingData;
    private Control _pendingOwner;
    private float _hoverTimer;
    private bool _isVisible;

    public override void _Ready()
    {
        if (Instance != null) { GD.PushError("TooltipService: 重复实例化"); return; }
        Instance = this;
        Layer = 128;

        var scene = GD.Load<PackedScene>("res://Scenes/resource_scene/tooltip_panel.tscn");
        _panel = scene.Instantiate<TooltipPanel>();
        _anim = _panel.Anim;
        AddChild(_panel);
    }

    public override void _Process(double delta)
    {
        if (_pendingData != null)
        {
            _hoverTimer += (float)delta;
            if (_hoverTimer >= ShowDelay)
            {
                _panel.Render(_pendingData);
                _anim?.Play("fade_in");
                _isVisible = true;
                _pendingData = null;
            }
        }

        if (_isVisible)
        {
            _panel.GlobalPosition = GetViewport().GetMousePosition() + TooltipOffset;

            var screenSize = GetViewport().GetVisibleRect().Size;
            var panelSize = _panel.Size;
            if (_panel.GlobalPosition.X + panelSize.X > screenSize.X)
                _panel.GlobalPosition = new Vector2(screenSize.X - panelSize.X - 8, _panel.GlobalPosition.Y);
            if (_panel.GlobalPosition.Y + panelSize.Y > screenSize.Y)
                _panel.GlobalPosition = new Vector2(_panel.GlobalPosition.X, screenSize.Y - panelSize.Y - 8);
        }
    }

    // ================================================================
    //  公开 API
    // ================================================================

    /// <summary>立即在当前鼠标位置显示 tooltip</summary>
    public void Show(TooltipData data)
    {
        CancelPending();
        _panel.Render(data);
        _anim?.Play("fade_in");
        _isVisible = true;
    }

    /// <summary>隐藏 tooltip</summary>
    public void HideTooltip()
    {
        CancelPending();
        if (!_isVisible) return;
        _anim?.Play("fade_out");
        _isVisible = false;
    }

    /// <summary>给 Control 绑定 tooltip——鼠标移入延迟显示，移出隐藏</summary>
    public void ShowFor(Control control, TooltipData data)
    {
        if (control == null || data == null) return;

        if (_bindings.ContainsKey(control))
            HideFor(control);

        _bindings[control] = data;

        Action enter = () => OnControlEntered(control);
        Action exit = () => OnControlExited(control);
        _handlers[control] = (enter, exit);

        control.MouseEntered += enter;
        control.MouseExited += exit;
    }

    /// <summary>解除绑定</summary>
    public void HideFor(Control control)
    {
        if (control == null || !_bindings.ContainsKey(control)) return;

        if (_handlers.TryGetValue(control, out var handlers))
        {
            control.MouseEntered -= handlers.enter;
            control.MouseExited -= handlers.exit;
            _handlers.Remove(control);
        }
        _bindings.Remove(control);
    }

    // ================================================================
    //  内部
    // ================================================================

    private System.Collections.Generic.Dictionary<Control, TooltipData> _bindings = new();
    private System.Collections.Generic.Dictionary<Control, (Action enter, Action exit)> _handlers = new();

    private void OnControlEntered(Control control)
    {
        if (_bindings.TryGetValue(control, out var data))
        {
            CancelPending();
            _pendingOwner = control;
            _pendingData = data;
            _hoverTimer = 0f;
        }
    }

    private void OnControlExited(Control control)
    {
        if (_pendingOwner == control)
            CancelPending();
        HideTooltip();
    }

    private void CancelPending()
    {
        _pendingData = null;
        _pendingOwner = null;
        _hoverTimer = 0f;
    }
}
