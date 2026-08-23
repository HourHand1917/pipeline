using Godot;
using Godot.Collections;

/// <summary>
/// 商店 UI（独立场景）。
/// 卡牌商店沿用动态网格；道具商店用静态槽位（icon / 价格 label / 瓶盖 icon 分离，可在编辑器拖动）。
/// </summary>
[GlobalClass]
public partial class ShopUI : Control
{
    [Signal] public delegate void ClosedEventHandler();

    [Export] private AnimationPlayer _anim;
    [Export] private GridContainer _cardGrid;
    [Export] private Label _goldLabel;
    [Export] private TextureButton _closeButton;
    [Export] private ShopManager _shopManager;

    /// <summary>卡片背景贴图（可选，不设则用默认按钮样式）。</summary>
    [Export] private Texture2D _cardBg;
    /// <summary>卡牌背景九宫格边距（像素）。</summary>
    [Export] private float _cardBgMargin = 10f;
    /// <summary>瓶盖图标（道具价格左侧显示）。</summary>
    [Export] private Texture2D _bottleCapIcon;

    private const int ItemSlotCount = 4;
    private readonly TextureButton[] _itemIcons = new TextureButton[ItemSlotCount];
    private readonly HBoxContainer[] _itemPriceRows = new HBoxContainer[ItemSlotCount];
    private readonly Label[] _itemPrices = new Label[ItemSlotCount];

    private const int CardSlotCount = 4;
    private readonly HBoxContainer[] _cardPriceRows = new HBoxContainer[CardSlotCount];
    private readonly Label[] _cardPrices = new Label[CardSlotCount];

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

        // 收集静态道具槽节点并绑定点击
        for (int i = 0; i < ItemSlotCount; i++)
        {
            var iconBtn = GetNodeOrNull<TextureButton>($"ItemShop/ItemIcon{i}");
            _itemIcons[i] = iconBtn;
            _itemPriceRows[i] = GetNodeOrNull<HBoxContainer>($"ItemShop/ItemPriceRow{i}");
            _itemPrices[i] = GetNodeOrNull<Label>($"ItemShop/ItemPriceRow{i}/Price");

            if (iconBtn != null)
            {
                _audio?.AttachUiSounds(iconBtn);
                int idx = i;
                iconBtn.Pressed += () => OnItemPressed(idx);
            }
        }

        // 收集卡牌价格行节点
        for (int i = 0; i < CardSlotCount; i++)
        {
            _cardPriceRows[i] = GetNodeOrNull<HBoxContainer>($"CardShop/CardPriceRow{i}");
            _cardPrices[i] = GetNodeOrNull<Label>($"CardShop/CardPriceRow{i}/Price");
        }

