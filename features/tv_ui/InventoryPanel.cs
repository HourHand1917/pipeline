using Godot;
using Godot.Collections;

/// <summary>
/// TV 版背包面板。嵌入 PlayerTV.PanelStack 内。
/// set_mode(0=Exploration) 隐藏"使用"按钮，set_mode(1=Battle) 显示全部。
/// 物品详情通过 TooltipService 悬浮显示。
/// </summary>
[GlobalClass]
public partial class InventoryPanel : Control
{
    [Signal] public delegate void ItemUsedEventHandler(int index);
    [Signal] public delegate void ItemDiscardedEventHandler(int index);

    [Export] private Container _itemRow;
    [Export] private Button _useButton;
    [Export] private Button _discardButton;
    [Export] private Label _bottleCapLabel;
    [Export] private Label _faucetLabel;
    [Export] private Label _levelLabel;

    private int _selectedIndex = -1;
    private bool _isBattle;

    private TextureButton[] _slots = new TextureButton[DataManager.MaxItemSlots];
    private TextureRect[] _slotIcons = new TextureRect[DataManager.MaxItemSlots];
    private Texture2D _slotBg;
    private Texture2D _buttonBg;

    public override void _Ready()
    {
        _slotBg = GD.Load<Texture2D>("res://features/exploration/art_assets/workbenchUI/卡牌背景.png");
        _buttonBg = GD.Load<Texture2D>("res://features/exploration/art_assets/battlescreen/已激活.png");

        for (int i = 0; i < DataManager.MaxItemSlots; i++)
        {
            var slot = _itemRow?.GetChildOrNull<TextureButton>(i);
            if (slot == null) continue;

            int idx = i;
            slot.Pressed += () => SelectItem(idx);
            _slots[i] = slot;

            // 格子背景：卡牌底图铺满（各状态一致）
            slot.StretchMode = TextureButton.StretchModeEnum.Scale;
            slot.TextureNormal = _slotBg;
            slot.TextureHover = _slotBg;
            slot.TexturePressed = _slotBg;
            slot.TextureDisabled = _slotBg;

            // 物品图标：子 TextureRect 等比居中叠在背景上
            var icon = new TextureRect
            {
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = MouseFilterEnum.Ignore,
            };
            icon.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            icon.OffsetLeft = 10;
            icon.OffsetTop = 10;
            icon.OffsetRight = -10;
            icon.OffsetBottom = -10;
            slot.AddChild(icon);
            _slotIcons[i] = icon;
        }

        if (_useButton != null) _useButton.Pressed += OnUse;
        if (_discardButton != null) _discardButton.Pressed += OnDiscard;

        ApplyButtonBg(_useButton);
        ApplyButtonBg(_discardButton);

        DataManager.Instance.ItemBagChanged += Refresh;
        DataManager.Instance.CurrencyChanged += Refresh;
        DataManager.Instance.LevelChanged += OnLevelChanged;

        Refresh();
    }

    public override void _ExitTree()
    {
        DataManager.Instance.ItemBagChanged -= Refresh;
        DataManager.Instance.CurrencyChanged -= Refresh;
        DataManager.Instance.LevelChanged -= OnLevelChanged;
    }

    private void ApplyButtonBg(Button btn)
    {
        if (btn == null || _buttonBg == null) return;

        var normal = new StyleBoxTexture { Texture = _buttonBg };
        var disabled = new StyleBoxTexture { Texture = _buttonBg, ModulateColor = new Color(0.5f, 0.5f, 0.5f) };

        btn.AddThemeStyleboxOverride("normal", normal);
        btn.AddThemeStyleboxOverride("hover", normal);
        btn.AddThemeStyleboxOverride("pressed", normal);
        btn.AddThemeStyleboxOverride("focus", normal);
        btn.AddThemeStyleboxOverride("disabled", disabled);
    }

    public void SetMode(int mode)
    {
        _isBattle = mode == 1;
        Refresh();
    }

    public void Refresh()
    {
        if (_itemRow == null) return;

        bool hasSelection = _selectedIndex >= 0
            && DataManager.Instance.GetItem(_selectedIndex) != null;

        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null) continue;

            var item = DataManager.Instance.GetItem(i);
            var icon = _slotIcons[i];
            if (item != null)
            {
                var obj = (GodotObject)item;
                if (icon != null)
                {
                    icon.Texture = obj.Get("icon").As<Texture2D>();
                    icon.Visible = true;
                    icon.Modulate = Colors.White; // 图标颜色交由 slot 统一控制
                }

                // 选中道具：背景 + 图标一起变亮；其余调暗
                bool isSelected = hasSelection && i == _selectedIndex;
                slot.Modulate = isSelected || !hasSelection
                    ? Colors.White
                    : new Color(0.55f, 0.55f, 0.55f);

                slot.Disabled = false;
                AttachTooltip(slot, obj);
            }
            else
            {
                if (icon != null) { icon.Texture = null; icon.Visible = false; }
                slot.Modulate = Colors.White;
                slot.Disabled = true;
                TooltipService.Instance.HideFor(slot);
            }
        }

        if (_useButton != null)
        {
            _useButton.Visible = _isBattle;
            _useButton.Disabled = !hasSelection;
        }
        if (_discardButton != null)
            _discardButton.Disabled = !hasSelection;

        if (_bottleCapLabel != null)
            _bottleCapLabel.Text = DataManager.Instance.BottleCap.ToString();
        if (_faucetLabel != null)
            _faucetLabel.Text = DataManager.Instance.Faucet.ToString();
        if (_levelLabel != null)
            _levelLabel.Text = $"lv: {DataManager.Instance.Lv}";
    }

    private void OnLevelChanged(int newLevel) => Refresh();

    private void AttachTooltip(TextureButton slot, GodotObject item)
    {
        var data = new TooltipData
        {
            Title = item.Get("display_name").AsString(),
            Description = item.Get("description").AsString(),
            Icon = item.Get("icon").As<Texture2D>(),
        };
        TooltipService.Instance.ShowFor(slot, data);
    }

    private void SelectItem(int index)
    {
        if (DataManager.Instance.GetItem(index) == null) return;
        _selectedIndex = index;
        Refresh();
    }

    private void OnUse()
    {
        if (_selectedIndex >= 0)
        {
            EmitSignal(SignalName.ItemUsed, _selectedIndex);
            _selectedIndex = -1;
            Refresh();
        }
    }

    private void OnDiscard()
    {
        if (_selectedIndex >= 0)
        {
            DataManager.Instance.DiscardItem(_selectedIndex);
            EmitSignal(SignalName.ItemDiscarded, _selectedIndex);
            _selectedIndex = -1;
            Refresh();
        }
    }
}
