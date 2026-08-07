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
        DataManager.Instance.CurrencyChanged += Refresh;
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
        // 不能盲目全部启用——要重跑 Refresh 的逻辑，否则会覆盖售罄/背包满等状态
        Refresh();
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

    private void AttachTooltip(Button btn, Resource itemRes, int price, int stock)
    {
        var data = new TooltipData();
        data.Title = itemRes.Get("display_name").AsString();
        data.Description = itemRes.Get("description").AsString();
        var icon = itemRes.Get("icon").As<Texture2D>();
        if (icon != null) data.Icon = icon;

        data.Details = new Godot.Collections.Dictionary<string, string>
        {
            { "价格", $"${price}" },
            { "库存", $"{stock}" },
        };

        // 卡牌特有：射程
        var script = itemRes.GetScript().As<Script>();
        if (script != null && script.ResourcePath.Contains("card_data"))
        {
            var range = itemRes.Call("range_text").AsString();
            if (!string.IsNullOrEmpty(range))
                data.Details["射程"] = range;
        }

        TooltipService.Instance.ShowFor(btn, data);
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
            bool isCard = entry.Call("is_card").AsBool();
            bool bagFull = !isCard && DataManager.Instance.ItemBag.Count >= DataManager.MaxItemSlots;

            var btn = new Button();
            btn.Disabled = stock <= 0 || _shopManager.PlayerGold < price || bagFull;

            string name = entry.Call("get_display_name").AsString();
            if (stock <= 0)
                btn.Text = $"{name}  已售罄";
            else if (bagFull)
                btn.Text = $"{name}  ${price}  背包已满";
            else
                btn.Text = $"{name}  ${price}  剩{stock}";
            btn.CustomMinimumSize = new Vector2(0, 50);
            var e = entry; // capture for lambda
            btn.Pressed += () => _shopManager.Buy(e);
            AttachTooltip(btn, itemRes, price, stock);
            _itemList.AddChild(btn);
        }
    }
}
