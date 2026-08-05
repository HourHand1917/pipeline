using Godot;
using Godot.Collections;

/// <summary>
/// 商店 UI。AnimationPlayer 控制开/关动画。
/// 读取 ShopData + ShopManager 渲染商品，点购买 → ShopManager.Buy()。
/// ShopEntry 是 GDScript Resource，通过 Get/Call 跨语言访问。
/// </summary>
[GlobalClass]
public partial class ShopUI : Control
{
    [Signal] public delegate void ClosedEventHandler();

    [Export] private AnimationPlayer _anim;
    [Export] private VBoxContainer _itemList;
    [Export] private Label _goldLabel;
    [Export] private Button _closeButton;
    [Export] private ShopManager _shopManager;

    public override void _Ready()
    {
        Visible = false;
        if (_shopManager != null)
            _shopManager.InventoryChanged += Refresh;
        if (_closeButton != null)
            _closeButton.Pressed += Close;
    }

    public void Open()
    {
        if (_shopManager?.ShopData == null) return;

        _anim?.Play("show_shopUI");
        Refresh();
    }

    public void Close()
    {
        _anim?.Play("hide_shopUI");
        EmitSignal(SignalName.Closed);
    }

    /// <summary>
    /// AnimationPlayer Method Track 调用：show 动画第一帧启用全部商品按钮。
    /// </summary>
    public void EnableButtons()
    {
        SetItemButtonsDisabled(false);
    }

    /// <summary>
    /// AnimationPlayer Method Track 调用：hide 动画第一帧禁用全部商品按钮，
    /// 防止动画播放期间玩家误点。
    /// </summary>
    public void DisableButtons()
    {
        SetItemButtonsDisabled(true);
    }

    private void SetItemButtonsDisabled(bool disabled)
    {
        if (_itemList == null) return;
        foreach (var child in _itemList.GetChildren())
        {
            if (child is Button btn)
                btn.Disabled = disabled;
        }
    }

    private void Refresh()
    {
        if (_shopManager?.ShopData == null) return;

        foreach (Node child in _itemList.GetChildren())
            child.QueueFree();

        if (_goldLabel != null)
            _goldLabel.Text = $"金币：{_shopManager.PlayerGold}";

        var entries = _shopManager.ShopData.Get("entries").As<Array<Resource>>();
        if (entries == null) return;

        foreach (var entry in entries)
        {
            var itemRes = entry.Get("item_res").As<Resource>();
            if (itemRes == null) continue;

            int stock = _shopManager.GetStock(itemRes);
            int price = entry.Get("price").AsInt32();
            var btn = new Button();
            btn.Disabled = stock <= 0 || _shopManager.PlayerGold < price;

            string name = entry.Call("get_display_name").AsString();
            btn.Text = stock > 0
                ? $"{name}  ${price}  剩{stock}"
                : $"{name}  已售罄";
            btn.CustomMinimumSize = new Vector2(0, 50);
            var e = entry; // capture for lambda
            btn.Pressed += () => _shopManager.Buy(e);
            _itemList.AddChild(btn);
        }
    }
}
