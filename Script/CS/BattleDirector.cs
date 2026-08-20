using Godot;
using Godot.Collections;

/// <summary>
/// 全局战斗调度器（Autoload）。持有「待开始的战斗」和「返回信息」，
/// 解决 change_scene 会销毁场景内节点、跨场景传参必须放 Autoload 的问题。
/// </summary>
[GlobalClass]
public partial class BattleDirector : Node
{
    public static BattleDirector Instance { get; private set; }

    /// <summary>待加载的战斗 rules .tres 路径</summary>
    public string PendingRulesPath { get; private set; } = "";
    /// <summary>触发战斗的 NPC 持久化 id</summary>
    public StringName NpcPersistenceId { get; private set; } = "";
    /// <summary>NPC 所在地图（也是战斗结束返回的地图）</summary>
    public StringName NpcMapId { get; private set; } = "";
    /// <summary>战斗结束返回的生成点</summary>
    public StringName ReturnSpawnId { get; private set; } = "";
    /// <summary>刚打赢战斗的 NPC id（NPC 返回后据此一次性自动弹战利品页）</summary>
    public StringName PendingLootNpcId { get; private set; } = "";

    /// <summary>地图默认音乐（战斗结束恢复用）</summary>
    [Export] public AudioStream DefaultMapMusic { get; set; }

    public override void _Ready()
    {
        if (Instance != null) { GD.PushError("BattleDirector: 重复实例化"); return; }
        Instance = this;
    }

    /// <summary>由 NPC 调用，进入战斗场景。</summary>
    public void StartBattle(string battleScenePath, string rulesPath, StringName npcId, StringName mapId, StringName returnSpawnId)
    {
        PendingRulesPath = rulesPath;
        NpcPersistenceId = npcId;
        NpcMapId = mapId;
        ReturnSpawnId = returnSpawnId;

        SceneTransition.Instance.ChangeScene(battleScenePath);
    }

    /// <summary>战斗胜利：标记 NPC 已打败（尸体），返回探索场景。</summary>
    public void OnBattleWon()
    {
        if (!string.IsNullOrEmpty(NpcPersistenceId) && !string.IsNullOrEmpty(NpcMapId))
        {
            GameState.Instance?.SetObjectState(
                NpcMapId.ToString(), NpcPersistenceId.ToString(),
                new Dictionary { { "defeated", true } });
        }
        PendingLootNpcId = NpcPersistenceId; // 返回后 NPC 据此自动弹战利品页
        ReturnToExploration();
    }

    /// <summary>战斗失败：返回探索场景（血量已归零，后续存档系统处理死亡）。</summary>
    public void OnBattleLost()
    {
        ReturnToExploration();
    }

    /// <summary>NPC 领取「刚打赢」标记。只匹配该 NPC 一次，消费后返回 false。</summary>
    public bool ConsumePendingLoot(StringName npcId)
    {
        if (PendingLootNpcId != npcId) return false;
        PendingLootNpcId = "";
        return true;
    }

    private void ReturnToExploration()
    {
        RestoreMapMusic();
        MapManager.Instance.TravelTo(NpcMapId, ReturnSpawnId);
    }

    /// <summary>战斗结束后恢复地图音乐。</summary>
    private void RestoreMapMusic()
    {
        var audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
        audio?.PlayMusicWithFade(DefaultMapMusic);
    }
}