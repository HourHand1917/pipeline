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

    /// <summary>背包：卡牌 id → 卡牌数据（数量靠 _cardCounts）</summary>
    public Dictionary<StringName, Resource> CardData { get; private set; } = new();
    /// <summary>背包：卡牌 id → 持有数量。归零时和 CardData 一起删除。</summary>
    public Dictionary<StringName, int> CardCounts { get; private set; } = new();
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

        CardData[id] = cardResource; // 覆盖也没关系
        CardCounts[id] = CardCounts.TryGetValue(id, out int c) ? c + count : count;

        EmitSignal(SignalName.CardAcquired, id, count);
        GD.Print($"背包：{GetCardName(id)} +{count}（共 {CardCounts[id]} 张）");
    }

    // ================================================================
    //  消耗
    // ================================================================

    public bool ConsumeCard(StringName id, int count)
    {
        if (!CardCounts.TryGetValue(id, out int c) || c < count)
            return false;

        c -= count;
        if (c <= 0)
        {
            CardData.Remove(id);
            CardCounts.Remove(id);
        }
        else
        {
            CardCounts[id] = c;
        }

        return true;
    }

    // ================================================================
    //  升级
    // ================================================================

    public bool UpgradeCard(StringName cardId)
    {
        var card = GetCard(cardId);
        if (card == null) return false;

        var obj = (GodotObject)card;
        var upgradedRes = obj.Get("upgraded_version").As<Resource>();
        if (upgradedRes == null) return false;

        if (!ConsumeCard(cardId, 1)) return false;
        AcquireCard(upgradedRes, 1);

        var upgradedObj = (GodotObject)upgradedRes;
        GD.Print($"升级：{GetCardName(cardId)} → {upgradedObj.Get("display_name").AsString()}");
        return true;
    }

    // ================================================================
    //  查询
    // ================================================================

    public bool HasCard(StringName id) =>
        CardCounts.TryGetValue(id, out int c) && c > 0;

    public int GetCardCount(StringName id) =>
        CardCounts.TryGetValue(id, out int c) ? c : 0;

    public Resource GetCard(StringName id) =>
        CardData.TryGetValue(id, out var res) ? res : null;

    public string GetCardName(StringName id)
    {
        var r = GetCard(id);
        return r != null ? ((GodotObject)r).Get("display_name").AsString() : id.ToString();
    }

    /// <summary>持有至少 1 张的卡牌列表，按 ID 排序（同前缀的升级版自动跟在原版后面）</summary>
    public Array<Resource> GetOwnedCards()
    {
        var list = new System.Collections.Generic.List<Resource>();
        foreach (var (id, count) in CardCounts)
            if (count > 0 && CardData.TryGetValue(id, out var res))
                list.Add(res);

        list.Sort((a, b) =>
        {
            var idA = ((GodotObject)a).Get("id").AsString();
            var idB = ((GodotObject)b).Get("id").AsString();
            return string.Compare(idA, idB, System.StringComparison.Ordinal);
        });

        var result = new Array<Resource>();
        foreach (var r in list) result.Add(r);
        return result;
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

    public void SaveBuildWithSize(Array<GodotObject> runtimeCards, Vector2I boardSize)
    {
        _savedBuild.Clear();
        foreach (var rt in runtimeCards)
        {
            _savedBuild.Add(new Dictionary
            {
                { "card_id", ((GodotObject)rt.Get("data")).Get("id").AsStringName() },
                { "anchor",  rt.Get("anchor_position").AsVector2I() },
                { "rotation", rt.Get("rotation_steps").AsInt32() },
                { "board_size", boardSize }
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

    // ================================================================
    //  新版规则系统
    // ================================================================

    private GodotObject _cachedRules;

    public GodotObject GetRules()
    {
        if (_cachedRules == null)
        {
            _cachedRules = GD.Load<GDScript>("res://Script/GD/resource/game_rules.gd").New().As<GodotObject>();
        }
        return _cachedRules;
    }

    private GodotObject _cachedLoadout;

    public GodotObject GetRecommendedLoadout()
    {
        if (_cachedLoadout == null)
        {
            _cachedLoadout = GD.Load<GDScript>("res://Script/GD/resource/loadout_data.gd").New().As<GodotObject>();
        }
        return _cachedLoadout;
    }
}