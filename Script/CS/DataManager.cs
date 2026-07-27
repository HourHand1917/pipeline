using Godot;
using Godot.Collections;

/// <summary>
/// 全局 Autoload。纯逻辑——图鉴、背包、构筑存档。
/// 卡牌通过 AcquireCard(Resource, count) 获取。
/// </summary>
[GlobalClass]
public partial class DataManager : Node
{
    [Signal] public delegate void BuildSavedEventHandler();
    [Signal] public delegate void CardAcquiredEventHandler(StringName cardId, int count);

    public static DataManager Instance { get; private set; }

    public Dictionary<StringName, Resource> CardDataLog { get; private set; } = new();// 图鉴：卡牌 id -> 卡牌资源
    public Dictionary<StringName, int> CardsCount { get; private set; } = new();// 背包：卡牌 id -> 拥有数量
    private Array<Dictionary> _savedBuild = new();

    public override void _Ready()
    {
        if (Instance != null) GD.PushError("DataManager: 重复实例化");
        Instance = this;
    }

    // ================================================================
    //  获取卡牌（唯一入口）
    // ================================================================

    public void AcquireCard(Resource cardResource, int count)
    {
        if (cardResource == null) { GD.PushError("AcquireCard: null"); return; }
        var obj = (GodotObject)cardResource;
        var id = obj.Get("id").AsStringName();
        if (string.IsNullOrEmpty(id)) { GD.PushError("AcquireCard: 缺少 id"); return; }

        if (!CardDataLog.ContainsKey(id))
        {
            CardDataLog[id] = cardResource;
            GD.Print($"图鉴新增：{GetCardName(id)}");
        }

        CardsCount[id] = CardsCount.TryGetValue(id, out int c) ? c + count : count;
        EmitSignal(SignalName.CardAcquired, id, count);
        GD.Print($"背包：{GetCardName(id)} +{count}（共 {CardsCount[id]} 张）");
    }

    // ================================================================
    //  消耗
    // ================================================================

    public bool ConsumeCard(StringName id, int count)
    {
        if (!CardsCount.TryGetValue(id, out int c) || c < count) return false;
        CardsCount[id] = c - count;
        if (CardsCount[id] <= 0) CardsCount.Remove(id);
        return true;
    }

    // ================================================================
    //  查询
    // ================================================================

    public bool HasCard(StringName id) => CardsCount.TryGetValue(id, out int c) && c > 0;
    public int GetCardCount(StringName id) => CardsCount.TryGetValue(id, out int c) ? c : 0;
    public Resource GetCard(StringName id) => CardDataLog.TryGetValue(id, out var c) ? c : null;

    public string GetCardName(StringName id)
    {
        var r = GetCard(id);
        return r != null ? ((GodotObject)r).Get("display_name").AsString() : id.ToString();
    }

    public Array<Resource> GetOwnedCards()
    {
        var a = new Array<Resource>();
        foreach (var (id, n) in CardsCount)
            if (n > 0 && CardDataLog.TryGetValue(id, out var c)) a.Add(c);
        return a;
    }

    public Array<Resource> GetAllCards()
    {
        var a = new Array<Resource>();
        foreach (var v in CardDataLog.Values) a.Add(v);
        return a;
    }

    // ================================================================
    //  构筑存档
    // ================================================================

    public void SaveBuild(Array<GodotObject> runtimeCards)
    {
        _savedBuild.Clear();
        foreach (var rt in runtimeCards)
        {
            _savedBuild.Add(new Dictionary
            {
                { "card_id", ((GodotObject)rt.Get("data")).Get("id").AsStringName() },
                { "anchor",  rt.Get("anchor_position").AsVector2I() },
                { "rotation", rt.Get("rotation_steps").AsInt32() }
            });
        }
        EmitSignal(SignalName.BuildSaved);
    }

    public Array<Dictionary> LoadBuild()
    {
        var a = new Array<Dictionary>();
        foreach (var e in _savedBuild) a.Add(e.Duplicate());
        return a;
    }
}
