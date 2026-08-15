using Godot;

/// <summary>
/// 工作台 UI 容器。三个 tab 按钮切换三个页面。
/// 切换流程：播旧页面 hide → 末尾回调换页 → 播新页面 show → 末尾回调解锁。
///
/// 动画（Anim 里）：
///   RESET           → WorkbenchUI + 三个页面 visible=false
///   show_workbench  → 入场，frame 0 method→EnableTabs(true)
///   hide_workbench  → 退场，frame 0 method→EnableTabs(false)
///   build_show/hide, upgrade_show/hide, ai_show/hide
///     frame 0 设页面 visible，frame 0.5 method→OnPanelHidden / OnPanelShown
/// </summary>
[GlobalClass]
public partial class WorkbenchUI : Control
{
    [Signal] public delegate void ClosedEventHandler();

    [Export] private AnimationPlayer _anim;
    [Export] private TextureButton _buildTab;
    [Export] private TextureButton _upgradeTab;
    [Export] private TextureButton _aiTab;
    [Export] private Texture2D _buildTabActive;
    [Export] private Texture2D _upgradeTabActive;
    [Export] private Texture2D _aiTabActive;
    [Export] private BuildWorkbenchPanel _buildPage;
    [Export] private EnhanceWorkbenchPanel _upgradePage;
    [Export] private Control _aiPage;
    [Export] private Button _closeButton;

    /// <summary>选中 tab 升起的高度（像素）</summary>
    [Export] private float _tabRaiseOffset = 12f;

    private TextureButton[] _tabs;
    private Texture2D[] _activeTextures;
    private Texture2D[] _normalTextures;
    private Vector2[] _tabBasePositions;
    private Control[] _pages;
    private string[] _showAnims;
    private string[] _hideAnims;
    private int _currentIndex;
    private int _pendingIndex;
    private bool _switching;
    private int? _queuedIndex;
    private Tween _tabTween;

    public override async void _Ready()
    {
        _tabs = new[] { _buildTab, _upgradeTab, _aiTab };
        _activeTextures = new[] { _buildTabActive, _upgradeTabActive, _aiTabActive };
        _normalTextures = new Texture2D[_tabs.Length];
        _tabBasePositions = new Vector2[_tabs.Length];
        _pages = new Control[] { _buildPage, _upgradePage, _aiPage };
        _showAnims = new[] { "build_show", "upgrade_show", "ai_show" };
        _hideAnims = new[] { "build_hide", "upgrade_hide", "ai_hide" };

        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i] != null)
            {
                _normalTextures[i] = _tabs[i].TextureNormal;
                int idx = i;
                _tabs[i].Pressed += () => SwitchToPage(idx);
            }
        }

        if (_closeButton != null)
            _closeButton.Pressed += Close;

        // 等 HBoxContainer 完成首次布局后再捕获基准位置并显示首页。
        // _Ready 里直接 ShowPage(0) 时 position 还没布局，tween 会被布局覆盖，导致首次打开按钮不升起。
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        for (int i = 0; i < _tabs.Length; i++)
            if (_tabs[i] != null) _tabBasePositions[i] = _tabs[i].Position;
        ShowPage(0);
    }

    // ================================================================
    //  页切换
    // ================================================================

    private void SwitchToPage(int index)
    {
        if (_pages == null || index == _currentIndex) return;

        // 点击立即升起目标 tab，不等旧页退场动画播完
        UpdateTabVisuals(index);

        if (_switching)
        {
            _queuedIndex = index;
            return;
        }

        _pendingIndex = index;
        _queuedIndex = null;
        _switching = true;
        _anim?.Play(_hideAnims[_currentIndex]);
    }

    public void OnPanelHidden()
    {
        ShowPage(_pendingIndex);
        _anim?.Play(_showAnims[_currentIndex]);
    }

    public void OnPanelShown()
    {
        _switching = false;

        if (_queuedIndex.HasValue)
        {
            int idx = _queuedIndex.Value;
            _queuedIndex = null;
            SwitchToPage(idx);
        }
    }

    private void ShowPage(int index)
    {
        for (int i = 0; i < _pages.Length; i++)
            if (_pages[i] != null) _pages[i].Visible = i == index;

        UpdateTabVisuals(index);

        _currentIndex = index;
        RefreshCurrentPage();
    }

    /// <summary>更新 tab 按钮贴图与升降（选中升起、其他降回）。可重复调用，幂等。</summary>
    private void UpdateTabVisuals(int index)
    {
        _tabTween?.Kill();
        _tabTween = CreateTween();

        for (int i = 0; i < _tabs.Length; i++)
        {
            if (_tabs[i] == null) continue;
            _tabs[i].TextureNormal = i == index && _activeTextures[i] != null
                ? _activeTextures[i] : _normalTextures[i];

            float targetY = i == index
                ? _tabBasePositions[i].Y - _tabRaiseOffset
                : _tabBasePositions[i].Y;
            _tabTween.TweenProperty(_tabs[i], "position:y", targetY, 0.2f)
                .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        }
    }

    private void RefreshCurrentPage()
    {
        if (_currentIndex == 0)
            _buildPage?.RefreshAll();
        else if (_currentIndex == 1)
            _upgradePage?.Refresh();
    }

    // ================================================================
    //  工作台整体显隐
    // ================================================================

    public void Open()
    {
        Visible = true;
        _anim?.Play("show_workbench");
    }

    public void Close()
    {
        CancelPendingSwitch();
        _anim?.Play("hide_workbench");
        EmitSignal(SignalName.Closed);
    }

    /// <summary>
    /// 关闭时取消进行中的页切换。页切换动画被 hide_workbench 打断后，
    /// OnPanelHidden / OnPanelShown 不会触发，必须手动复位状态并同步页面可见性，
    /// 否则 _switching 会永久卡在 true，重新打开后点 tab 无响应。
    /// </summary>
    private void CancelPendingSwitch()
    {
        _switching = false;
        _queuedIndex = null;
        _pendingIndex = 0;
        ShowPage(_currentIndex);
    }

    public void EnableTabs(bool enable)
    {
        foreach (var t in _tabs)
            if (t != null) t.Disabled = !enable;
    }
}
