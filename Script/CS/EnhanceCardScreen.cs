using Godot;

/// <summary>
/// 卡牌升级界面。
/// 显示背包中可升级卡牌，预览升级前后对比，消耗水龙头执行升级。
/// 
/// 临时：通过 initialCardsNode 加载初始卡牌到 DataManager。
/// 后续有存档系统后删除 LoadInitialCards 及相关节点。
/// </summary>
[GlobalClass]
public partial class EnhanceCardScreen : Control
{
    [Signal] public delegate void EnhanceScreenClosedEventHandler();

    [Export] private VBoxContainer inventoryBox;
    [Export] private Label beforeName;
    [Export] private Label afterName;
    [Export] private Label faucetLabel;
    [Export] private Button enhanceButton;
    [Export] private Button backButton;

    // ═══════════════════════════════════════════
    // 临时：初始卡牌数据节点（后续删除）
    // ═══════════════════════════════════════════
    [Export] private Node initialCardsNode;

    private Resource _selectedCard;
    private Resource _upgradedCard;
    private int _enhanceCost = 1;

    // ================================================================
    //  Ready
    // ================================================================

    public override void _Ready()
    {
        backButton.Pressed += Close;
        enhanceButton.Pressed += OnEnhancePressed;
        enhanceButton.Disabled = true;

        // 临时：加载初始卡牌
        LoadInitialCards();

        Refresh();
    }

    // ================================================================
    //  Overlay
    // ================================================================

    public void Open()
    {
        Visible = true;
        Refresh();
    }

    public void Close()
    {
        Visible = false;
        EmitSignal(SignalName.EnhanceScreenClosed);
    }

    // ================================================================
    //  临时：初始卡牌加载（后续删除）
    // ================================================================

	private void LoadInitialCards()
	{
		if (initialCardsNode == null) return;

		foreach (Node child in initialCardsNode.GetChildren())
		{
			var cardVariant = child.Get("card_resource");
			if (cardVariant.Obj == null) continue;
			var cardResource = cardVariant.As<Resource>();
			if (cardResource != null)
				DataManager.Instance.AcquireCard(cardResource, 1);
		}
	}

    // ================================================================
    //  刷新
    // ================================================================

    private void Refresh()
    {
        foreach (Node child in inventoryBox.GetChildren())
            child.QueueFree();

        faucetLabel.Text = $"💧 {DataManager.Instance.Faucet}";

        foreach (var card in DataManager.Instance.GetOwnedCards())
        {
            var obj = (GodotObject)card;
            var id = obj.Get(GDScriptKeys.CardData.Id).AsStringName();
            int count = DataManager.Instance.GetCardCount(id);
            bool isUpgraded = obj.Get("is_upgraded").AsBool();
            var upgraded = obj.Get("upgraded_version").As<Resource>();

            if (isUpgraded || upgraded == null) continue;

            var btn = new Button();
            btn.Text = $"{obj.Get(GDScriptKeys.CardData.DisplayName)}  ×{count}";
            btn.CustomMinimumSize = new Vector2(0, 56);
            btn.Pressed += () => SelectCard(card, upgraded);
            inventoryBox.AddChild(btn);
        }

        ClearSelection();
    }

    // ================================================================
    //  选择卡牌
    // ================================================================

    private void SelectCard(Resource card, Resource upgraded)
    {
        _selectedCard = card;
        _upgradedCard = upgraded;

        var obj = (GodotObject)card;
        var upObj = (GodotObject)upgraded;

        beforeName.Text = $"升级前\n{obj.Get(GDScriptKeys.CardData.DisplayName)}\n" +
                          $"类型：{obj.Get(GDScriptKeys.CardData.Category)}\n" +
                          $"{obj.Get(GDScriptKeys.CardData.Description)}";

        afterName.Text = $"升级后\n{upObj.Get(GDScriptKeys.CardData.DisplayName)}\n" +
                         $"类型：{upObj.Get(GDScriptKeys.CardData.Category)}\n" +
                         $"{upObj.Get(GDScriptKeys.CardData.Description)}";

        enhanceButton.Disabled = DataManager.Instance.Faucet < _enhanceCost;
    }

    private void ClearSelection()
    {
        _selectedCard = null;
        _upgradedCard = null;

        beforeName.Text = "升级前\n--\n从右侧选择一张卡牌";
        afterName.Text = "升级后\n--";

        enhanceButton.Disabled = true;
    }

    // ================================================================
    //  升级
    // ================================================================

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
}