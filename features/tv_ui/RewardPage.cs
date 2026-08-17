using Godot;
using System;

/// <summary>
/// 战利品收集页。展示一个 ILootSource 的剩余战利品（瓶盖/水龙头/卡牌/道具），
/// 点击槽即可收入背包；全部领取完自动合上，也可手动关闭。
/// 弹出时发 RewardOpened 信号（TV 据此切到背包面板）。
/// 只依赖 ILootSource 接口，不依赖宝箱/敌人等具体类型。
/// </summary>
[GlobalClass]
public partial class RewardPage : Control
{
    [Signal] public delegate void RewardOpenedEventHandler();
    [Signal] public delegate void AllClaimedEventHandler();
    [Signal] public delegate void ClosedEventHandler();

    [Export] private AnimationPlayer _anim;
    [Export] private VBoxContainer _lootList;
    [Export] private Button _closeButton;

    private ILootSource _source;
    private bool _open;

    public override void _Ready()
    {
        Visible = false;
        if (_closeButton != null)
            _closeButton.Pressed += Close;
    }

    // ================================================================
    //  开 / 关
    // ================================================================

    public void Open(ILootSource source)
    {
        if (source == null || source.RemainingLoot == null) return;

        _source = source;
        _open = true;
        PlayerController.Instance?.LockMovement(); // 打开战利品页时锁定移动
        RebuildSlots();
        Visible = true;
        _anim?.Play("show_reward");
        EmitSignal(SignalName.RewardOpened);
    }

    public void Close()
    {
        if (!_open) return;
        _open = false;
        PlayerController.Instance?.UnlockMovement(); // 关闭战利品页时解锁移动
        _anim?.Play("hide_reward");
        EmitSignal(SignalName.Closed);
    }

    /// <summary>动画 method track 调用：禁用/启用页面内所有按钮。</summary>
    public void EnableButtons(bool enable)
    {
        if (_closeButton != null) _closeButton.Disabled = !enable;
        if (_lootList != null)
        {
            foreach (Node child in _lootList.GetChildren())
                if (child is Button btn) btn.Disabled = !enable;
        }
    }

    // ================================================================
    //  渲染
    // ================================================================

    private void RebuildSlots()
    {
        if (_lootList == null || _source == null) return;

        foreach (Node child in _lootList.GetChildren())
            child.QueueFree();

        var loot = (GodotObject)_source.RemainingLoot;
        int cap = loot.Get(GDScriptKeys.LootTable.BottleCap).AsInt32();
        int faucet = loot.Get(GDScriptKeys.LootTable.Faucet).AsInt32();
        var cards = loot.Get(GDScriptKeys.LootTable.Cards).As<Godot.Collections.Array<Resource>>();
        var items = loot.Get(GDScriptKeys.LootTable.Items).As<Godot.Collections.Array<Resource>>();

        if (cap > 0) AddCurrencySlot("瓶盖", cap, ClaimBottleCap);
        if (faucet > 0) AddCurrencySlot("水龙头", faucet, ClaimFaucet);

        bool bagFull = DataManager.Instance.ItemBag.Count >= DataManager.MaxItemSlots;

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            int idx = i;
            AddResourceSlot(card, $"卡牌 {GetName(card)}", hasIcon: false, disabled: false, () => ClaimCard(idx));
        }

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            int idx = i;
            string label = bagFull ? $"道具 {GetName(item)}（背包已满）" : $"道具 {GetName(item)}";
            AddResourceSlot(item, label, hasIcon: true, disabled: bagFull, () => ClaimItem(idx));
        }
    }

    private void AddCurrencySlot(string label, int amount, Action onClaim)
    {
        var btn = new Button();
        btn.Text = $"{label} ×{amount}";
        btn.CustomMinimumSize = new Vector2(0, 48);
        btn.Pressed += () => onClaim();
        _lootList.AddChild(btn);
    }

    private void AddResourceSlot(Resource res, string label, bool hasIcon, bool disabled, Action onClaim)
    {
        var btn = new Button();
        btn.Text = label;
        btn.Disabled = disabled;
        btn.CustomMinimumSize = new Vector2(0, 48);
        btn.Pressed += () => onClaim();
        AttachTooltip(btn, (GodotObject)res, hasIcon);
        _lootList.AddChild(btn);
    }

    // 卡牌与道具都暴露 display_name / description，字段名一致，这里统一用 CardData 的键读取。
    private string GetName(Resource res) =>
        ((GodotObject)res).Get(GDScriptKeys.CardData.DisplayName).AsString();

    private void AttachTooltip(Button btn, GodotObject res, bool hasIcon)
    {
        var data = new TooltipData
        {
            Title = res.Get(GDScriptKeys.CardData.DisplayName).AsString(),
            Description = res.Get(GDScriptKeys.CardData.Description).AsString(),
        };
        if (hasIcon)
            data.Icon = res.Get(GDScriptKeys.ItemData.Icon).As<Texture2D>();
        TooltipService.Instance.ShowFor(btn, data);
    }

    // ================================================================
    //  领取
    // ================================================================

    private void ClaimBottleCap()
    {
        var loot = (GodotObject)_source.RemainingLoot;
        int amount = loot.Call(GDScriptKeys.LootTable.TakeBottleCap).AsInt32();
        if (amount <= 0) return;
        DataManager.Instance.ModifyCurrency(DataManager.CurrencyType.BottleCap, amount);
        OnClaimed();
    }

    private void ClaimFaucet()
    {
        var loot = (GodotObject)_source.RemainingLoot;
        int amount = loot.Call(GDScriptKeys.LootTable.TakeFaucet).AsInt32();
        if (amount <= 0) return;
        DataManager.Instance.ModifyCurrency(DataManager.CurrencyType.Faucet, amount);
        OnClaimed();
    }

    private void ClaimCard(int index)
    {
        var loot = (GodotObject)_source.RemainingLoot;
        var card = loot.Call(GDScriptKeys.LootTable.TakeCard, index).As<Resource>();
        if (card == null) return;
        DataManager.Instance.AcquireCard(card, 1);
        OnClaimed();
    }

    private void ClaimItem(int index)
    {
        // 背包满：不移除、不关闭，留给玩家去丢弃道具后再来领
        if (DataManager.Instance.ItemBag.Count >= DataManager.MaxItemSlots)
        {
            GD.Print("道具背包已满，无法领取。");
            return;
        }

        var loot = (GodotObject)_source.RemainingLoot;
        var item = loot.Call(GDScriptKeys.LootTable.TakeItem, index).As<Resource>();
        if (item == null) return;
        DataManager.Instance.AddItem(item);
        OnClaimed();
    }

    private void OnClaimed()
    {
        _source?.OnLootClaimed();

        var loot = (GodotObject)_source.RemainingLoot;
        if (loot.Call(GDScriptKeys.LootTable.IsEmpty).AsBool())
        {
            Close();
            EmitSignal(SignalName.AllClaimed);
        }
        else
        {
            RebuildSlots();
        }
    }
}
