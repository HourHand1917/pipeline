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
    [Export] private TextureButton _closeButton;
    [Export] private Label _title;

    private ILootSource _source;
    private bool _open;
    private Texture2D _bottleCapIcon;
    private Texture2D _faucetIcon;
    /// <summary>货币图标尺寸（正方形，像素）。太大就在 Inspector 里调小。</summary>
    [Export] private float _currencyIconSize = 32f;
    /// <summary>战利品按钮字体大小。在 Inspector 里调。</summary>
    [Export] private int _lootFontSize = 18;
    /// <summary>战利品按钮字体（仿手写）。留空则用系统楷体/行楷。</summary>
    [Export] private Font _lootFont;

    public override void _Ready()
    {
        Visible = false;
        if (_closeButton != null)
            _closeButton.Pressed += Close;

        _bottleCapIcon = GD.Load<Texture2D>("res://features/exploration/art_assets/货币/瓶盖/结算画面手绘版.png");
        _faucetIcon = GD.Load<Texture2D>("res://features/exploration/art_assets/货币/水龙头/手绘版.png");

        if (_lootFont == null)
            _lootFont = CreateHandwritingFont();

        _title?.AddThemeFontOverride("font", _lootFont);
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

        // 不留尸体的来源：关闭时把没领完的全自动收进背包（放不下的道具丢弃）。
        if (_source != null && _source.AutoClaimRemainderOnClose)
            AutoClaimRemainder();

        _anim?.Play("hide_reward");
        EmitSignal(SignalName.Closed);
    }

    /// <summary>动画 method track 调用：禁用/启用页面内所有按钮。</summary>
    public void EnableButtons(bool enable)
    {
        if (_closeButton != null) _closeButton.Disabled = !enable;
        if (_lootList != null)
            SetButtonsEnabled(_lootList, enable);
    }

    private static void SetButtonsEnabled(Node node, bool enable)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is Button btn) btn.Disabled = !enable;
            SetButtonsEnabled(child, enable);
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

        if (cap > 0) AddCurrencySlot("瓶盖", cap, ClaimBottleCap, _bottleCapIcon);
        if (faucet > 0) AddCurrencySlot("水龙头", faucet, ClaimFaucet, _faucetIcon);

        bool bagFull = DataManager.Instance.ItemBag.Count >= DataManager.MaxItemSlots;

        for (int i = 0; i < cards.Count; i++)
        {
            var card = cards[i];
            int idx = i;
            AddResourceSlot(card, $"{GetName(card)}（卡牌）", hasIcon: false, disabled: false, () => ClaimCard(idx));
        }

        for (int i = 0; i < items.Count; i++)
        {
            var item = items[i];
            int idx = i;
            string label = bagFull ? $"{GetName(item)}（道具）（背包已满）" : $"{GetName(item)}（道具）";
            AddResourceSlot(item, label, hasIcon: true, disabled: bagFull, () => ClaimItem(idx));
        }
    }

    private void AddCurrencySlot(string label, int amount, Action onClaim, Texture2D icon)
    {
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 8);

        if (icon != null)
        {
            var iconRect = new TextureRect
            {
                Texture = icon,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
                CustomMinimumSize = new Vector2(_currencyIconSize, _currencyIconSize),
                StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered,
            };
            iconRect.SizeFlagsVertical = SizeFlags.ShrinkCenter;
            row.AddChild(iconRect);
        }

        var btn = new LootButton
        {
            Text = $"{label} ×{amount}",
            CustomMinimumSize = new Vector2(0, 48),
            SizeFlagsHorizontal = SizeFlags.ExpandFill,
        };
        StyleLootButton(btn);
        btn.Pressed += () => onClaim();
        row.AddChild(btn);

        _lootList.AddChild(row);
    }

    private void AddResourceSlot(Resource res, string label, bool hasIcon, bool disabled, Action onClaim)
    {
        var btn = new LootButton();
        btn.Text = label;
        btn.Disabled = disabled;
        btn.CustomMinimumSize = new Vector2(0, 48);
        StyleLootButton(btn);
        btn.Pressed += () => onClaim();
        AttachTooltip(btn, (GodotObject)res, hasIcon);
        _lootList.AddChild(btn);
    }

    /// <summary>统一战利品按钮样式：纯文字（无背景）、仿手写字体、黑色文字。</summary>
    private void StyleLootButton(Button btn)
    {
        btn.Flat = true;
        btn.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        btn.AddThemeFontOverride("font", _lootFont);
        btn.AddThemeFontSizeOverride("font_size", _lootFontSize);
        btn.AddThemeColorOverride("font_color", Colors.Black);
        btn.AddThemeColorOverride("font_hover_color", Colors.Black);
        btn.AddThemeColorOverride("font_pressed_color", Colors.Black);
        btn.AddThemeColorOverride("font_focus_color", Colors.Black);
        btn.AddThemeColorOverride("font_disabled_color", new Color(0.35f, 0.35f, 0.35f));
    }

    /// <summary>用系统自带的中文仿手写字体（楷体/行楷），无需随项目分发字体文件。</summary>
    private static Font CreateHandwritingFont()
    {
        var sys = new SystemFont();
        sys.FontNames = new string[] { "楷体", "KaiTi", "华文行楷", "STXingkai" };
        return sys;
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

    /// <summary>
    /// 把剩余战利品全自动收进背包：货币/卡牌全收；道具受 4 格上限约束，放不下的丢弃。
    /// 仅用于不留尸体的掉落源（HostileNPC），关闭战利品页时调用。
    /// </summary>
    private void AutoClaimRemainder()
    {
        if (_source == null || _source.RemainingLoot == null) return;
        var table = (GodotObject)_source.RemainingLoot;

        // 货币全收
        int cap = table.Call(GDScriptKeys.LootTable.TakeBottleCap).AsInt32();
        if (cap > 0) DataManager.Instance.ModifyCurrency(DataManager.CurrencyType.BottleCap, cap);
        int faucet = table.Call(GDScriptKeys.LootTable.TakeFaucet).AsInt32();
        if (faucet > 0) DataManager.Instance.ModifyCurrency(DataManager.CurrencyType.Faucet, faucet);

        // 卡牌全收（无上限）
        var cards = table.Get(GDScriptKeys.LootTable.Cards).As<Godot.Collections.Array<Resource>>();
        while (cards.Count > 0)
        {
            var card = table.Call(GDScriptKeys.LootTable.TakeCard, 0).As<Resource>();
            if (card == null) break;
            DataManager.Instance.AcquireCard(card, 1);
        }

        // 道具：背包上限 4，放不下的丢弃
        var items = table.Get(GDScriptKeys.LootTable.Items).As<Godot.Collections.Array<Resource>>();
        while (items.Count > 0)
        {
            var item = table.Call(GDScriptKeys.LootTable.TakeItem, 0).As<Resource>();
            if (item == null) break;
            if (!DataManager.Instance.AddItem(item))
            {
                string name = ((GodotObject)item).Get(GDScriptKeys.ItemData.DisplayName).AsString();
                GD.Print($"背包已满，丢弃道具：{name}");
            }
        }
    }
}
