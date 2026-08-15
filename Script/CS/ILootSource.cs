using Godot;

/// <summary>
/// 战利品源抽象。任何「拥有剩余战利品 + 领取后能持久化」的对象都实现它。
/// RemainingLoot 是 GDScript LootTable 资源（res://Script/GD/resource/loot_table.gd）。
/// RewardPage 只依赖这个接口，不依赖宝箱/敌人等具体类型。
/// </summary>
public interface ILootSource
{
    /// <summary>当前剩余战利品（可变引用，RewardPage 直接读写）。</summary>
    Resource RemainingLoot { get; }

    /// <summary>每次领取后的持久化回调。</summary>
    void OnLootClaimed();
}
