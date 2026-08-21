using Godot;
using Godot.Collections;

/// <summary>
/// 商店 UI（独立场景）。卡片式商品展示，分两排：上排卡牌、下排物品。
/// 每个槽位是单个商品（无数量），点击即购买。
/// 读取 ShopManager 的 CardSlots / ItemSlots 渲染，点卡片 → ShopManager.Buy()。
/// </summary>
[GlobalClass]
public partial class ShopUI : Control
{
    [Signal] public delegate void ClosedEventHandler();

    [Export] private AnimationPlayer _anim;
    [Export] private GridContainer _cardGrid;
    [Export] private GridContainer _itemGrid;
    [Export] private Label _goldLabel;
    [Export] private TextureButton _closeButton;
    [Export] private ShopManager _shopManager;

    /// <summary>卡片背景贴图（可选，不设则用默认按钮样式）。</summary>
    [Export] private Texture2D _cardBg;

    private AudioManager _audio;

    public override void _Ready()
    {
        _audio = GetNodeOrNull<AudioManager>("/root/AudioManager");

        Visible = false;
        if (_shopManager != null)
        {
            _shopManager.InventoryChanged += Refresh;
            _shopManager.TransactionResult += OnTransactionResult;
        }
        DataManager.Instance.CurrencyChanged += Refresh;
        if (_closeButton != null)
        {
            _audio?.AttachUiSounds(_closeButton);
            _closeButton.Pressed += Close;
        }
    }

    public override void _ExitTree()
    {
        DataManager.Instance.CurrencyChanged -= Refresh;
        if (_shopManager != null)
            _shopManager.TransactionResult -= OnTransactionResult;
    }

    private void OnTransactionResult(bool success, string message)
    {
        if (success)
             _audio?.PlayShopBuySuccess();
        else
            _audio?.PlayUiInvalid();
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

    public void EnableButtons() => Refresh();

    public void DisableButtons() => SetCardsDisabled(true);

    private void SetCardsDisabled(bool disabled)
    {
        foreach (var grid in new[] { _cardGrid, _itemGrid })
        {
            if (grid == null) continue;
            foreach (var child in grid.GetChildren())
            {
                if (child is Button btn)
                    btn.Disabled = disabled;
            }
        }
    }

    private void Refresh()
    {
        if (_shopManager?.ShopData == null) return;

        ClearGrid(_cardGrid);
        ClearGrid(_itemGrid);

        if (_goldLabel != null)
            _goldLabel.Text = $"金币：{_shopManager.PlayerGold}";

        foreach (var entry in _shopManager.CardSlots)
            AddCard(_cardGrid, entry);

        foreach (var entry in _shopManager.ItemSlots)
            AddCard(_itemGrid, entry);
    }

    private static void ClearGrid(GridContainer grid)
    {
        if (grid == null) return;
        foreach (Node child in grid.GetChildren())
            child.QueueFree();
    }

    private void AddCard(GridContainer grid, Resource entry)
    {
        var itemRes = entry.Get("item_res").As<Resource>();
        if (itemRes == null) return;

        int price = entry.Get("price").AsInt32();
        bool isCard = entry.Call("is_card").AsBool();
        bool bagFull = !isCard && DataManager.Instance.ItemBag.Count >= DataManager.MaxItemSlots;

        var card = MakeCard(itemRes, entry, price, bagFull);
        card.Disabled = _shopManager.PlayerGold < price || bagFull;

        _audio?.AttachUiSounds(card);

        var e = entry;
        card.Pressed += () => _shopManager.Buy(e);
        AttachTooltip(card, itemRes, price);
        grid.AddChild(card);
    }

    private void AttachTooltip(Button card, Resource itemRes, int price)
    {
        var data = new TooltipData();
        data.Title = itemRes.Get("display_name").AsString();
        data.Description = itemRes.Get("description").AsString();
        var icon = itemRes.Get("icon").As<Texture2D>();
        if (icon != null) data.Icon = icon;

        data.Details = new Dictionary<string, string>
        {
            { "价格", $"${price}" },
        };

        var script = itemRes.GetScript().As<Script>();
        if (script != null && script.ResourcePath.Contains("card_data"))
        {
            var range = itemRes.Call("range_text").AsString();
            if (!string.IsNullOrEmpty(range))
                data.Details["射程"] = range;
        }

        TooltipService.Instance.ShowFor(card, data);
    }

    private Button MakeCard(Resource itemRes, Resource entry, int price, bool bagFull)
    {
        var card = new Button
        {
            CustomMinimumSize = new Vector2(0, 140),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
        };

        if (_cardBg != null)
        {
            var style = new StyleBoxTexture { Texture = _cardBg };
            style.ContentMarginLeft = 24;
            style.ContentMarginRight = 24;
            style.ContentMarginTop = 18;
            style.ContentMarginBottom = 18;
            card.AddThemeStyleboxOverride("normal", style);
        }

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;

        var hbox = new HBoxContainer();
        hbox.AddThemeConstantOverride("separation", 14);
        hbox.MouseFilter = Control.MouseFilterEnum.Ignore;

        var iconTex = itemRes.Get("icon").As<Texture2D>();
        if (iconTex != null)
        {
            var icon = new TextureRect
            {
                Texture = iconTex,
                CustomMinimumSize = new Vector2(80, 80),
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
                MouseFilter = Control.MouseFilterEnum.Ignore,
            };
            hbox.AddChild(icon);
        }

        var vbox = new VBoxContainer
        {
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };

        string name = entry.Call("get_display_name").AsString();
        var nameLabel = new Label { Text = name };
        nameLabel.AddThemeFontSizeOverride("font_size", 24);
        vbox.AddChild(nameLabel);

        string info = bagFull ? "背包已满" : $"${price}";
        var infoLabel = new Label { Text = info };
        infoLabel.AddThemeFontSizeOverride("font_size", 20);
        infoLabel.AddThemeColorOverride("font_color", new Color("#f1c453"));
        vbox.AddChild(infoLabel);

        hbox.AddChild(vbox);
        margin.AddChild(hbox);
        card.AddChild(margin);
        return card;
    }
}