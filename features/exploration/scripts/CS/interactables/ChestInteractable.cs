using Godot;
using Godot.Collections;

/// <summary>
/// 宝箱：持有战利品（GDScript LootTable 资源）的可持久化交互物。
/// 点击打开 RewardPage 领取；未领完的战利品保留在宝箱里，通过 GameState 持久化。
/// </summary>
public partial class ChestInteractable : InteractableBase, ILootSource
{
    /// <summary>战利品定义（GDScript LootTable 资源），设计器在 .tscn/.tres 里配。</summary>
    [Export] public Resource Loot { get; set; }
    /// <summary>探索 HUD（场景里导出引用，路由到 TV 内的 RewardPage，同 WorkbenchInteractable → Hud 模式）。</summary>
    [Export] public ExplorationHUD Hud { get; set; }

    /// <summary>是否已经打开过。</summary>
    public bool IsOpened { get; private set; }

    // ---- ILootSource ----
    public Resource RemainingLoot { get; private set; }
    public void OnLootClaimed() => PersistInteraction(_mapId);

    public override void HandleInteract()
    {
        EnsureRemainingLoot();
        if (RemainingLoot == null || IsLootEmpty())
        {
            GD.Print($"「{DisplayName}」已空。");
            return;
        }

        IsOpened = true;
        PersistInteraction(_mapId);
        Hud?.ShowReward(this);
    }

    /// <summary>首次打开时复制一份运行时副本（浅拷贝：卡牌/道具引用共享，数组独立）。</summary>
    private void EnsureRemainingLoot()
    {
        if (RemainingLoot != null || Loot == null) return;
        RemainingLoot = (Resource)Loot.Duplicate(false);
    }

    private bool IsLootEmpty() =>
        ((GodotObject)RemainingLoot).Call(GDScriptKeys.LootTable.IsEmpty).AsBool();

    public override Dictionary SaveState()
    {
        var dict = new Dictionary { { "opened", IsOpened } };
        if (RemainingLoot != null)
            dict["loot"] = ((GodotObject)RemainingLoot).Call(GDScriptKeys.LootTable.ToDict);
        return dict;
    }

    public override void LoadState(Dictionary state)
    {
        if (state.TryGetValue("opened", out var v))
            IsOpened = v.AsBool();

        if (state.TryGetValue("loot", out var lootData) && Loot != null)
        {
            EnsureRemainingLoot();
            ((GodotObject)RemainingLoot).Call(GDScriptKeys.LootTable.FromDict, lootData);
        }

        if (IsOpened && (RemainingLoot == null || IsLootEmpty()))
        {
            sprite.Modulate = new Color(0.1f, 0.2f, 0.4f);
            SetBlinkEnabled(false);
        }
    }
}
