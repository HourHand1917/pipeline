using Godot;

/// <summary>
/// 探索场景 HUD：TV + 棋盘 + 血条。
/// 血条复用战斗场景贴图，血量从 DataManager 获取。
/// HUD 出现/消失动画（show_hud / hide_hud）在编辑器的 AnimationPlayer 里制作。
/// </summary>
[GlobalClass]
public partial class ExplorationHUD : Control
{
    [Export] private TextureProgressBar _healthBar;
    [Export] private AnimationPlayer _anim;
    [Export] private PlayerTV _playerTV;

    public override void _Ready()
    {
        if (_healthBar != null)
        {
            DataManager.Instance.HealthChanged += OnHealthChanged;
            OnHealthChanged(DataManager.Instance.PlayerHp, DataManager.Instance.MaxPlayerHp);
        }
    }

    public override void _ExitTree()
    {
        if (_healthBar != null)
            DataManager.Instance.HealthChanged -= OnHealthChanged;
    }

    private void OnHealthChanged(int current, int max)
    {
        if (_healthBar == null) return;
        _healthBar.MaxValue = max;
        _healthBar.Value = current;
    }

    // ================================================================
    //  HUD 显隐动画（AnimationPlayer 驱动）
    // ================================================================

    /// <summary>设置内部 TV 的模式（探索模式）。</summary>
    public void SetMode(PlayerTV.TVMode mode)
    {
        _playerTV?.SetMode(mode);
    }

    public void ShowHUD()
    {
        _anim?.Play("show_hud");
    }

    public void HideHUD()
    {
        _anim?.Play("hide_hud");
    }

    /// <summary>AnimationPlayer Method Track 调用（如禁用 TV 按钮，防止动画期间误点）</summary>
    public void EnableHudButtons(bool enable)
    {
        // 预留：动画里需要禁用交互时，在这里处理子节点按钮
    }

    /// <summary>触发战利品领取页（宝箱/敌人等交互物调用）。路由到 TV 内的 RewardPage。</summary>
    public void ShowReward(ILootSource source)
    {
        _playerTV?.OpenReward(source);
    }
}
