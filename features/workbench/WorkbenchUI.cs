using Godot;
using System.Collections.Generic;

/// <summary>
/// 工作台 UI 容器。三个 tab 按钮切换三个页面。
/// 切换流程：播旧页面 hide → 末尾回调换页 → 播新页面 show → 末尾回调解锁。
///
/// 动画（Anim 里）：
///   RESET           → WorkbenchUI + 三个页面 visible=false
///   show_workbench  → 入场，末尾 method→EnableAllButtons(true)
///   hide_workbench  → 退场，开头 method→EnableAllButtons(false)
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
    [Export] private TextureButton _closeButton;

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
    private bool _basePositionsCaptured;
    private Dictionary<BaseButton, bool> _savedDisabledStates;
    private bool _buttonsDisabled;

    public override void _Ready()
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

        // 页面可见性和 tab 升起都推迟到 Open() 时做。
        // HBoxContainer 的首次排序是 call_deferred 触发的，比 process_frame 还晚，
        // 在 _Ready 里（哪怕延迟一帧）做 tween 仍会被布局覆盖，导致首次打开不升起。
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
        EnableAllButtons(false);
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
        else
        {
            EnableAllButtons(true);
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
        // 惰性捕获基准位置：只在第一次真正用到时抓取（此时必已布局完成），且只抓一次，
        // 避免把已经升起的位置误当成基准。
        if (!_basePositionsCaptured)
        {
            for (int i = 0; i < _tabs.Length; i++)
                if (_tabs[i] != null) _tabBasePositions[i] = _tabs[i].Position;
            _basePositionsCaptured = true;
        }

        // 补给机「小工作台」没有 tab 按钮：跳过升降 tween，避免空 tween 报错。
        bool hasTabs = false;
        for (int i = 0; i < _tabs.Length; i++)
            if (_tabs[i] != null) { hasTabs = true; break; }
        if (!hasTabs) return;

        _tabTween?.Kill();
        _tabTween = CreateTween();
        // 并行播放，让「选中升起」和「其他降下」同时进行，而不是逐个顺序播放
        _tabTween.SetParallel(true);

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
        // 首次打开在这里初始化：ShowPage 会设置页面可见性 + 升起当前 tab。
        // 此时布局早已完成，基准位置正确，不会被容器重排覆盖。
        ShowPage(_currentIndex);
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

    /// <summary>
    /// 禁用/启用工作台内所有按钮（递归查找 BaseButton 子节点）。供动画轨道 method 调用，
    /// 也用于页切换期间。禁用前会记住每个按钮原有的 Disabled 状态，启用时恢复，避免把
    /// 逻辑上本就该禁用的按钮（如棋盘越界格、库存为 0 的卡牌）误开启。
    /// 幂等：重复调用 false / true 不会重复保存或覆盖原始状态。
    /// </summary>
    public void EnableAllButtons(bool enable)
    {
        if (enable)
        {
            if (!_buttonsDisabled) return;
            foreach (var pair in _savedDisabledStates)
            {
                if (GodotObject.IsInstanceValid(pair.Key))
                    pair.Key.Disabled = pair.Value;
            }
            _savedDisabledStates = null;
            _buttonsDisabled = false;
        }
        else
        {
            if (_buttonsDisabled) return;
            _savedDisabledStates = new Dictionary<BaseButton, bool>();
            var buttons = new List<BaseButton>();
            CollectButtons(this, buttons);
            foreach (var btn in buttons)
            {
                _savedDisabledStates[btn] = btn.Disabled;
                btn.Disabled = true;
            }
            _buttonsDisabled = true;
        }
    }

    private static void CollectButtons(Node node, List<BaseButton> result)
    {
        if (node is BaseButton btn)
            result.Add(btn);
        foreach (Node child in node.GetChildren())
            CollectButtons(child, result);
    }
}
