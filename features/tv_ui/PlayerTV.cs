using Godot;

/// <summary>
/// 玩家 TV UI 容器。右下角小电视，上下按钮切换三个面板。
/// 所有动画由你在 AnimationPlayer 里制作。
///
/// 需要做的动画：
///   RESET         → 初始状态
///   show_tv       → TV 整体滑入 + 淡入，method track 调 EnableButtons(true)
///   hide_tv       → TV 整体滑出 + 淡出，method track 调 EnableButtons(false)
///   switch_panel  → PanelStack fade out→in，中点 method track 调 OnSwitchMidpoint()
/// </summary>
[GlobalClass]
public partial class PlayerTV : Control
{
    [Signal] public delegate void ItemUsedEventHandler(int index);
    [Signal] public delegate void ItemDiscardedEventHandler(int index);
    [Signal] public delegate void RouteSelectedEventHandler(string routeId);

    public enum TVMode { Exploration, Battle }

    [Export] private AnimationPlayer _anim;
    [Export] private TextureButton _upButton;
    [Export] private TextureButton _downButton;
    [Export] private MapPanel _mapPanel;
    [Export] private InventoryPanel _inventoryPanel;
    [Export] private EnemyMessage _enemyPanel;

    private Control[] _panels;
    private int _currentIndex;
    private int _pendingDirection;
    private bool _switching;

    public override void _Ready()
    {
        _panels = new Control[] { _mapPanel, _inventoryPanel, _enemyPanel };

        if (_upButton != null)
            _upButton.Pressed += () => SwitchPanel(-1);
        if (_downButton != null)
            _downButton.Pressed += () => SwitchPanel(1);

        for (int i = 0; i < _panels.Length; i++)
            if (_panels[i] != null) _panels[i].Visible = i == 0;
    }

    // ================================================================
    //  面板切换（AnimationPlayer 驱动）
    // ================================================================

    private int? _queuedDirection;

    private void SwitchPanel(int direction)
    {
        if (_panels == null || _panels.Length == 0) return;

        if (_switching)
        {
            _queuedDirection = direction;
            return;
        }

        _pendingDirection = direction;
        _queuedDirection = null;
        _switching = true;
        _anim?.Play("switch_panel");
    }

    /// <summary>动画中点回调 — 换子面板</summary>
    public void OnSwitchMidpoint()
    {
        int count = _panels.Length;
        int newIndex = (_currentIndex + _pendingDirection + count) % count;

        for (int i = 0; i < count; i++)
            if (_panels[i] != null) _panels[i].Visible = i == newIndex;
        _currentIndex = newIndex;

        // 切到背包面板时刷新数据和货币显示
        if (_panels[_currentIndex] == _inventoryPanel)
            RefreshInventory();

        // 切离敌人面板时停止跟踪
        if (_panels[_currentIndex] != _enemyPanel)
            _enemyPanel?.StopTracking();
    }

    /// <summary>动画末尾回调 — 解锁按钮，处理排队</summary>
    public void OnSwitchComplete()
    {
        _switching = false;

        if (_queuedDirection.HasValue)
        {
            int dir = _queuedDirection.Value;
            _queuedDirection = null;
            SwitchPanel(dir);
        }
    }

    // ================================================================
    //  Mode
    // ================================================================

    public void SetMode(TVMode mode)
    {
        int m = (int)mode;
        _mapPanel?.SetMode(m);
        _inventoryPanel?.SetMode(m);

        _panels = mode == TVMode.Exploration
            ? new Control[] { _mapPanel, _inventoryPanel }
            : new Control[] { _mapPanel, _inventoryPanel, _enemyPanel };

        _enemyPanel.Visible = mode != TVMode.Exploration;

        for (int i = 0; i < _panels.Length; i++)
            if (_panels[i] != null) _panels[i].Visible = i == 0;
        _currentIndex = 0;
    }

    // ================================================================
    //  TV 显隐
    // ================================================================

    public void ShowTV()
    {
        _anim?.Play("show_tv");
    }

    public void HideTV()
    {
        _anim?.Play("hide_tv");
    }

    /// <summary>AnimationPlayer Method Track 调用</summary>
    public void EnableButtons(bool enable)
    {
        if (_upButton != null) _upButton.Disabled = !enable;
        if (_downButton != null) _downButton.Disabled = !enable;
    }

    // ================================================================
    //  面板刷新
    // ================================================================

    public void RefreshInventory()
    {
        _inventoryPanel?.Refresh();
    }

    /// <summary>更新敌人面板（传入敌人实例和行动名）</summary>
    public void UpdateEnemyPanel(EnemyBattle enemy, string actionName = "")
    {
        _enemyPanel?.TrackEnemy(enemy, actionName);
    }

    /// <summary>清除敌人面板跟踪</summary>
    public void ClearEnemyPanel()
    {
        _enemyPanel?.StopTracking();
    }
}