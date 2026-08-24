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
    [Export] public VideoStreamPlayer PhaseOneIntroVideo { get; set; }
    [Export] public HostileNPC ForcedDialogueNpc { get; set; }

    [ExportGroup("Phase-one intro presentation")]
    [Export]
    public Godot.Collections.Array<NodePath> HideDuringPhaseOneIntro { get; set; } = new();

    [ExportGroup("Phase-one defeat dialogue")]
    [Export]
    public Resource PhaseOneDefeatTimeline { get; set; }

    [ExportGroup("Battle configuration")]
    [Export(PropertyHint.File, "*.tscn")] public string BattleScenePath { get; set; } = "res://Scenes/game_scene/boom_battle_scene.tscn";
    [Export(PropertyHint.File, "*.tres")] public string PhaseOneRulesPath { get; set; } = "res://features/enemy_ai_node/rules/core00_phase_one_rules.tres";
    [Export(PropertyHint.File, "*.tres")] public string PhaseTwoRulesPath { get; set; } = "res://features/enemy_ai_node/rules/core00_phase_two_rules.tres";
    [Export] public StringName MapId { get; set; } = "f4";
    [Export] public StringName ReturnSpawnId { get; set; } = "f4_core_return";
    [Export] public StringName PhaseOnePersistenceId { get; set; } = "f4_core00_phase_one";
    [Export] public StringName PhaseTwoPersistenceId { get; set; } = "f4_core00_phase_two";
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

    private bool _sequenceRunning;
    private bool _dialogueFinished;
    private readonly List<(CanvasItem Item, bool WasVisible)> _introHiddenItems = new();

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

        HideStageActors();
        bool phaseOneDone = IsDefeated(PhaseOnePersistenceId);
        bool phaseTwoDone = IsDefeated(PhaseTwoPersistenceId);
        Monitoring = !phaseOneDone && !phaseTwoDone;
        Monitorable = Monitoring;
        if (phaseOneDone && !phaseTwoDone)
            Callable.From(BeginPhaseTwoInterlude).CallDeferred();
    }

    public override void _ExitTree()
    {
        EndPhaseOneIntroPresentation();
        if (ForcedDialogueNpc != null && GodotObject.IsInstanceValid(ForcedDialogueNpc))
            ForcedDialogueNpc.DialogueFinished -= OnForcedDialogueFinished;
        base._ExitTree();
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
        Monitoring = false;
        (Player ?? PlayerController.Instance)?.LockMovement();
        if (!await PlayPhaseOneIntroVideo())
            await PlayStagePair(PhaseOneLeftAnimation, PhaseOneRightAnimation);
        GetNodeOrNull<AudioManager>("/root/AudioManager")?.PlayMusicWithFade(BattleMusic);
        StartConfiguredBattle(PhaseOneRulesPath, PhaseOnePersistenceId);
    }

    private async void BeginPhaseTwoInterlude()
    {
        if (_sequenceRunning || IsDefeated(PhaseTwoPersistenceId)) return;
        _sequenceRunning = true;
        Monitoring = false;
        Player ??= PlayerController.Instance;
        Player?.LockMovement();

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

    private async Task<bool> PlayPhaseOneIntroVideo()
    {
        if (PhaseOneIntroVideo?.Stream == null)
            return false;

        BeginPhaseOneIntroPresentation();
        PhaseOneIntroVideo.Visible = true;
        PhaseOneIntroVideo.Stop();
        PhaseOneIntroVideo.Play();
        await ToSignal(PhaseOneIntroVideo, VideoStreamPlayer.SignalName.Finished);
        PhaseOneIntroVideo.Stop();
        PhaseOneIntroVideo.Visible = false;
        EndPhaseOneIntroPresentation();
        return true;
    }

    private void BeginPhaseOneIntroPresentation()
    {
        EndPhaseOneIntroPresentation();
        foreach (NodePath path in HideDuringPhaseOneIntro)
        {
            CanvasItem item = GetNodeOrNull<CanvasItem>(path);
            if (item == null || item == PhaseOneIntroVideo)
                continue;
            _introHiddenItems.Add((item, item.Visible));
            item.Visible = false;
        }
    }

    private void EndPhaseOneIntroPresentation()
    {
        foreach ((CanvasItem item, bool wasVisible) in _introHiddenItems)
        {
            if (GodotObject.IsInstanceValid(item))
                item.Visible = wasVisible;
        }
        _introHiddenItems.Clear();
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

    private void StartConfiguredBattle(string rulesPath, StringName encounterId)
    {
        string victoryScenePath = encounterId == PhaseTwoPersistenceId
            ? PhaseTwoVictoryScenePath
            : "";
        BattleDirector.Instance?.StartBattle(
            BattleScenePath,
            rulesPath,
            encounterId,
            MapId,
            ReturnSpawnId,
            this,
            -1,
            victoryScenePath);
    }

    private bool IsDefeated(StringName encounterId)
    {
        Dictionary state = GameState.Instance?.GetObjectState(MapId.ToString(), encounterId.ToString());
        return state != null && state.TryGetValue("defeated", out Variant value) && value.AsBool();
    }
}
