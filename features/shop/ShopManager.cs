using Godot;
using Godot.Collections;

/// <summary>
/// 商店逻辑。验证购买 + 扣款发货 + 库存管理。
/// 实现 IPersistable — 库存通过 GameState 跨场景持久化，
/// ExplorationManager 自动注入 mapId 并恢复状态。
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
    [Export] public int RestockPrice { get; set; } = 50;

    // ================================================================
    //  IPersistable
    // ================================================================

    /// <summary>跨地图持久化 ID（同一地图内唯一）</summary>
    [Export] public string PersistenceId { get; set; } = "";
    private string _mapId;

    public Dictionary SaveState()
    {
        var dict = new Dictionary();
        foreach (var kv in _stock)
            dict[kv.Key.ResourcePath] = kv.Value;
        return dict;
    }

    public void LoadState(Dictionary state)
    {
        _stock.Clear();
        foreach (var key in state.Keys)
        {
            var res = GD.Load<Resource>(key.AsString());
            if (res != null)
                _stock[res] = state[key].AsInt32();
        }
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
    }

    // ================================================================
    //  商店逻辑
    // ================================================================

    private System.Collections.Generic.Dictionary<Resource, int> _stock = new();

    public int PlayerGold
    {
        get => Currency == DataManager.CurrencyType.BottleCap
            ? DataManager.Instance.BottleCap : DataManager.Instance.Faucet;
    }

    public override void _Ready()
    {
        if (ShopData != null) LoadDefaults();
    }

    /// <summary>从 .tres 加载默认库存（InitPersistence 之后会覆盖为持久化值）</summary>
    private void LoadDefaults()
    {
        _stock.Clear();
        var entries = ShopData.Get("entries").As<Array<Resource>>();
        if (entries == null) return;

        foreach (var entry in entries)
        {
            var itemRes = entry.Get("item_res").As<Resource>();
            if (itemRes == null) continue;
            _stock[itemRes] = entry.Get("stock").AsInt32();
        }
        EmitSignal(SignalName.InventoryChanged);
    }

    /// <summary>付费补货——重置到 .tres 默认值并清除持久化记录</summary>
    public void Restock()
    {
        GameState.Instance?.ClearObjectState(_mapId, PersistenceId);
        LoadDefaults();
    }

    public int GetStock(Resource res) => _stock.TryGetValue(res, out var s) ? s : 0;

    public void Buy(Resource entry)
    {
        if (entry == null) return;
        var itemRes = entry.Get("item_res").As<Resource>();
        if (itemRes == null) return;

        if (GetStock(itemRes) <= 0)
        {
            EmitSignal(SignalName.TransactionResult, false, "已售罄。");
            return;
        }
        int price = entry.Get("price").AsInt32();
        if (PlayerGold < price)
        {
            EmitSignal(SignalName.TransactionResult, false, "余额不足。");
            return;
        }

        // 先扣钱再发货
        DataManager.Instance.ModifyCurrency(Currency, -price);

        bool delivered;
        if (entry.Call("is_card").AsBool())
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
            DataManager.Instance.ModifyCurrency(Currency, price); // 退款
            var name = entry.Call("get_display_name").AsString();
            EmitSignal(SignalName.TransactionResult, false, $"背包已满，无法购买 {name}。");
            return;
        }

        _stock[itemRes]--;
        Persist();

        EmitSignal(SignalName.InventoryChanged);
        var boughtName = entry.Call("get_display_name").AsString();
        EmitSignal(SignalName.TransactionResult, true, $"购买 {boughtName} 成功。");
    }

    private void Persist()
    {
        if (string.IsNullOrEmpty(_mapId) || string.IsNullOrEmpty(PersistenceId)) return;
        GameState.Instance?.SetObjectState(_mapId, PersistenceId, SaveState());
    }
}
