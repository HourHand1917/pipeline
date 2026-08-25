using Godot;
using Godot.Collections;

/// <summary>
/// 敌对 NPC。玩家足够靠近时自动触发战斗。
/// 被打败后直接消失（不留尸体），并通过 GameState 持久化，重进地图不再出现。
/// 战斗胜利返回后可自动打开战利品页。
/// </summary>
[GlobalClass]
public partial class HostileNPC : NPCBase, ILootSource
{
    /// <summary>指向战斗场景 .tscn。</summary>
    [Export(PropertyHint.File, "*.tscn")] public string BattleScenePath { get; set; } = "res://Scenes/game_scene/battle_scene.tscn";
    /// <summary>指向战斗规则 .tres。</summary>
    [Export(PropertyHint.File, "*.tres")] public string BattleRulesPath { get; set; } = "";
    /// <summary>战斗结束返回地图的生成点。</summary>
    [Export] public StringName ReturnSpawnId { get; set; } = "";
    /// <summary>自动触发战斗的距离阈值。</summary>
    [Export] public float AggroRadius { get; set; } = 80.0f;
    /// <summary>离开地图再回来会复活；对话进度仍会持久化。</summary>
    [Export] public bool RespawnOnReturn { get; set; } = false;

    [ExportGroup("触发与剧情")]
    [Export] public bool AutoTriggerByRange { get; set; } = true;
    [Export] public bool AutoStartBattleAfterDialogue { get; set; } = true;
    [Export(PropertyHint.Range, "-1,99,1")]
    public int PlayerLevelAfterVictory { get; set; } = -1;

    [ExportGroup("音乐")]
    [Export] public AudioStream BattleMusic { get; set; }

    [ExportGroup("战利品")]
    /// <summary>战利品定义（GDScript LootTable 资源）。</summary>
    [Export] public Resource Loot { get; set; }
    /// <summary>探索 HUD，用来打开战利品页。</summary>
    [Export] public ExplorationHUD Hud { get; set; }

    public Resource RemainingLoot { get; private set; }

    private bool _defeated;
    private bool _triggered;
    private bool _battleAfterDialogue;
    private bool _awaitingAggroExitAfterCancel;
    private bool _introPlayed;

    public override void _Ready()
    {
        base._Ready();
        if (detectionShape?.Shape is CircleShape2D circle)
            circle.Radius = AggroRadius;
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!AutoTriggerByRange)
            return;

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
                // 另一段对话仍在运行时不抢占，下一物理帧继续等待。
                _triggered = false;
                _battleAfterDialogue = false;
            }
            return;
        }

        TriggerBattle();
    }

    // 敌对 NPC 不响应点击，靠近自动触发。
    public override void HandleInteract() { }

    protected override void OnDialogueSignalReceived(Variant argument)
    {
        // Dialogic 中可使用 start_battle；实际切场景仍等待 Timeline 正常结束。
        if ((argument.VariantType == Variant.Type.String ||
             argument.VariantType == Variant.Type.StringName) &&
            argument.AsString().Equals("start_battle", System.StringComparison.OrdinalIgnoreCase))
            _battleAfterDialogue = true;
    }

    protected override void OnDialogueCompleted()
    {
        _introPlayed = true;
        PersistInteraction(_mapId);

        if (!AutoStartBattleAfterDialogue)
        {
            _battleAfterDialogue = false;
            return;
        }

        if (_battleAfterDialogue && !_defeated)
        {
            _battleAfterDialogue = false;
            TriggerBattle();
        }
    }

    protected override void OnDialogueCancelled()
    {
        _battleAfterDialogue = false;
        // 必须先离开警戒范围，避免关闭对话后下一物理帧立即重新打开。
        _awaitingAggroExitAfterCancel = true;
    }

    public void OnLootClaimed() { }

    public bool AutoClaimRemainderOnClose => true;

    private void TriggerBattle()
    {
        if (string.IsNullOrEmpty(BattleScenePath))
        {
            GD.PrintErr($"HostileNPC「{NpcName}」未设置 BattleScenePath");
            _triggered = false;
            return;
        }

        if (string.IsNullOrEmpty(BattleRulesPath))
        {
            GD.PrintErr($"HostileNPC「{NpcName}」未设置 BattleRulesPath");
            _triggered = false;
            return;
        }

        if (BattleMusic != null)
        {
            var audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
            audio?.PlayMusicWithFade(BattleMusic);
        }

        BattleDirector.Instance?.StartBattle(
            BattleScenePath,
            BattleRulesPath,
            PersistenceId,
            _mapId,
            ReturnSpawnId,
            this,
            PlayerLevelAfterVictory);
    }

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
        if (state.TryGetValue("intro_played", out var intro) && intro.AsBool())
            _introPlayed = true;

        bool wasDefeated = state.TryGetValue("defeated", out var defeated) && defeated.AsBool();
        if (!wasDefeated)
            return;

        _defeated = true;
        HideDefeated();

        if (Loot != null && BattleDirector.Instance?.ConsumePendingLoot(PersistenceId) == true)
            Callable.From(AutoOpenReward).CallDeferred();
    }

    public override void _ExitTree()
    {
        if (RespawnOnReturn && _defeated &&
            !string.IsNullOrEmpty(_mapId) && !string.IsNullOrEmpty(PersistenceId))
        {
            Dictionary state = SaveState();
            state["defeated"] = false;
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

    private void HideDefeated()
    {
        SetBlinkEnabled(false);
        Monitoring = false;
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
        if (RemainingLoot != null || Loot == null)
            return;
        RemainingLoot = (Resource)Loot.Duplicate(false);
    }

    private bool HasLoot() => RemainingLoot != null && !IsLootEmpty();

    private bool IsLootEmpty() =>
        ((GodotObject)RemainingLoot).Call(GDScriptKeys.LootTable.IsEmpty).AsBool();
}