        // GoldLabel 现在是 HBoxContainer（Cap 瓶盖 icon + Price 数字）
        _goldLabel = GetNodeOrNull<Label>("GoldLabel/Price");
    }

    public override void _ExitTree()
    {
        DataManager.Instance.CurrencyChanged -= Refresh;
        if (_shopManager != null)
            _shopManager.TransactionResult -= OnTransactionResult;
    }

    private void OnTransactionResult(bool success, string message)
    {
        if (success) _audio?.PlayShopBuySuccess();
        else _audio?.PlayUiInvalid();
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
        if (_cardGrid != null)
            foreach (var child in _cardGrid.GetChildren())
                if (child is Button btn) btn.Disabled = disabled;
        foreach (var icon in _itemIcons)
            if (icon != null) icon.Disabled = disabled;
    }

    private void Refresh()
    {
        if (_shopManager?.ShopData == null) return;

        ClearGrid(_cardGrid);
        RefreshItems();

        if (_goldLabel != null)
            _goldLabel.Text = $"拥有瓶盖:{_shopManager.PlayerGold}";

        foreach (var entry in _shopManager.CardSlots)
            AddCard(_cardGrid, entry);

        RefreshCardPrices();
    }

    private void RefreshCardPrices()
    {
        var slots = _shopManager.CardSlots;
        for (int i = 0; i < CardSlotCount; i++)
        {
            if (_cardPriceRows[i] == null) continue;

            if (i < slots.Count)
            {
                int price = slots[i].Get("price").AsInt32();
                _cardPriceRows[i].Visible = true;
                if (_cardPrices[i] != null)
                    _cardPrices[i].Text = price.ToString();
            }
            else
            {
                _cardPriceRows[i].Visible = false;
            }
        }
    }

    // ================================================================
    //  道具商店（静态槽位）
    // ================================================================

    private void RefreshItems()
    {
        var slots = _shopManager.ItemSlots;
        bool bagFull = DataManager.Instance.ItemBag.Count >= DataManager.MaxItemSlots;

        for (int i = 0; i < ItemSlotCount; i++)
        {
            var iconBtn = _itemIcons[i];
            if (iconBtn == null) continue;

            if (i < slots.Count)
            {
                var entry = slots[i];
                var itemRes = entry.Get("item_res").As<Resource>();
                int price = entry.Get("price").AsInt32();

                iconBtn.Visible = true;
                iconBtn.TextureNormal = itemRes?.Get("icon").As<Texture2D>();
                iconBtn.Disabled = _shopManager.PlayerGold < price || bagFull;

                if (_itemPriceRows[i] != null)
                    _itemPriceRows[i].Visible = true;
                if (_itemPrices[i] != null)
                    _itemPrices[i].Text = price.ToString();

                AttachTooltip(iconBtn, itemRes, price);
            }
            else
            {
                iconBtn.Visible = false;
                if (_itemPriceRows[i] != null)
                    _itemPriceRows[i].Visible = false;
            }
        }
    }

    private void OnItemPressed(int idx)
    {
        if (_shopManager == null) return;
        var slots = _shopManager.ItemSlots;
        if (idx >= 0 && idx < slots.Count)
            _shopManager.Buy(slots[idx]);
    }

    // ================================================================
    //  卡牌商店（动态网格）
    // ================================================================

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

        var card = MakeCard(itemRes, entry);
        card.Disabled = _shopManager.PlayerGold < price;

        _audio?.AttachUiSounds(card);

        var e = entry;
        card.Pressed += () => _shopManager.Buy(e);
        AttachTooltip(card, itemRes, price);
        grid.AddChild(card);
    }

    private void AttachTooltip(Control card, Resource itemRes, int price)
    {
        var data = new TooltipData();
        data.Title = itemRes.Get("display_name").AsString();
        data.Description = itemRes.Get("description").AsString();
        var icon = itemRes.Get("icon").As<Texture2D>();
        if (icon != null) data.Icon = icon;

        data.Details = new Dictionary<string, string>
        {
            { "价格", $"{price}" },
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

    private Button MakeCard(Resource itemRes, Resource entry)
    {
        var card = new Button
        {
            CustomMinimumSize = new Vector2(0, 140),
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ExpandFill,
        };

        // 卡牌背景：九宫格，随卡片自动填满
        if (_cardBg != null)
        {
            var style = new StyleBoxTexture
            {
                Texture = _cardBg,
                TextureMarginLeft = _cardBgMargin,
                TextureMarginTop = _cardBgMargin,
                TextureMarginRight = _cardBgMargin,
                TextureMarginBottom = _cardBgMargin,
            };
            style.ContentMarginLeft = 24;
            style.ContentMarginRight = 24;
            style.ContentMarginTop = 18;
            style.ContentMarginBottom = 18;
            card.AddThemeStyleboxOverride("normal", style);
        }

        var margin = new MarginContainer();
        margin.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        margin.MouseFilter = Control.MouseFilterEnum.Ignore;

        var vbox = new VBoxContainer
        {
            Alignment = BoxContainer.AlignmentMode.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        vbox.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        vbox.AddThemeConstantOverride("separation", 8);

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
                SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter,
            };
            vbox.AddChild(icon);
        }

        string name = entry.Call("get_display_name").AsString();
        var nameLabel = new Label
        {
            Text = name,
            HorizontalAlignment = HorizontalAlignment.Center,
        };
        nameLabel.AddThemeFontSizeOverride("font_size", 24);
        nameLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        vbox.AddChild(nameLabel);

        margin.AddChild(vbox);
        card.AddChild(margin);
        return card;
    }
}
