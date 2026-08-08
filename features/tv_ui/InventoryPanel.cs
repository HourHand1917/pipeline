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

    private int _selectedIndex = -1;
    private bool _isBattle;

    private TextureButton[] _slots = new TextureButton[DataManager.MaxItemSlots];

    public override void _Ready()
    {
        for (int i = 0; i < DataManager.MaxItemSlots; i++)
        {
            var slot = _itemRow?.GetChildOrNull<TextureButton>(i);
            if (slot != null)
            {
                int idx = i;
                slot.Pressed += () => SelectItem(idx);
                _slots[i] = slot;
            }
        }

        if (_useButton != null) _useButton.Pressed += OnUse;
        if (_discardButton != null) _discardButton.Pressed += OnDiscard;

        DataManager.Instance.ItemBagChanged += Refresh;
        DataManager.Instance.CurrencyChanged += Refresh;

        Refresh();
    }

    public void SetMode(int mode)
    {
        _isBattle = mode == 1;
        Refresh();
    }

    public void Refresh()
    {
        if (_itemRow == null) return;

        for (int i = 0; i < _slots.Length; i++)
        {
            var slot = _slots[i];
            if (slot == null) continue;

            var item = DataManager.Instance.GetItem(i);
            if (item != null)
            {
                var obj = (GodotObject)item;
                slot.TextureNormal = obj.Get("icon").As<Texture2D>();
                slot.Disabled = false;
                AttachTooltip(slot, obj);
            }
            else
            {
                slot.TextureNormal = null;
                slot.Disabled = true;
                TooltipService.Instance.HideFor(slot);
            }
        }

        bool hasSelection = _selectedIndex >= 0
            && DataManager.Instance.GetItem(_selectedIndex) != null;

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
    }

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
