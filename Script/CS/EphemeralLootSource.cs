using Godot;

/// <summary>
/// 一次性战利品源：不持久化，用于任务奖励、事件掉落等临时场景。
/// 直接包一个 LootTable 资源即可。
/// </summary>
public partial class EphemeralLootSource : RefCounted, ILootSource
{
    public Resource RemainingLoot { get; }

    public EphemeralLootSource(Resource loot) => RemainingLoot = loot;

    public void OnLootClaimed() { }
    public bool AutoClaimRemainderOnClose => false;
}
