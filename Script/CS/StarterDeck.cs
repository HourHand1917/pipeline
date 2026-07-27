using Godot;

/// <summary>
/// 挂到主场景的节点上。开局时把配置的卡牌注入 DataManager。
/// 每张卡在编辑器中直接拖入 .tres 文件即可。
/// </summary>
[GlobalClass]
public partial class StarterDeck : Node
{
    [Export] public Resource[] Cards { get; set; } = System.Array.Empty<Resource>();

    public override void _Ready()
    {
        var dm = GetNode<DataManager>("/root/DataManager");
        foreach (var card in Cards)
            dm.AcquireCard(card, 1);
    }
}
