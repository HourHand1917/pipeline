using Godot;
using Godot.Collections;

/// <summary>
/// 敌对 NPC。玩家足够靠近时自动触发战斗。
/// 被打败后直接消失（不留尸体），并通过 GameState 持久化，重进地图不再出现。
///
/// 掉落物系统（参考 ChestInteractable）：
///   战斗胜利返回后自动弹出 TV 战利品页领取奖励；不留尸体，故无法回头补领。
/// </summary>
[GlobalClass]
public partial class HostileNPC : NPCBase, ILootSource
{
    /// <summary>指向战斗场景 .tscn（默认共用 battle_scene，想换背景就换这里）</summary>
    [Export] public string BattleScenePath { get; set; } = "res://Scenes/game_scene/battle_scene.tscn";
    /// <summary>指向战斗规则 .tres（如 boom_rules）</summary>
    [Export] public string BattleRulesPath { get; set; } = "";
    /// <summary>战斗结束返回地图的生成点</summary>
    [Export] public StringName ReturnSpawnId { get; set; } = "";
    /// <summary>自动触发战斗的距离阈值</summary>
    [Export] public float AggroRadius { get; set; } = 80.0f;
    /// <summary>离开地图再回来会复活（不永久战败）；对话进度仍会持久化。</summary>
    [Export] public bool RespawnOnReturn { get; set; } = false;

    [ExportGroup("音乐")]
    [Export] public AudioStream BattleMusic { get; set; }

    [ExportGroup("战利品")]
    /// <summary>战利品定义（GDScript LootTable 资源），设计器在 .tscn/.tres 里配。</summary>
    [Export] public Resource Loot { get; set; }
    /// <summary>探索 HUD（路由到 TV 内的 RewardPage，同 ChestInteractable → Hud 模式）。</summary>
    [Export] public ExplorationHUD Hud { get; set; }

    // ---- ILootSource ----
    public Resource RemainingLoot { get; private set; }

    private bool _defeated;   // 持久化：战斗胜利后消失（RespawnOnReturn 时离开地图会被清除）
    private bool _triggered;  // 防重复触发（不持久化）
    private bool _battleAfterDialogue;
    private bool _awaitingAggroExitAfterCancel;
    private bool _introPlayed; // 持久化：开场对话是否已播放

    public override void _Ready()
    {
        base._Ready();
        if (detectionShape.Shape is CircleShape2D circle)
            circle.Radius = AggroRadius;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (_awaitingAggroExitAfterCancel)
        {
            if (!IsPlayerInRange)
            {
                _awaitingAggroExitAfterCancel = false;
                _triggered = false;
            }
            return;
        }

        if (_triggered || _defeated || !IsPlayerInRange)
            return;

        _triggered = true;
        if (HasConfiguredDialogue && !_introPlayed)
        {
            _battleAfterDialogue = true;
            if (!TryStartConfiguredDialogue())
            {
                // 例如另一段 Dialogic 对话仍在运行：下一帧继续等待，不抢占插件状态。
                _triggered = false;
                _battleAfterDialogue = false;
            }
            return;
        }

        // 未配置 Timeline 时保持原功能：进入警戒范围立即开战。
        TriggerBattle();
    }

    // 敌对 NPC 不响应点击（靠近自动触发；打败后不留尸体，也无需二次互动）。
    public override void HandleInteract() { }

    protected override void OnDialogueSignalReceived(Variant argument)
    {
        // 可在 Dialogic 分支中放置信号 start_battle；实际切场景仍等 Timeline 正常结束。
        if ((argument.VariantType == Variant.Type.String ||
             argument.VariantType == Variant.Type.StringName) &&
            argument.AsString().Equals("start_battle", System.StringComparison.OrdinalIgnoreCase))
            _battleAfterDialogue = true;
    }

    protected override void OnDialogueCompleted()
    {
        if (_battleAfterDialogue && !_defeated)
        {
            _battleAfterDialogue = false;
            _introPlayed = true;
            PersistInteraction(_mapId); // 保存对话进度
            TriggerBattle();
        }
    }

    protected override void OnDialogueCancelled()
    {
        _battleAfterDialogue = false;
        // 敌人是自动触发；若立刻清 _triggered，玩家仍在范围内时下一物理帧
        // 会把刚关闭的对话重新打开。必须先离开警戒范围，之后才能再次触发。
        _awaitingAggroExitAfterCancel = true;
    }

    // 没有尸体，无需持久化剩余战利品或刷新尸体视觉。
    public void OnLootClaimed() { }

    // 没有尸体可回头补领：关闭战利品页时把没领完的全自动收进背包。
    public bool AutoClaimRemainderOnClose => true;

    private void TriggerBattle()
    {
        if (string.IsNullOrEmpty(BattleRulesPath))
        {
            GD.PrintErr($"HostileNPC「{NpcName}」未设置 BattleRulesPath");
            return;
        }

        if (BattleMusic != null)
        {
            var audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
            audio?.PlayMusicWithFade(BattleMusic);
        }

        BattleDirector.Instance?.StartBattle(BattleScenePath, BattleRulesPath, PersistenceId, _mapId, ReturnSpawnId);
    }

    // ================================================================
    //  持久化（打败后消失）
    // ================================================================

    public override Dictionary SaveState()
    {
        return new Dictionary
        {
            { "defeated", _defeated },
            { "intro_played", _introPlayed },
        };
    }

    public override void LoadState(Dictionary state)
    {
        if (state.TryGetValue("intro_played", out var ip) && ip.AsBool())
            _introPlayed = true;

        bool wasDefeated = state.TryGetValue("defeated", out var v) && v.AsBool();
        if (!wasDefeated) return;

        _defeated = true;
        HideDefeated();

        // 战斗胜利返回后自动弹战利品页（一次性，用 BattleDirector 标记消费掉）
        if (Loot != null && BattleDirector.Instance?.ConsumePendingLoot(PersistenceId) == true)
            Callable.From(AutoOpenReward).CallDeferred();
    }

    public override void _ExitTree()
    {
        // 可复活的敌人：离开地图时清掉「战败」状态（下次回来复活），但保留对话进度。
        if (RespawnOnReturn && _defeated &&
            !string.IsNullOrEmpty(_mapId) && !string.IsNullOrEmpty(PersistenceId))
        {
            var state = SaveState();
            state["defeated"] = false; // 复活：只清战败，保留对话进度
            GameState.Instance?.SetObjectState(_mapId, PersistenceId, state);
        }
        base._ExitTree();
    }

    private void AutoOpenReward()
    {
        EnsureRemainingLoot();
        if (HasLoot())
            Hud?.ShowReward(this);
    }

    /// <summary>不留尸体：隐藏并禁用互动。</summary>
    private void HideDefeated()
    {
        SetBlinkEnabled(false);
        Monitoring = false; // 父 Area2D 不再检测玩家靠近
        if (clickZone != null)
        {
            clickZone.Monitoring = false;
            clickZone.Monitorable = false;
        }
        if (sprite != null)
            sprite.Visible = false;
    }

    private void EnsureRemainingLoot()
    {
        if (RemainingLoot != null || Loot == null) return;
        RemainingLoot = (Resource)Loot.Duplicate(false);
    }

    private bool HasLoot() => RemainingLoot != null && !IsLootEmpty();

    private bool IsLootEmpty() =>
        ((GodotObject)RemainingLoot).Call(GDScriptKeys.LootTable.IsEmpty).AsBool();
}
