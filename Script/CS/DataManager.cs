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
    [Signal] public delegate void CardCollectionChangedEventHandler();
    [Signal] public delegate void ItemBagChangedEventHandler();
    [Signal] public delegate void CurrencyChangedEventHandler();
    [Signal] public delegate void LevelChangedEventHandler(int newLevel);

    public static DataManager Instance { get; private set; }

    /// <summary>背包：卡牌 id → 卡牌数据（数量靠 _cardCounts）</summary>
    public Dictionary<StringName, Resource> CardData { get; private set; } = new();
    /// <summary>背包：卡牌 id → 持有数量。归零时和 CardData 一起删除。</summary>
    public Dictionary<StringName, int> CardCounts { get; private set; } = new();
    private Array<Dictionary> _savedBuild = new();

    // ============ 道具栏（上限 4） ============
    public Array<Resource> ItemBag { get; private set; } = new();
    public const int MaxItemSlots = 4;

    // ============ 金钱 ============
    public enum CurrencyType { BottleCap, Faucet }
    public int BottleCap { get; private set; } = 0;
    public int Faucet { get; private set; } = 0;

    // ============ 等级 ============
    public int Lv { get; private set; } = 1;

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

        CardData[id] = cardResource;
        CardCounts[id] = CardCounts.TryGetValue(id, out int c) ? c + count : count;

        EmitSignal(SignalName.CardAcquired, id, count);
        EmitSignal(SignalName.CardCollectionChanged);
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

        EmitSignal(SignalName.CardCollectionChanged);
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
    //  道具
    // ================================================================

    /// <summary>加入道具。失败返回 false（背包满）。</summary>
    public bool AddItem(Resource itemResource)
    {
        if (itemResource == null) return false;
        if (ItemBag.Count >= MaxItemSlots)
        {
            GD.Print("道具栏已满。");
            return false;
        }

        ItemBag.Add(itemResource);
        EmitSignal(SignalName.ItemBagChanged);
        GD.Print($"获得道具：{((GodotObject)itemResource).Get("display_name").AsString()}");
        return true;
    }

    /// <summary>丢弃道具（使用 = 丢弃）。返回被丢弃的道具资源，方便战斗层读 effects。</summary>
    public Resource DiscardItem(int index)
    {
        if (index < 0 || index >= ItemBag.Count) return null;

        var item = ItemBag[index];
        ItemBag.RemoveAt(index);
        EmitSignal(SignalName.ItemBagChanged);
        GD.Print($"丢弃道具：{((GodotObject)item).Get("display_name").AsString()}");
        return item;
    }

    /// <summary>获取道具（不删除）。</summary>
    public Resource GetItem(int index)
    {
        if (index < 0 || index >= ItemBag.Count) return null;
        return ItemBag[index];
    }

    // ================================================================
    //  金钱
    // ================================================================

    /// <summary>修改金钱。amount 可正可负。</summary>
    public void ModifyCurrency(CurrencyType type, int amount)
    {
        if (type == CurrencyType.BottleCap)
            BottleCap += amount;
        else
            Faucet += amount;
        EmitSignal(SignalName.CurrencyChanged);
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
    // ================================================================
//  等级
// ================================================================

/// <summary>增加等级。amount 可正可负。</summary>
    public void AddLevel(int amount)
    {
        Lv += amount;
        if (Lv < 1) Lv = 1;
        EmitSignal(SignalName.LevelChanged, Lv);
        GD.Print($"等级变化：{(amount >= 0 ? "+" : "")}{amount}，当前等级 {Lv}");
    }
}