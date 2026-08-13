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
        // 成品战斗只使用 DataManager 的 14 张 MVP 卡牌目录。
        // Cards 继续保留为旧场景的可读配置，但不再注入旧卡，避免背包出现 14+5 张重复体系。
        DataManager.Instance.EnsureMvpCatalogLoaded();
        DataManager.Instance.GrantStarterCurrencyOnce(Currency, 60);
    }
}
