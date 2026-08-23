using Godot;
using Godot.Collections;

/// <summary>
/// 敌对 NPC。玩家足够靠近时自动触发战斗。
/// 被打败后留下「尸体」（变灰、不触发、不响应），并通过 GameState 持久化。
/// </summary>
[GlobalClass]
public partial class HostileNPC : NPCBase
{
    /// <summary>指向战斗场景 .tscn（默认共用 battle_scene，想换背景就换这里）</summary>
    [Export] public string BattleScenePath { get; set; } = "res://Scenes/game_scene/battle_scene.tscn";
    /// <summary>指向战斗规则 .tres（如 rocky_boom_rules）</summary>
    [Export] public string BattleRulesPath { get; set; } = "";
    /// <summary>战斗结束返回地图的生成点</summary>
    [Export] public StringName ReturnSpawnId { get; set; } = "";
    /// <summary>自动触发战斗的距离阈值</summary>
    [Export] public float AggroRadius { get; set; } = 80.0f;
<<<<<<< Updated upstream
=======
    /// <summary>离开地图再回来会复活（不永久战败）；对话进度仍会持久化。</summary>
    [Export] public bool RespawnOnReturn { get; set; } = false;
    [ExportGroup("触发与剧情")]
    [Export] public bool AutoTriggerByRange { get; set; } = true;
    [Export] public bool AutoStartBattleAfterDialogue { get; set; } = true;
    [Export(PropertyHint.Range, "-1,99,1")] public int PlayerLevelAfterVictory { get; set; } = -1;
>>>>>>> Stashed changes

    [ExportGroup("音乐")]
[Export] public AudioStream BattleMusic { get; set; }

    private bool _defeated;   // 持久化：战斗胜利后变尸体
    private bool _triggered;  // 防重复触发（不持久化）

    public override void _Ready()
    {
        base._Ready();
        if (detectionShape.Shape is CircleShape2D circle)
            circle.Radius = AggroRadius;
    }

    public override void _PhysicsProcess(double delta)
    {
<<<<<<< Updated upstream
=======
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

>>>>>>> Stashed changes
        if (_triggered || _defeated || !IsPlayerInRange)
            return;

        _triggered = true;
        TriggerBattle();
    }

    public override void HandleInteract()
    {
        // 敌对 NPC 不响应点击，靠近即触发
    }

<<<<<<< Updated upstream
=======
    protected override void OnDialogueCompleted()
    {
        if (!AutoStartBattleAfterDialogue)
        {
            _battleAfterDialogue = false;
            _introPlayed = true;
            PersistInteraction(_mapId);
            return;
        }
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

>>>>>>> Stashed changes
    private void TriggerBattle()
    {
        if (string.IsNullOrEmpty(BattleRulesPath))
        {
            GD.PrintErr($"HostileNPC「{NpcName}」未设置 BattleRulesPath");
            return;
        }

        if (BattleMusic != null)
<<<<<<< Updated upstream
    {
        var audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
        audio?.PlayMusicWithFade(BattleMusic);
    }
    
        BattleDirector.Instance?.StartBattle(BattleScenePath, BattleRulesPath, PersistenceId, _mapId, ReturnSpawnId);
=======
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
>>>>>>> Stashed changes
    }

    // ================================================================
    //  持久化（尸体）
    // ================================================================

    public override Dictionary SaveState()
    {
        return new Dictionary { { "defeated", _defeated } };
    }

    public override void LoadState(Dictionary state)
    {
        if (state.TryGetValue("defeated", out var v) && v.AsBool())
        {
            _defeated = true;
            ShowCorpse();
        }
    }

    private void ShowCorpse()
    {
        // 视觉变灰 + 停止闪烁 + 禁用碰撞（尸体可穿过）
        if (sprite != null)
            sprite.Modulate = new Color(0.4f, 0.4f, 0.4f, 1f);
        SetBlinkEnabled(false);
        if (detectionShape != null) detectionShape.Disabled = true;
        if (clickShape != null) clickShape.Disabled = true;
    }

    
}
