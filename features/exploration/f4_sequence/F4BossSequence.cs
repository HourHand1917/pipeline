using Godot;
using Godot.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>F4 Core-00 two-stage encounter, using the existing battles and Dialogic NPC.</summary>
[GlobalClass]
public partial class F4BossSequence : Area2D
{
    [ExportGroup("Scene references")]
    [Export] public PlayerController Player { get; set; }
    [Export] public AnimatedSprite2D LeftStageActor { get; set; }
    [Export] public AnimatedSprite2D RightStageActor { get; set; }
    [Export] public HostileNPC ForcedDialogueNpc { get; set; }
    [Export] public AnimationPlayer SequenceAnimationPlayer { get; set; }

    [ExportGroup("Phase-one defeat dialogue")]
    [Export]
    public Resource PhaseOneDefeatTimeline { get; set; }

    [ExportGroup("Phase-two defeat dialogue")]
    [Export]
    public Resource PhaseTwoDefeatTimeline { get; set; }

    [ExportGroup("Battle configuration")]
    [Export(PropertyHint.File, "*.tscn")] public string BattleScenePath { get; set; } = "res://Scenes/game_scene/boom_battle_scene.tscn";
    [Export(PropertyHint.File, "*.tres")] public string PhaseOneRulesPath { get; set; } = "res://features/enemy_ai_node/rules/core00_phase_one_rules.tres";
    [Export(PropertyHint.File, "*.tres")] public string PhaseTwoRulesPath { get; set; } = "res://features/enemy_ai_node/rules/core00_phase_two_rules.tres";
    [Export] public StringName MapId { get; set; } = "f4";
    [Export] public StringName ReturnSpawnId { get; set; } = "f4_core_return";
    [Export] public StringName PhaseOnePersistenceId { get; set; } = "f4_core00_phase_one";
    [Export] public StringName PhaseTwoPersistenceId { get; set; } = "f4_core00_phase_two";
    [Export] public StringName PhaseTwoOutroPersistenceId { get; set; } = "f4_core00_phase_two_outro";
    [Export] public AudioStream BattleMusic { get; set; }
    [Export(PropertyHint.File, "*.tscn")]
    public string PhaseTwoVictoryScenePath { get; set; } =
        "res://features/exploration/f4_sequence/ending/f4_ending_screen.tscn";

    [ExportGroup("Entry animation")]
    [Export] public StringName PhaseOneLeftAnimation { get; set; } = "enter_left";
    [Export] public StringName PhaseOneRightAnimation { get; set; } = "enter_right";
    // Phase two has no authored entrance animation yet. Leave these empty until
    // its future asset is dragged into the two stage actors in the Inspector.
    [Export] public StringName PhaseTwoLeftAnimation { get; set; } = "";
    [Export] public StringName PhaseTwoRightAnimation { get; set; } = "";
    [Export(PropertyHint.Range, "0.05,1,0.05")] public float DialogueMusicVolume { get; set; } = 0.28f;
    
    [ExportGroup("Audio")]
    [Export] public AudioStream EntrySound { get; set; }
    [Export] public int EntrySoundFrame { get; set; } = 6; // 在第6帧播放进场音效

    private bool _sequenceRunning;
    private bool _dialogueFinished;
    private bool _entrySoundPlayed = false;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
        if (ForcedDialogueNpc != null)
        {
            ForcedDialogueNpc.AutoTriggerByRange = false;
            ForcedDialogueNpc.AutoStartBattleAfterDialogue = false;
            ForcedDialogueNpc.AllowEscapeToExitDialogue = false;
            ForcedDialogueNpc.DialogueFinished += OnForcedDialogueFinished;
        }

        // 连接舞台角色的帧变化信号
        ConnectStageActorSignals();

