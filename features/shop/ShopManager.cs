using Godot;
using Godot.Collections;
using System.Collections.Generic;

/// <summary>
/// 商店逻辑。验证购买 + 扣款发货 + 自动补货。
///
/// 商品分两类（卡牌 / 物品），各占一行、各最多 4 个槽位，每个槽位是「单个」商品（无数量）。
/// ShopData.entries 是「商品列表」（货池）；开局从货池随机各抽 4 个摆上，
/// 某一类卖空后，再从该类货池随机抽 4 个补货。
///
/// 实现 IPersistable —— 当前摆出的卡牌/物品通过 GameState 跨场景持久化。
/// ShopEntry / ShopData 是 GDScript Resource，通过 Get/Call 跨语言访问。
/// </summary>
[GlobalClass]
public partial class ShopManager : Node, IPersistable
{
    [Signal] public delegate void InventoryChangedEventHandler();
    [Signal] public delegate void TransactionResultEventHandler(bool success, string message);

    /// <summary>ShopData (GDScript Resource)</summary>
    [Export] public Resource ShopData { get; set; }
    [Export] public DataManager.CurrencyType Currency { get; set; } = DataManager.CurrencyType.BottleCap;

    /// <summary>每类商品的槽位上限</summary>
    [Export] public int SlotCount { get; set; } = 4;

    /// <summary>跨地图持久化 ID（同一地图内唯一）</summary>
    [Export] public string PersistenceId { get; set; } = "";

    private static readonly System.Random Rng = new();

    private string _mapId;

    // 货池：从 ShopData 拆出的卡牌 / 物品条目
    private readonly List<Resource> _cardPool = new();
    private readonly List<Resource> _itemPool = new();

    // 当前摆出的槽位（每个槽位一个条目，买走即移除）
    private readonly List<Resource> _cardSlots = new();
    private readonly List<Resource> _itemSlots = new();

    public IReadOnlyList<Resource> CardSlots => _cardSlots;
    public IReadOnlyList<Resource> ItemSlots => _itemSlots;

    public int PlayerGold =>
        Currency == DataManager.CurrencyType.BottleCap
            ? DataManager.Instance.BottleCap : DataManager.Instance.Faucet;

    public override void _Ready()
    {
        if (ShopData != null) LoadDefaults();
    }

    /// <summary>从 .tres 加载货池并首次补货（InitPersistence 之后会被持久化值覆盖）。</summary>
    private void LoadDefaults()
    {
        _cardPool.Clear();
        _itemPool.Clear();
        _cardSlots.Clear();
        _itemSlots.Clear();

        var entries = ShopData.Get("entries").As<Array<Resource>>();
        if (entries == null) return;

        foreach (var entry in entries)
        {
            if (entry.Call("is_card").AsBool()) _cardPool.Add(entry);
            else _itemPool.Add(entry);
        }

        RestockCards();
        RestockItems();
        EmitSignal(SignalName.InventoryChanged);
    }

    // ================================================================
    //  补货
    // ================================================================

    private void RestockCards()
    {
        _cardSlots.Clear();
        _cardSlots.AddRange(PickRandom(_cardPool, SlotCount));
    }

    private void RestockItems()
    {
        _itemSlots.Clear();
        _itemSlots.AddRange(PickRandom(_itemPool, SlotCount));
    }

    /// <summary>从货池无放回地随机抽 n 个（n 不超过池子大小）。</summary>
    private static List<Resource> PickRandom(List<Resource> pool, int count)
    {
        var result = new List<Resource>();
        var available = new List<Resource>(pool);
        int n = Mathf.Min(count, available.Count);
        for (int i = 0; i < n; i++)
        {
            int idx = Rng.Next(available.Count);
            result.Add(available[idx]);
            available.RemoveAt(idx);
        }
        return result;
    }

    // ================================================================
    //  购买
    // ================================================================

