using Godot;
using Godot.Collections;

/// <summary>
/// 商店逻辑。验证购买 + 扣款发货 + 库存管理。
/// ShopEntry / ShopData 是 GDScript Resource，通过 Get/Call 跨语言访问。
/// </summary>
[GlobalClass]
public partial class ShopManager : Node
{
    [Signal] public delegate void InventoryChangedEventHandler();
    [Signal] public delegate void TransactionResultEventHandler(bool success, string message);

    /// <summary>ShopData (GDScript Resource)</summary>
    [Export] public Resource ShopData { get; set; }
    [Export] public DataManager.CurrencyType Currency { get; set; } = DataManager.CurrencyType.BottleCap;
    [Export] public int RestockPrice { get; set; } = 50;

    private System.Collections.Generic.Dictionary<Resource, int> _stock = new();

    public int PlayerGold
    {
        get => Currency == DataManager.CurrencyType.BottleCap
            ? DataManager.Instance.BottleCap : DataManager.Instance.Faucet;
    }

    public override void _Ready()
    {
        if (ShopData != null) Restock();
    }

    public void Restock()
    {
        _stock.Clear();
        var entries = ShopData.Get("entries").As<Array<Resource>>();
        if (entries == null) return;

        foreach (var entry in entries)
        {
            if (entry == null) continue;
            var itemRes = entry.Get("item_res").As<Resource>();
            if (itemRes == null) continue;
            _stock[itemRes] = entry.Get("stock").AsInt32();
        }
        EmitSignal(SignalName.InventoryChanged);
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
        if (PlayerGold < entry.Get("price").AsInt32())
        {
            EmitSignal(SignalName.TransactionResult, false, "余额不足。");
            return;
        }

        DataManager.Instance.ModifyCurrency(Currency, -entry.Get("price").AsInt32());

        if (entry.Call("is_card").AsBool())
            DataManager.Instance.AcquireCard(itemRes, 1);
        else
            DataManager.Instance.AddItem(itemRes);

        _stock[itemRes]--;
        EmitSignal(SignalName.InventoryChanged);
        var name = entry.Call("get_display_name").AsString();
        EmitSignal(SignalName.TransactionResult, true, $"购买 {name} 成功。");
    }
}
