using Godot;
using Godot.Collections;

[GlobalClass]
public partial class EnemyMessage : PanelContainer
{
    [Export] private TextureProgressBar healthBar;
    [Export] private Label healthLabel;
    [Export] private TextureProgressBar shieldBar;
    [Export] private Label shieldLabel;
    [Export] private GridContainer buffGrid;
    [Export] private Label infoLabel;
    [Export] private Label intentLabel;

    private EnemyBattle _trackedEnemy;
    private string _currentActionName = "";

    // ================================================================
    //  开始跟踪敌人实例
    // ================================================================

    public void TrackEnemy(EnemyBattle enemy, string actionName = "")
    {
        StopTracking();

        if (enemy == null) return;

        GD.Print($"[调试] EnemyMessage.TrackEnemy enemy={enemy?.DisplayName}, actionName={actionName}");

        _trackedEnemy = enemy;
        _currentActionName = actionName;

        _trackedEnemy.HealthChanged += OnHealthChanged;
        _trackedEnemy.ShieldChanged += OnShieldChanged;
        _trackedEnemy.Died += OnDied;

        RefreshAll();
    }

    // ================================================================
    //  停止跟踪
    // ================================================================

    public void StopTracking()
    {
        if (_trackedEnemy == null) return;

        _trackedEnemy.HealthChanged -= OnHealthChanged;
        _trackedEnemy.ShieldChanged -= OnShieldChanged;
        _trackedEnemy.Died -= OnDied;
        _trackedEnemy = null;

        ClearDisplay();
    }

    // ================================================================
    //  更新行动名（不重新绑定敌人）
    // ================================================================

    public void UpdateActionName(string actionName)
    {
        _currentActionName = actionName;
        if (_trackedEnemy != null)
        {
            RefreshInfoLabel();
            RefreshIntentLabel();
        }
    }

    // ================================================================
    //  全量刷新
    // ================================================================

    private void RefreshAll()
    {
        if (_trackedEnemy == null) return;

        healthBar.MaxValue = _trackedEnemy.MaxHp;
        healthBar.Value = _trackedEnemy.CurrentHp;
        healthLabel.Text = $"{_trackedEnemy.CurrentHp} / {_trackedEnemy.MaxHp}";

        shieldBar.MaxValue = _trackedEnemy.MaxHp;
        shieldBar.Value = _trackedEnemy.Shield;
        shieldLabel.Text = $"{_trackedEnemy.Shield}";

        RefreshInfoLabel();
        RefreshIntentLabel();
        RefreshBuffGrid();
    }

    private void RefreshInfoLabel()
    {
        string actionText = string.IsNullOrEmpty(_currentActionName)
            ? ""
            : $" | 行动：{_currentActionName}";
        infoLabel.Text = $"{_trackedEnemy.DisplayName}{actionText}";
    }

    private void RefreshIntentLabel()
    {
        if (intentLabel == null || _trackedEnemy == null) return;

        if (string.IsNullOrEmpty(_currentActionName))
        {
            intentLabel.Text = "";
        }
    }

    // ================================================================
    //  信号回调
    // ================================================================

    private void OnHealthChanged(int current, int max)
    {
        healthBar.MaxValue = max;
        healthBar.Value = current;
        healthLabel.Text = $"{current} / {max}";
    }

    private void OnShieldChanged(int current)
    {
        shieldBar.Value = current;
        shieldLabel.Text = $"{current}";
    }

    private void OnDied()
    {
        healthBar.Value = 0;
        healthLabel.Text = $"0 / {_trackedEnemy.MaxHp}";
        infoLabel.Text = $"{_trackedEnemy.DisplayName} 已倒下";
        if (intentLabel != null) intentLabel.Text = "";
    }

    // ================================================================
    //  Buff 网格
    // ================================================================

    private void RefreshBuffGrid()
    {
        if (buffGrid == null || _trackedEnemy == null) return;

        foreach (Node child in buffGrid.GetChildren())
            child.QueueFree();

        var stats = _trackedEnemy.GetStats();
        if (stats == null) return;

        var buffs = stats.Get("buffs").As<Array>();
        foreach (var bi in buffs)
        {
            if (bi.Obj == null) continue;
            var instance = bi.As<GodotObject>();
            var buff = instance.Get("buff").As<GodotObject>();
            int stacks = instance.Get("stacks").AsInt32();

            var label = new Label();
            label.Text = $"{buff.Get("buff_name")} ×{stacks}";
            buffGrid.AddChild(label);
        }
    }

    // ================================================================
    //  清空显示
    // ================================================================

    private void ClearDisplay()
    {
        healthBar.Value = 0;
        healthLabel.Text = "-- / --";
        shieldBar.Value = 0;
        shieldLabel.Text = "--";
        infoLabel.Text = "";
        if (intentLabel != null) intentLabel.Text = "";

        if (buffGrid != null)
        {
            foreach (Node child in buffGrid.GetChildren())
                child.QueueFree();
        }
    }

        public EnemyBattle GetTrackedEnemy()
    {
        return _trackedEnemy;
    }
}