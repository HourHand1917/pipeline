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
        if (_triggered || _defeated || !IsPlayerInRange)
            return;

        _triggered = true;
        TriggerBattle();
    }

    public override void HandleInteract()
    {
        // 敌对 NPC 不响应点击，靠近即触发
    }

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
