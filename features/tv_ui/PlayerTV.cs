using Godot;

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

    public void OnSwitchMidpoint()
    {
        int count = _panels.Length;
        int newIndex = (_currentIndex + _pendingDirection + count) % count;

        for (int i = 0; i < count; i++)
            if (_panels[i] != null) _panels[i].Visible = i == newIndex;
        _currentIndex = newIndex;

        if (_panels[_currentIndex] == _inventoryPanel)
            RefreshInventory();

        if (_panels[_currentIndex] != _enemyPanel)
            _enemyPanel?.StopTracking();
    }

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

        _enemyPanel.Visible = mode == TVMode.Battle;

        if (mode == TVMode.Exploration)
            _enemyPanel?.StopTracking();

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

    public void UpdateEnemyPanel(EnemyBattle enemy, string actionName = "", string intentText = "")
    {
        _enemyPanel?.TrackEnemy(enemy, actionName);
        // EnemyMessage 需要加上 UpdateIntentText 方法
        if (!string.IsNullOrEmpty(intentText))
        {
            _enemyPanel?.CallDeferred("UpdateIntentText", intentText);
        }
    }

    public void ClearEnemyPanel()
    {
        _enemyPanel?.StopTracking();
    }
}