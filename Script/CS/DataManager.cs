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
    [Signal] public delegate void HealthChangedEventHandler(int current, int max);

    public static DataManager Instance { get; private set; }

    /// <summary>背包：卡牌 id → 卡牌数据（数量靠 CardCounts）</summary>
    public Dictionary<StringName, Resource> CardData { get; private set; } = new();
    /// <summary>背包：卡牌 id → 持有数量。归零时和 CardData 一起删除。</summary>
    public Dictionary<StringName, int> CardCounts { get; private set; } = new();
    private Array<Dictionary> _savedBuild = new();

    // ============ 道具栏（上限 4） ============
    public Array<Resource> ItemBag { get; private set; } = new();
    public const int MaxItemSlots = 4;

    /// <summary>完整道具图鉴：id → ItemData。与四格战斗携带栏分离。</summary>
    public Dictionary<StringName, Resource> ItemData { get; private set; } = new();
    /// <summary>仓库数量：id → 未携带/未消耗的数量。</summary>
    public Dictionary<StringName, int> ItemCounts { get; private set; } = new();

    private bool _mvpCatalogLoaded;
    private bool _starterCurrencyGranted;

    public static readonly string[] MvpCardPaths =
    {
        "res://card/cards/knuckle_striker.tres",
        "res://card/cards/scrap_fist.tres",
        "res://card/cards/hydraulic_fist.tres",
        "res://card/cards/rocket_fist.tres",
        "res://card/cards/quad_coil_gun.tres",
        "res://card/cards/pea_gun.tres",
        "res://card/cards/deadly_kiss.tres",
        "res://card/cards/simple_cannon.tres",
        "res://card/cards/military_power_pack.tres",
        "res://card/cards/scrap_battery.tres",
        "res://card/cards/hemostatic_pump.tres",
        "res://card/cards/mechanical_shoes.tres",
        "res://card/cards/tactical_armor.tres",
        "res://card/cards/armored_shield.tres",
    };

    public static readonly string[] MvpItemPaths =
    {
        "res://Resource/item/mvp/coolant.tres",
        "res://Resource/item/mvp/spare_battery.tres",
        "res://Resource/item/mvp/spinach_powerups.tres",
        "res://Resource/item/mvp/gasoline.tres",
        "res://Resource/item/mvp/roller_shoes.tres",
        "res://Resource/item/mvp/grenade.tres",
        "res://Resource/item/mvp/bulletproof_vest.tres",
        "res://Resource/item/mvp/particle_wall.tres",
        "res://Resource/item/mvp/ice_cream.tres",
        "res://Resource/item/mvp/medkit.tres",
    };

    // ============ 金钱 ============
    public enum CurrencyType { BottleCap, Faucet }
    public int BottleCap { get; private set; } = 0;
    public int Faucet { get; private set; } = 0;

    // ============ 等级 ============
    public int Lv { get; private set; } = 1;

    // ============ 血量（探索/战斗间继承） ============
    public int PlayerHp { get; private set; } = 30;
    public int MaxPlayerHp { get; private set; } = 30;

    public override void _Ready()
    {
        if (Instance != null) GD.PushError("DataManager: 重复实例化");
        Instance = this;
        EnsureMvpCatalogLoaded();
    }

    public override void _ExitTree()
    {
        ItemBag.Clear();
        ItemData.Clear();
        ItemCounts.Clear();
        CardData.Clear();
        CardCounts.Clear();
        _savedBuild.Clear();
        if (ReferenceEquals(Instance, this)) Instance = null;
    }

    public void EnsureMvpCatalogLoaded()
    {
        if (_mvpCatalogLoaded) return;
        _mvpCatalogLoaded = true;

        foreach (string path in MvpCardPaths)
        {
            var card = GD.Load<Resource>(path);
            if (card == null) { GD.PushError($"MVP 卡牌加载失败：{path}"); continue; }
            var id = ((GodotObject)card).Get("id").AsStringName();
            if (!CardData.ContainsKey(id)) CardData[id] = card;
            if (!CardCounts.ContainsKey(id)) CardCounts[id] = 1;
        }

        foreach (string path in MvpItemPaths)
        {
            var item = GD.Load<Resource>(path);
            if (item == null) { GD.PushError($"MVP 道具加载失败：{path}"); continue; }
            var obj = (GodotObject)item;
            var id = obj.Get("id").AsStringName();
            if (string.IsNullOrEmpty(id)) { GD.PushError($"MVP 道具缺少 id：{path}"); continue; }
            ItemData[id] = item;
            if (!ItemCounts.ContainsKey(id)) ItemCounts[id] = obj.Get("mvp_stock").AsInt32();
        }

        EmitSignal(SignalName.CardCollectionChanged);
        EmitSignal(SignalName.ItemBagChanged);
    }

    public void RegisterLegacyCardIfMissing(Resource cardResource)
    {
        if (cardResource == null) return;
        var id = ((GodotObject)cardResource).Get("id").AsStringName();
        if (string.IsNullOrEmpty(id) || CardData.ContainsKey(id)) return;
        CardData[id] = cardResource;
        CardCounts[id] = 1;
        EmitSignal(SignalName.CardCollectionChanged);
    }

    public void GrantStarterCurrencyOnce(CurrencyType type, int amount)
    {
        if (_starterCurrencyGranted) return;
        _starterCurrencyGranted = true;
        ModifyCurrency(type, amount);
    }

    // ================================================================
    //  获取卡牌
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

    public bool ConsumeCard(StringName id, int count)
    {
        if (!CardCounts.TryGetValue(id, out int c) || c < count) return false;
        c -= count;
        if (c <= 0) { CardData.Remove(id); CardCounts.Remove(id); }
        else CardCounts[id] = c;
        EmitSignal(SignalName.CardCollectionChanged);
        return true;
    }

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

    public Array<Resource> GetAllMvpItems()
    {
        EnsureMvpCatalogLoaded();
        var result = new Array<Resource>();
        foreach (string path in MvpItemPaths)
        {
            var item = GD.Load<Resource>(path);
            if (item != null) result.Add(item);
        }
        return result;
    }

    public Resource GetItemData(StringName id) =>
        ItemData.TryGetValue(id, out var item) ? item : null;

    public int GetOwnedItemCount(StringName id) =>
        ItemCounts.TryGetValue(id, out int count) ? count : 0;

    public bool EquipItem(StringName id)
    {
        if (ItemBag.Count >= MaxItemSlots) return false;
        if (!ItemData.TryGetValue(id, out var item)) return false;
        if (!ItemCounts.TryGetValue(id, out int count) || count <= 0) return false;
        ItemCounts[id] = count - 1;
        ItemBag.Add(item);
        EmitSignal(SignalName.ItemBagChanged);
        return true;
    }

    public bool UnequipItem(int index)
    {
        if (index < 0 || index >= ItemBag.Count) return false;
        var item = ItemBag[index];
        ItemBag.RemoveAt(index);
        var id = ((GodotObject)item).Get("id").AsStringName();
        ItemCounts[id] = ItemCounts.TryGetValue(id, out int count) ? count + 1 : 1;
        EmitSignal(SignalName.ItemBagChanged);
        return true;
    }

    public bool AddItem(Resource itemResource)
    {
        if (itemResource == null) return false;
        if (ItemBag.Count >= MaxItemSlots) { GD.Print("道具栏已满。"); return false; }
        var id = ((GodotObject)itemResource).Get("id").AsStringName();
        if (!string.IsNullOrEmpty(id)) ItemData[id] = itemResource;
        ItemBag.Add(itemResource);
        EmitSignal(SignalName.ItemBagChanged);
        GD.Print($"获得道具：{((GodotObject)itemResource).Get("display_name").AsString()}");
        return true;
    }

    public Resource DiscardItem(int index)
    {
        if (index < 0 || index >= ItemBag.Count) return null;
        var item = ItemBag[index];
        ItemBag.RemoveAt(index);
        EmitSignal(SignalName.ItemBagChanged);
        GD.Print($"丢弃道具：{((GodotObject)item).Get("display_name").AsString()}");
        return item;
    }

    public Resource GetItem(int index)
    {
        if (index < 0 || index >= ItemBag.Count) return null;
        return ItemBag[index];
    }

    // ================================================================
    //  金钱
    // ================================================================

    public void ModifyCurrency(CurrencyType type, int amount)
    {
        if (type == CurrencyType.BottleCap) BottleCap += amount;
        else Faucet += amount;
        EmitSignal(SignalName.CurrencyChanged);
    }

    // ================================================================
    //  规则系统
    // ================================================================

    private GodotObject _cachedRules;

    public GodotObject GetRules()
    {
        if (_cachedRules == null)
        {
            var res = GD.Load<Resource>("res://data/game_rules.tres");
            _cachedRules = res as GodotObject;
        }
        return _cachedRules;
    }

    private GodotObject _cachedLoadout;

    public GodotObject GetRecommendedLoadout()
    {
        if (_cachedLoadout == null)
            _cachedLoadout = GD.Load<GDScript>("res://Script/GD/resource/loadout_data.gd").New().As<GodotObject>();
        return _cachedLoadout;
    }

    // ================================================================
    //  等级
    // ================================================================

    public void AddLevel(int amount)
    {
        Lv += amount;
        if (Lv < 1) Lv = 1;
        EmitSignal(SignalName.LevelChanged, Lv);
        GD.Print($"等级变化：{(amount >= 0 ? "+" : "")}{amount}，当前等级 {Lv}");
    }

    // ================================================================
    //  血量
    // ================================================================

    public void ModifyHp(int amount)
    {
        PlayerHp = Mathf.Clamp(PlayerHp + amount, 0, MaxPlayerHp);
        EmitSignal(SignalName.HealthChanged, PlayerHp, MaxPlayerHp);
    }

    public void SetMaxHp(int maxHp, bool fillToMax = true)
    {
        MaxPlayerHp = Mathf.Max(1, maxHp);
        if (fillToMax) PlayerHp = MaxPlayerHp;
        else PlayerHp = Mathf.Clamp(PlayerHp, 0, MaxPlayerHp);
        EmitSignal(SignalName.HealthChanged, PlayerHp, MaxPlayerHp);
    }

    public void SetHp(int hp)
    {
        PlayerHp = Mathf.Clamp(hp, 0, MaxPlayerHp);
        EmitSignal(SignalName.HealthChanged, PlayerHp, MaxPlayerHp);
    }
}