    public void Buy(Resource entry)
    {
        if (entry == null) return;

        var itemRes = entry.Get("item_res").As<Resource>();
        if (itemRes == null) return;

        bool isCard = entry.Call("is_card").AsBool();
        var slots = isCard ? _cardSlots : _itemSlots;
        if (!slots.Contains(entry)) return; // 已被买走

        int price = entry.Get("price").AsInt32();
        if (PlayerGold < price)
        {
            EmitSignal(SignalName.TransactionResult, false, "余额不足。");
            return;
        }

        // 先扣钱再发货
        DataManager.Instance.ModifyCurrency(Currency, -price);

        bool delivered;
        if (isCard)
        {
            DataManager.Instance.AcquireCard(itemRes, 1);
            delivered = true; // 卡牌无上限
        }
        else
        {
            delivered = DataManager.Instance.AddItem(itemRes);
        }

        if (!delivered)
        {
            // 发货失败（比如背包满了），退款
            DataManager.Instance.ModifyCurrency(Currency, price);
            var name = entry.Call("get_display_name").AsString();
            EmitSignal(SignalName.TransactionResult, false, $"背包已满，无法购买 {name}。");
            return;
        }

        slots.Remove(entry);

        // 卖空 → 立即从货池随机补 4 个
        if (isCard && _cardSlots.Count == 0) RestockCards();
        else if (!isCard && _itemSlots.Count == 0) RestockItems();

        // 补货之后再存，确保补货结果也被持久化（否则卖空时存的是空货架）
        Persist();

        EmitSignal(SignalName.InventoryChanged);
        var boughtName = entry.Call("get_display_name").AsString();
        EmitSignal(SignalName.TransactionResult, true, $"购买 {boughtName} 成功。");
    }

    // ================================================================
    //  IPersistable
    // ================================================================

    public Dictionary SaveState()
    {
        var dict = new Dictionary();
        dict["cards"] = ToPathArray(_cardSlots);
        dict["items"] = ToPathArray(_itemSlots);
        return dict;
    }

    public void LoadState(Dictionary state)
    {
        if (state == null) return;

        var cardPaths = state.ContainsKey("cards") ? SaveManager.ReadStringArray(state["cards"]) : null;
        var itemPaths = state.ContainsKey("items") ? SaveManager.ReadStringArray(state["items"]) : null;

        _cardSlots.Clear();
        _cardSlots.AddRange(FromPathArray(cardPaths, _cardPool));

        _itemSlots.Clear();
        _itemSlots.AddRange(FromPathArray(itemPaths, _itemPool));
    }

    public void InitPersistence(string mapId)
    {
        _mapId = mapId;
        if (string.IsNullOrEmpty(PersistenceId) || string.IsNullOrEmpty(mapId)) return;

        var saved = GameState.Instance?.GetObjectState(mapId, PersistenceId);
        if (saved != null)
        {
            LoadState(saved);
            EmitSignal(SignalName.InventoryChanged);
        }
        else
        {
            // 首次进入：把初始随机货架也存下来，否则下次重进会重新随机
            Persist();
        }
    }

    private void Persist()
    {
        if (string.IsNullOrEmpty(_mapId) || string.IsNullOrEmpty(PersistenceId)) return;
        GameState.Instance?.SetObjectState(_mapId, PersistenceId, SaveState());
    }

    // ================================================================
    //  工具
    // ================================================================

    private static string[] ToPathArray(List<Resource> slots)
    {
        var arr = new string[slots.Count];
        for (int i = 0; i < slots.Count; i++)
        {
            var itemRes = slots[i].Get("item_res").As<Resource>();
            arr[i] = itemRes?.ResourcePath ?? "";
        }
        return arr;
    }

    private static List<Resource> FromPathArray(string[] paths, List<Resource> pool)
    {
        var result = new List<Resource>();
        if (paths == null) return result;
        foreach (var path in paths)
        {
            foreach (var entry in pool)
            {
                var itemRes = entry.Get("item_res").As<Resource>();
                if (itemRes != null && itemRes.ResourcePath == path)
                {
                    result.Add(entry);
                    break;
                }
            }
        }
        return result;
    }
}