        bool phaseOneDone = IsDefeated(PhaseOnePersistenceId);
        bool phaseTwoDone = IsDefeated(PhaseTwoPersistenceId);
        Monitoring = !phaseOneDone && !phaseTwoDone;
        Monitorable = Monitoring;
        if (phaseOneDone && !phaseTwoDone)
            Callable.From(BeginPhaseTwoInterlude).CallDeferred();
        else if (phaseTwoDone && !IsCompleted(PhaseTwoOutroPersistenceId))
            Callable.From(BeginPhaseTwoVictoryInterlude).CallDeferred();
    }

    public override void _ExitTree()
    {
        if (ForcedDialogueNpc != null && GodotObject.IsInstanceValid(ForcedDialogueNpc))
            ForcedDialogueNpc.DialogueFinished -= OnForcedDialogueFinished;
        
        DisconnectStageActorSignals();
        base._ExitTree();
    }

    /// <summary>
    /// 连接舞台角色的帧变化信号，用于在特定帧播放音效
    /// </summary>
    private void ConnectStageActorSignals()
    {
        if (LeftStageActor != null)
        {
            LeftStageActor.FrameChanged += OnStageActorFrameChanged;
        }
        if (RightStageActor != null)
        {
            RightStageActor.FrameChanged += OnStageActorFrameChanged;
        }
    }

    private void DisconnectStageActorSignals()
    {
        if (LeftStageActor != null && GodotObject.IsInstanceValid(LeftStageActor))
        {
            LeftStageActor.FrameChanged -= OnStageActorFrameChanged;
        }
        if (RightStageActor != null && GodotObject.IsInstanceValid(RightStageActor))
        {
            RightStageActor.FrameChanged -= OnStageActorFrameChanged;
        }
    }

    /// <summary>
    /// 当舞台角色动画播放到指定帧时，播放进场音效
    /// </summary>
    private void OnStageActorFrameChanged()
    {
        if (_entrySoundPlayed) return;
        if (EntrySound == null) return;
        
        // 检查是否任一舞台角色到达指定帧
        bool leftAtFrame = LeftStageActor != null && 
                          LeftStageActor.Visible && 
                          LeftStageActor.Frame == EntrySoundFrame;
        bool rightAtFrame = RightStageActor != null && 
                           RightStageActor.Visible && 
                           RightStageActor.Frame == EntrySoundFrame;
        
        if (leftAtFrame || rightAtFrame)
        {
            _entrySoundPlayed = true;
            var audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
            audio?.PlaySfx(EntrySound);
        }
    }

    private void OnBodyEntered(Node2D body)
    {
        if (_sequenceRunning || IsDefeated(PhaseOnePersistenceId) || body is not PlayerController) return;
        _ = BeginPhaseOne();
    }

    private async Task BeginPhaseOne()
    {
        if (_sequenceRunning) return;
        _sequenceRunning = true;
        _entrySoundPlayed = false; // 重置音效播放标记
        SetDeferred("monitoring", false);
        (Player ?? PlayerController.Instance)?.LockMovement();

        // 一阶段入场动画：播完再进入战斗（animation_finished 驱动）。
        await PlaySequenceAnimation("phase_one_intro");
        GetNodeOrNull<AudioManager>("/root/AudioManager")?.PlayMusicWithFade(BattleMusic);
        StartConfiguredBattle(PhaseOneRulesPath, PhaseOnePersistenceId);
    }

    private async void BeginPhaseTwoInterlude()
    {
        if (_sequenceRunning || IsDefeated(PhaseTwoPersistenceId)) return;
        _sequenceRunning = true;
        _entrySoundPlayed = false; // 重置音效播放标记
        Monitoring = false;
        Player ??= PlayerController.Instance;
        Player?.LockMovement();

        // 一阶段战败动画播完后再进入对话。
        await PlaySequenceAnimation("phase_one_defeat");

        AudioManager audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
        await PlayOptionalPhaseOneDefeatDialogue(audio);

        if (!PhaseTwoLeftAnimation.IsEmpty || !PhaseTwoRightAnimation.IsEmpty)
            await PlayStagePair(PhaseTwoLeftAnimation, PhaseTwoRightAnimation);
        audio?.SetBusVolume(AudioManager.Bus.MUSIC, 1.0f);
        audio?.PlayMusicWithFade(BattleMusic);
        StartConfiguredBattle(PhaseTwoRulesPath, PhaseTwoPersistenceId);
    }

    private void OnForcedDialogueFinished() => _dialogueFinished = true;

    /// <summary>
    /// Phase two always returns to F4 first.  Only the completed, forced
    /// post-battle dialogue is allowed to open the ending scene.
    /// </summary>
    private async void BeginPhaseTwoVictoryInterlude()
    {
        if (_sequenceRunning
            || !IsDefeated(PhaseTwoPersistenceId)
            || IsCompleted(PhaseTwoOutroPersistenceId))
            return;

        _sequenceRunning = true;
        Monitoring = false;
        Monitorable = false;
        Player ??= PlayerController.Instance;
        Player?.LockMovement();

        // 二阶段战败动画播完后再触发最终对话。
        await PlaySequenceAnimation("phase_two_defeat");

        AudioManager audio = GetNodeOrNull<AudioManager>("/root/AudioManager");
        bool dialogueCompleted = await PlayRequiredPhaseTwoDefeatDialogue(audio);
        audio?.SetBusVolume(AudioManager.Bus.MUSIC, 1.0f);
        if (!dialogueCompleted)
        {
            Player?.UnlockMovement();
            _sequenceRunning = false;
            return;
        }

        if (string.IsNullOrWhiteSpace(PhaseTwoVictoryScenePath)
            || !ResourceLoader.Exists(PhaseTwoVictoryScenePath)
            || SceneTransition.Instance == null)
        {
            GD.PushError("F4BossSequence: ending scene is unavailable after the Core-00 post-battle dialogue.");
            Player?.UnlockMovement();
            _sequenceRunning = false;
            return;
        }

        MarkCompleted(PhaseTwoOutroPersistenceId);
        SceneTransition.Instance.ChangeScene(PhaseTwoVictoryScenePath);
    }

    private async Task<bool> PlayRequiredPhaseTwoDefeatDialogue(AudioManager audio)
    {
        if (PhaseTwoDefeatTimeline == null || ForcedDialogueNpc == null)
        {
            GD.PushError("F4BossSequence: PhaseTwoDefeatTimeline is required before the ending can play.");
            return false;
        }

        ForcedDialogueNpc.DialogueTimeline = PhaseTwoDefeatTimeline;
        audio?.SetBusVolume(AudioManager.Bus.MUSIC, DialogueMusicVolume);
        _dialogueFinished = false;
        bool started = ForcedDialogueNpc.StartConfiguredDialogueNow();
        for (int retry = 0; !started && retry < 120; retry++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            started = ForcedDialogueNpc.StartConfiguredDialogueNow();
        }

        if (!started)
        {
            GD.PushError("F4BossSequence: required Core-00 post-battle dialogue could not start; ending is blocked.");
            return false;
        }

        while (!_dialogueFinished)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        return true;
    }

    /// <summary>
    /// Inspector hook for tomorrow's authored timeline.  Leaving it empty is
    /// intentionally valid: the sequence proceeds straight to phase two.
    /// </summary>
    private async Task PlayOptionalPhaseOneDefeatDialogue(AudioManager audio)
    {
        if (PhaseOneDefeatTimeline == null || ForcedDialogueNpc == null)
            return;

        ForcedDialogueNpc.DialogueTimeline = PhaseOneDefeatTimeline;
        audio?.SetBusVolume(AudioManager.Bus.MUSIC, DialogueMusicVolume);
        _dialogueFinished = false;
        bool started = ForcedDialogueNpc.StartConfiguredDialogueNow();
        for (int retry = 0; !started && retry < 120; retry++)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            started = ForcedDialogueNpc.StartConfiguredDialogueNow();
        }

        if (!started)
        {
            // Missing/temporarily invalid dialogue content must never strand
            // the boss sequence.  Designers can repair the dragged resource
            // later without touching this controller.
            GD.PushWarning("F4BossSequence: phase-one defeat dialogue could not start; continuing to phase two.");
            audio?.SetBusVolume(AudioManager.Bus.MUSIC, 1.0f);
            return;
        }

        while (!_dialogueFinished)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private async Task PlayStagePair(StringName leftAnimation, StringName rightAnimation)
    {
        float duration = Mathf.Max(0.35f, PlayStageActor(LeftStageActor, leftAnimation));
        duration = Mathf.Max(duration, PlayStageActor(RightStageActor, rightAnimation));
        await ToSignal(GetTree().CreateTimer(duration), SceneTreeTimer.SignalName.Timeout);
        HideStageActors();
    }

    private static float PlayStageActor(AnimatedSprite2D actor, StringName animation)
    {
        if (actor?.SpriteFrames == null || !actor.SpriteFrames.HasAnimation(animation)) return 0.0f;
        actor.Visible = true;
        actor.Stop();
        actor.Frame = 0;
        actor.FrameProgress = 0.0f;
        actor.Play(animation);
        int frames = actor.SpriteFrames.GetFrameCount(animation);
        double speed = actor.SpriteFrames.GetAnimationSpeed(animation);
        return speed > 0.0 ? (float)(frames / speed) : 0.35f;
    }

    private void HideStageActors()
    {
        if (LeftStageActor != null) { LeftStageActor.Stop(); LeftStageActor.Visible = false; }
        if (RightStageActor != null) { RightStageActor.Stop(); RightStageActor.Visible = false; }
    }

    /// <summary>播放序列 AnimationPlayer 动画，并等待其播完（animation_finished）。未配置动画则立即返回。</summary>
    private async Task PlaySequenceAnimation(StringName name)
    {
        if (SequenceAnimationPlayer == null || !SequenceAnimationPlayer.HasAnimation(name))
            return;
        SequenceAnimationPlayer.Play(name);
        await ToSignal(SequenceAnimationPlayer, AnimationPlayer.SignalName.AnimationFinished);
    }

    private void StartConfiguredBattle(string rulesPath, StringName encounterId)
    {
        // 失败时清空两阶段标记，玩家从头重打（不做 F4 战斗持久化）。
        BattleDirector.Instance?.RegisterBattleLostReset(ClearPhasePersistence);
        BattleDirector.Instance?.StartBattle(
            BattleScenePath,
            rulesPath,
            encounterId,
            MapId,
            ReturnSpawnId,
            this,
            -1,
            "");
    }

    /// <summary>战斗失败时清空两阶段的击败标记。</summary>
    private void ClearPhasePersistence()
    {
        GameState.Instance?.ClearObjectState(MapId.ToString(), PhaseOnePersistenceId.ToString());
        GameState.Instance?.ClearObjectState(MapId.ToString(), PhaseTwoPersistenceId.ToString());
    }

    private bool IsDefeated(StringName encounterId)
    {
        Dictionary state = GameState.Instance?.GetObjectState(MapId.ToString(), encounterId.ToString());
        return state != null && state.TryGetValue("defeated", out Variant value) && value.AsBool();
    }

    private bool IsCompleted(StringName stateId)
    {
        Dictionary state = GameState.Instance?.GetObjectState(MapId.ToString(), stateId.ToString());
        return state != null && state.TryGetValue("completed", out Variant value) && value.AsBool();
    }

    private void MarkCompleted(StringName stateId)
    {
        GameState.Instance?.SetObjectState(
            MapId.ToString(),
            stateId.ToString(),
            new Dictionary { { "completed", true } });
    }
}