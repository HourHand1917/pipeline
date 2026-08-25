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
    [Export] private TextureRect enemyIcon;

    [ExportGroup("敌人头像")]
    [Export] private Texture2D boomPortrait;
    [Export] private Texture2D rockyPortrait;
    [Export] private Texture2D sharkkPortrait;
    [Export] private Texture2D coreHandPortrait;
    [Export] private Texture2D coreBodyPortrait;

    private EnemyBattle _trackedEnemy;
    private string _currentActionName = "";
    private string _currentIntentText = "";

    public override void _Ready()
    {
        infoLabel?.AddThemeColorOverride("font_color", new Color("#78e8eb"));
        intentLabel?.AddThemeColorOverride("font_color", new Color("#f1c453"));
        intentLabel?.AddThemeColorOverride("font_outline_color", new Color("#25120d"));
        intentLabel?.AddThemeConstantOverride("outline_size", 5);
        if (healthBar != null) healthBar.TintProgress = new Color("#e35b56");
        if (shieldBar != null) shieldBar.TintProgress = new Color("#63d7e4");
        if (enemyIcon != null) enemyIcon.Visible = false;
    }

    public void TrackEnemy(EnemyBattle enemy, string actionName = "")
    {
        if (_trackedEnemy == enemy)
        {
            UpdateActionName(actionName);
            RefreshPortrait();
            return;
        }
        StopTracking();
        if (enemy == null) return;

        _trackedEnemy = enemy;
        _currentActionName = actionName;
        _trackedEnemy.HealthChanged += OnHealthChanged;
        _trackedEnemy.ShieldChanged += OnShieldChanged;
        _trackedEnemy.IntentChanged += OnIntentChanged;
        _trackedEnemy.Died += OnDied;

        if (_trackedEnemy.PlannedAction != null)
            ApplyPlannedAction(_trackedEnemy.PlannedAction, string.IsNullOrWhiteSpace(actionName));

        RefreshPortrait();
        RefreshAll();
    }

    public void StopTracking()
    {
        if (_trackedEnemy == null) return;
        _trackedEnemy.HealthChanged -= OnHealthChanged;
        _trackedEnemy.ShieldChanged -= OnShieldChanged;
        _trackedEnemy.IntentChanged -= OnIntentChanged;
        _trackedEnemy.Died -= OnDied;
        _trackedEnemy = null;
        ClearDisplay();
    }

    public void UpdateActionName(string actionName)
    {
        _currentActionName = actionName;
        if (_trackedEnemy != null)
        {
            RefreshInfoLabel();
            RefreshIntentLabel();
        }
    }

    public void UpdateIntentText(string intentText)
    {
        _currentIntentText = intentText ?? "";
        if (_trackedEnemy != null) RefreshIntentLabel();
    }

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
        if (_trackedEnemy == null) return;
        string actionText = string.IsNullOrEmpty(_currentActionName) ? "" : $" | 行动：{_currentActionName}";
        infoLabel.Text = $"{_trackedEnemy.DisplayName}{actionText}";
    }

    private void RefreshIntentLabel()
    {
        if (intentLabel == null || _trackedEnemy == null) return;
        intentLabel.Text = string.IsNullOrWhiteSpace(_currentIntentText)
            ? "● 意图：等待 / 调整位置"
            : $"⚠ 下回合：{_currentIntentText}";
    }

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

    private void OnIntentChanged(GodotObject action)
    {
        ApplyPlannedAction(action, true);
        RefreshInfoLabel();
        RefreshIntentLabel();
    }

    private void ApplyPlannedAction(GodotObject action, bool updateName)
    {
        if (action == null)
        {
            if (updateName) _currentActionName = "观察局势";
            _currentIntentText = "尚未锁定攻击区域";
            return;
        }
        if (updateName) _currentActionName = action.Get("display_name").AsString();
        _currentIntentText = action.Get("intent_text").AsString();
        if (string.IsNullOrWhiteSpace(_currentIntentText)) _currentIntentText = _currentActionName;
    }

    private void OnDied()
    {
        if (_trackedEnemy == null) return;
        healthBar.Value = 0;
        healthLabel.Text = $"0 / {_trackedEnemy.MaxHp}";
        infoLabel.Text = $"{_trackedEnemy.DisplayName} 已倒下";
        if (intentLabel != null) intentLabel.Text = "";
    }

    private void RefreshBuffGrid()
    {
        if (buffGrid == null || _trackedEnemy == null) return;
        foreach (Node child in buffGrid.GetChildren()) child.QueueFree();
        var stats = _trackedEnemy.GetStats();
        if (stats == null) return;
        var buffs = stats.Get("buffs").As<Array>();
        foreach (var bi in buffs)
        {
            if (bi.Obj == null) continue;
            var instance = bi.As<GodotObject>();
            var buff = instance.Get("buff").As<GodotObject>();
            int stacks = instance.Get("stacks").AsInt32();
            var label = new Label { Text = $"{buff.Get("buff_name")} ×{stacks}" };
            buffGrid.AddChild(label);
        }
    }

    private void ClearDisplay()
    {
        _currentActionName = "";
        _currentIntentText = "";
        healthBar.Value = 0;
        healthLabel.Text = "-- / --";
        shieldBar.Value = 0;
        shieldLabel.Text = "--";
        infoLabel.Text = "";
        if (intentLabel != null) intentLabel.Text = "";
        if (enemyIcon != null)
        {
            enemyIcon.Texture = null;
            enemyIcon.Visible = false;
            enemyIcon.Set("flip_h", false);
        }
        if (buffGrid != null)
            foreach (Node child in buffGrid.GetChildren()) child.QueueFree();
    }

    private void RefreshPortrait()
    {
        if (enemyIcon == null || _trackedEnemy == null) return;
        string enemyId = (_trackedEnemy.EnemyId ?? "").ToLowerInvariant();
        string role = _trackedEnemy.Role.ToString().ToLowerInvariant();
        Texture2D portrait = null;
        bool mirror = false;

        if (role is "true_hand" or "false_hand")
        {
            portrait = coreHandPortrait;
            mirror = role == "false_hand";
        }
        else if (role == "body" || enemyId.Contains("core")) portrait = coreBodyPortrait;
        else if (enemyId.Contains("shark")) portrait = sharkkPortrait;
        else if (enemyId.Contains("rocky")) portrait = rockyPortrait;
        else if (enemyId.Contains("boom")) portrait = boomPortrait;

        enemyIcon.Texture = portrait;
        enemyIcon.Set("flip_h", mirror);
        enemyIcon.Visible = portrait != null;
    }

    public EnemyBattle GetTrackedEnemy() => _trackedEnemy;
    public void RefreshBuffGridPublic() => RefreshBuffGrid();
}
