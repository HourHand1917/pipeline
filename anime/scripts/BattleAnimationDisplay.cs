using Godot;

/// <summary>
/// Inspector-facing facade for the complete drag-and-drop animation display.
/// These settings affect presentation only; runtime combat facing and movement
/// are never written back to PlayerBattle or EnemyBattle.
/// </summary>
[GlobalClass]
public partial class BattleAnimationDisplay : Node2D
{
    [ExportGroup("Battle Track Layout")]
    [Export] public Vector2 TrackOffset { get; set; } = Vector2.Zero;

    [ExportGroup("Actor Visual Scale")]
    [Export(PropertyHint.Range, "0.1,4,0.01")]
    public float PlayerScaleMultiplier { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "0.1,4,0.01")]
    public float EnemyScaleMultiplier { get; set; } = 1.0f;

    [ExportGroup("Animation Facing")]
    [Export] public bool EnemiesAlwaysFacePlayer { get; set; } = true;
    [Export] public bool ForceEnemyFacing { get; set; } = false;
    [Export] public BattleAnimationHub.FacingDirection ForcedEnemyFacing { get; set; }
        = BattleAnimationHub.FacingDirection.Left;
    [Export] public BattleAnimationHub.FacingDirection PlayerInitialFacing { get; set; }
        = BattleAnimationHub.FacingDirection.Right;
    [Export] public BattleAnimationHub.FacingDirection EnemyInitialFacing { get; set; }
        = BattleAnimationHub.FacingDirection.Left;

    [ExportGroup("Horizontal Camera")]
    [Export] public bool CameraEnabled { get; set; } = true;
    [Export] public bool SmoothCamera { get; set; } = false;
    [Export(PropertyHint.Range, "1,30,0.5")]
    public float CameraFollowSpeed { get; set; } = 12.0f;

    public BattleAnimationHub AnimationHub { get; private set; }
    public BattleTrackCamera TrackCamera { get; private set; }
    public BattleBackdropPresenter BackdropPresenter { get; private set; }

    public override void _Ready()
    {
        AnimationHub = GetNodeOrNull<BattleAnimationHub>("BattleAnimationHub");
        TrackCamera = GetNodeOrNull<BattleTrackCamera>("BattleTrackCamera");
        BackdropPresenter = GetNodeOrNull<BattleBackdropPresenter>("BattleBackdropPresenter");
        ApplyInspectorSettings();
    }

    public void ApplyInspectorSettings()
    {
        if (AnimationHub != null)
        {
            AnimationHub.EnemiesAlwaysFacePlayer = EnemiesAlwaysFacePlayer;
            AnimationHub.ForceEnemyFacing = ForceEnemyFacing;
            AnimationHub.ForcedEnemyFacing = ForcedEnemyFacing;
            AnimationHub.PlayerInitialFacing = PlayerInitialFacing;
            AnimationHub.EnemyInitialFacing = EnemyInitialFacing;
            AnimationHub.PlayerScaleMultiplier = Mathf.Max(0.01f, PlayerScaleMultiplier);
            AnimationHub.EnemyScaleMultiplier = Mathf.Max(0.01f, EnemyScaleMultiplier);
            AnimationHub.ApplyPresentationOverrides();
        }
        if (TrackCamera != null)
        {
            TrackCamera.Enabled = CameraEnabled;
            TrackCamera.SmoothFollow = SmoothCamera;
            TrackCamera.FollowSpeed = CameraFollowSpeed;
        }
        if (BackdropPresenter != null)
            BackdropPresenter.TrackOffset = TrackOffset;
    }
}
