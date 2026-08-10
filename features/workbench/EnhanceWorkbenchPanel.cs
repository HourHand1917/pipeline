using Godot;

/// <summary>
/// 工作台卡牌升级面板。背包选卡牌，左右两块面板显示升级前后对比，消耗 Faucet 升级。
/// </summary>
[GlobalClass]
public partial class EnhanceWorkbenchPanel : Control
{
    [Export] private GridContainer _inventoryBox;
    [Export] private Label _beforeText;
    [Export] private Label _afterText;
    [Export] private Label _faucetLabel;
    [Export] private TextureButton _enhanceButton;
    [Export] private Label _enhanceBtnLabel;
    [Export] private Texture2D _cardBgNormalTex;
    [Export] private Texture2D _cardBgDisabledTex;
    [Export] private Vector2 _cardCellSize = new(120, 88);

    private Resource _selectedCard;
    private Resource _upgradedCard;
    private int _enhanceCost;

    private StyleBoxTexture _cardBgNormal;
    private StyleBoxTexture _cardBgHover;
    private StyleBoxTexture _cardBgDisabled;

    public override void _Ready()
    {
        _enhanceButton.Pressed += OnEnhancePressed;
        _enhanceButton.Disabled = true;

        // 文本自动换行
        _beforeText.AutowrapMode = TextServer.AutowrapMode.Word;
        _afterText.AutowrapMode = TextServer.AutowrapMode.Word;

        DataManager.Instance.CardCollectionChanged += Refresh;
        DataManager.Instance.CurrencyChanged += Refresh;
    }

    public void Refresh()
    {
        InitStyles();
        RefreshInventory();
        RefreshCurrency();
        ClearSelection();
    }

    private void InitStyles()
    {
        if (_cardBgNormal == null && _cardBgNormalTex != null)
        {
            _cardBgNormal = new StyleBoxTexture { Texture = _cardBgNormalTex };
            _cardBgNormal.ContentMarginLeft = 4;
            _cardBgNormal.ContentMarginRight = 4;
            _cardBgNormal.ContentMarginTop = 4;
            _cardBgNormal.ContentMarginBottom = 4;

            _cardBgHover = new StyleBoxTexture { Texture = _cardBgNormalTex, ModulateColor = new Color(1.3f, 1.3f, 1.3f) };
            _cardBgHover.ContentMarginLeft = 4;
            _cardBgHover.ContentMarginRight = 4;
            _cardBgHover.ContentMarginTop = 4;
            _cardBgHover.ContentMarginBottom = 4;
        }
        if (_cardBgDisabled == null && _cardBgDisabledTex != null)
        {
            _cardBgDisabled = new StyleBoxTexture { Texture = _cardBgDisabledTex };
            _cardBgDisabled.ContentMarginLeft = 4;
            _cardBgDisabled.ContentMarginRight = 4;
            _cardBgDisabled.ContentMarginTop = 4;
            _cardBgDisabled.ContentMarginBottom = 4;
        }
    }

    private void RefreshInventory()
    {
        foreach (Node child in _inventoryBox.GetChildren())
            child.QueueFree();

        foreach (var card in DataManager.Instance.GetOwnedCards())
        {
            var obj = (GodotObject)card;
            var id = obj.Get(GDScriptKeys.CardData.Id).AsStringName();
            int count = DataManager.Instance.GetCardCount(id);
            bool isUpgraded = obj.Get("is_upgraded").AsBool();
            var upgraded = obj.Get("upgraded_version").As<Resource>();

            if (isUpgraded || upgraded == null) continue;

            int cost = obj.Get("enhance_cost").AsInt32();
            if (cost <= 0) cost = 1;

            var btn = new Button();
            btn.Text = $"{obj.Get(GDScriptKeys.CardData.IconText)}\n{obj.Get(GDScriptKeys.CardData.DisplayName)}\n×{count}  -{cost}💧";
            btn.CustomMinimumSize = _cardCellSize;

            if (_cardBgNormal != null)
            {
                btn.AddThemeStyleboxOverride("normal", _cardBgNormal);
                btn.AddThemeStyleboxOverride("hover", _cardBgHover);
            }

            btn.Pressed += () => SelectCard(card, upgraded);
            _inventoryBox.AddChild(btn);

            AttachTooltip(btn, obj);
        }
    }

    private void RefreshCurrency()
    {
        _faucetLabel.Text = $"💧 {DataManager.Instance.Faucet}";
        UpdateEnhanceLabel();
    }

    private void SelectCard(Resource card, Resource upgraded)
    {
        _selectedCard = card;
        _upgradedCard = upgraded;

        var obj = (GodotObject)card;
        var upObj = (GodotObject)upgraded;

        _enhanceCost = obj.Get("enhance_cost").AsInt32();
        if (_enhanceCost <= 0) _enhanceCost = 1;

        _beforeText.Text = $"{obj.Get(GDScriptKeys.CardData.IconText)}\n{obj.Get(GDScriptKeys.CardData.DisplayName)}\n{obj.Get(GDScriptKeys.CardData.Category)}\n\n{obj.Get(GDScriptKeys.CardData.Description)}";
        _afterText.Text = $"{upObj.Get(GDScriptKeys.CardData.IconText)}\n{upObj.Get(GDScriptKeys.CardData.DisplayName)}\n{upObj.Get(GDScriptKeys.CardData.Category)}\n\n{upObj.Get(GDScriptKeys.CardData.Description)}";

        _enhanceButton.Disabled = DataManager.Instance.Faucet < _enhanceCost;
        UpdateEnhanceLabel();
    }

    private void ClearSelection()
    {
        _selectedCard = null;
        _upgradedCard = null;

        _beforeText.Text = "选择一张卡牌";
        _afterText.Text = "查看升级结果";

        _enhanceButton.Disabled = true;
        UpdateEnhanceLabel();
    }

    private void UpdateEnhanceLabel()
    {
        if (_enhanceBtnLabel != null)
            _enhanceBtnLabel.Text = $"升级！  -{_enhanceCost} 💧";
    }

    private void OnEnhancePressed()
    {
        if (_selectedCard == null || _upgradedCard == null) return;

        var cardId = ((GodotObject)_selectedCard).Get(GDScriptKeys.CardData.Id).AsStringName();

        if (DataManager.Instance.Faucet < _enhanceCost)
        {
            GD.Print("水龙头不足！");
            return;
        }

        DataManager.Instance.ModifyCurrency(DataManager.CurrencyType.Faucet, -_enhanceCost);
        DataManager.Instance.UpgradeCard(cardId);
        Refresh();
    }

    private static void AttachTooltip(Button btn, GodotObject obj)
    {
        var data = new TooltipData
        {
            Title = obj.Get(GDScriptKeys.CardData.DisplayName).AsString(),
            Description = obj.Get(GDScriptKeys.CardData.Description).AsString(),
        };
        string range = obj.Call("range_text").AsString();
        if (!string.IsNullOrEmpty(range)) data.Details["射程"] = range;
        int cd = obj.Get(GDScriptKeys.CardData.CooldownTurns).AsInt32();
        if (cd > 0) data.Details["冷却"] = $"{cd} 回合";

        TooltipService.Instance.ShowFor(btn, data);
    }
}
