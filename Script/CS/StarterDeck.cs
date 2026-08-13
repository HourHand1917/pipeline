using Godot;

/// <summary>
/// 挂到主场景的节点上。开局时把配置的卡牌注入 DataManager。
/// 每张卡在编辑器中直接拖入 .tres 文件即可。
/// </summary>
[GlobalClass]
public partial class StarterDeck : Node
{
    [Export] public Resource[] Cards { get; set; } = System.Array.Empty<Resource>();
    [Export] public DataManager.CurrencyType Currency { get; set; } = DataManager.CurrencyType.BottleCap;

    public override void _Ready()
    {
        foreach (var card in Cards)
            DataManager.Instance.AcquireCard(card, 3);
        DataManager.Instance.UpgradeCard("battery");
        DataManager.Instance.ModifyCurrency(Currency, 60);
        DataManager.Instance.ModifyCurrency(DataManager.CurrencyType.Faucet, 5);
    }
}
