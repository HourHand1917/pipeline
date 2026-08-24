using Godot;
using System;

/// <summary>
/// Horizontal camera for the one-dimensional combat track.
///
/// The combatants are Controls rather than world-space bodies, so a Camera2D
/// cannot move them without also moving unrelated battle UI. This component
/// creates a clipped viewport around the existing DistanceTrack and scrolls
/// only that track. It never changes combat state, input, viewport resolution,
/// or any vertical layout value.
/// </summary>
[GlobalClass]
public partial class BattleTrackCamera : Node2D
{
    private static readonly StringName CameraOwnerMeta = "anime_track_camera_owner";

    [ExportGroup("Auto Bind")]
    [Export] public NodePath BattleManagerPath { get; set; } = "../BattleManager";
    [Export] public NodePath BattleScreenPath { get; set; } = "../BattleScreen";

    [ExportGroup("Horizontal Follow")]
    [Export] public bool Enabled { get; set; } = true;
    [Export] public bool SmoothFollow { get; set; } = false;
    [Export(PropertyHint.Range, "1,30,0.5")]
    public float FollowSpeed { get; set; } = 12.0f;
    [Export(PropertyHint.Range, "0,128,1")]
    public float EdgePadding { get; set; } = 0.0f;
    [Export] public bool CenterMapsShorterThanViewport { get; set; } = true;

    public bool IsInstalled => GodotObject.IsInstanceValid(_cameraViewport)
        && GodotObject.IsInstanceValid(_distanceTrack)
        && _distanceTrack.GetParent() == _cameraViewport;
    public float CurrentOffsetX { get; private set; }
    public float TargetOffsetX { get; private set; }
    public float ContentWidth { get; private set; }
    public float ViewportWidth => GodotObject.IsInstanceValid(_cameraViewport)
        ? _cameraViewport.Size.X
        : 0.0f;

    private BattleManager _battle;
    private BattleScreen _screen;
    private PlayerBattle _player;
    private HBoxContainer _distanceTrack;
    private Control _cameraViewport;
    private Node _originalParent;
    private int _originalIndex = -1;
    private Vector2 _originalTrackPosition;
    private bool _hasOffset;

    public override void _Process(double delta)
    {
        DiscoverRuntime();
        if (!Enabled || !EnsureCameraViewport())
            return;

        RefreshTrackLayout();
        FollowPlayer(delta);
    }

    public void SnapToPlayer()
    {
        if (!EnsureCameraViewport())
            return;
        RefreshTrackLayout();
        TargetOffsetX = CalculateTargetOffset();
        CurrentOffsetX = TargetOffsetX;
        _hasOffset = true;
        ApplyOffset();
    }

    private void DiscoverRuntime()
    {
        _battle ??= GetNodeOrNull<BattleManager>(BattleManagerPath)
            ?? FindInAncestorScopes<BattleManager>(this);
        if (_battle != null)
        {
            _player = _battle.Player ?? _player;
            _screen = _battle.BattleScreenRef ?? _screen;
        }

        _screen ??= GetNodeOrNull<BattleScreen>(BattleScreenPath)
            ?? FindInAncestorScopes<BattleScreen>(this);
        HBoxContainer discoveredTrack = _screen?.UIManager?.DistanceTrack;
        if (discoveredTrack != null && discoveredTrack != _distanceTrack)
        {
            RestoreOriginalHierarchy();
            _distanceTrack = discoveredTrack;
            _hasOffset = false;
        }
    }

    private bool EnsureCameraViewport()
    {
        if (!GodotObject.IsInstanceValid(_distanceTrack)
            || !GodotObject.IsInstanceValid(_player))
            return false;
        if (IsInstalled)
            return true;

        if (_distanceTrack.HasMeta(CameraOwnerMeta))
        {
            GodotObject owner = _distanceTrack.GetMeta(CameraOwnerMeta).AsGodotObject();
            if (GodotObject.IsInstanceValid(owner) && owner != this)
                return false;
            _distanceTrack.RemoveMeta(CameraOwnerMeta);
        }

        Node parent = _distanceTrack.GetParent();
        if (parent == null)
            return false;

        _originalParent = parent;
        _originalIndex = _distanceTrack.GetIndex();
        _originalTrackPosition = _distanceTrack.Position;

        _cameraViewport = new Control
        {
            Name = "BattleTrackCameraViewport",
            ClipContents = true,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            SizeFlagsHorizontal = Control.SizeFlags.ExpandFill,
            SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
        };
        parent.AddChild(_cameraViewport);
        parent.MoveChild(_cameraViewport, _originalIndex);

        _distanceTrack.Reparent(_cameraViewport, false);
        _distanceTrack.AnchorLeft = 0.0f;
        _distanceTrack.AnchorTop = 0.0f;
        _distanceTrack.AnchorRight = 0.0f;
        _distanceTrack.AnchorBottom = 0.0f;
        _distanceTrack.Position = Vector2.Zero;
        _distanceTrack.SetMeta(CameraOwnerMeta, this);
        CurrentOffsetX = 0.0f;
        TargetOffsetX = 0.0f;
        _hasOffset = false;
        return true;
    }

    private void RefreshTrackLayout()
    {
        if (!IsInstalled)
            return;

        Vector2 minimum = _distanceTrack.GetCombinedMinimumSize();
        ContentWidth = Mathf.Max(1.0f, minimum.X);
        float contentHeight = Mathf.Max(1.0f, minimum.Y);
        if (!Mathf.IsEqualApprox(_cameraViewport.CustomMinimumSize.Y, contentHeight))
            _cameraViewport.CustomMinimumSize = new Vector2(0.0f, contentHeight);

        float height = Mathf.Max(contentHeight, _cameraViewport.Size.Y);
        Vector2 desiredSize = new(ContentWidth, height);
        if (!_distanceTrack.Size.IsEqualApprox(desiredSize))
            _distanceTrack.Size = desiredSize;
    }

    private void FollowPlayer(double delta)
    {
        if (_cameraViewport.Size.X <= 1.0f || ContentWidth <= 1.0f)
            return;

        TargetOffsetX = CalculateTargetOffset();
        if (!_hasOffset || !SmoothFollow || FollowSpeed <= 0.0f)
        {
            CurrentOffsetX = TargetOffsetX;
            _hasOffset = true;
        }
        else
        {
            float weight = 1.0f - Mathf.Exp(-FollowSpeed * (float)delta);
            CurrentOffsetX = Mathf.Lerp(CurrentOffsetX, TargetOffsetX, weight);
            if (Mathf.Abs(CurrentOffsetX - TargetOffsetX) < 0.25f)
                CurrentOffsetX = TargetOffsetX;
        }
        ApplyOffset();
    }

    private float CalculateTargetOffset()
    {
        float viewWidth = _cameraViewport?.Size.X ?? 0.0f;
        if (viewWidth <= 0.0f)
            return 0.0f;

        if (ContentWidth <= viewWidth)
            return CenterMapsShorterThanViewport
                ? Mathf.Round((viewWidth - ContentWidth) * 0.5f)
                : 0.0f;

        TrackSlot playerSlot = FindPlayerSlot();
        if (playerSlot == null)
            return Mathf.Clamp(CurrentOffsetX, viewWidth - ContentWidth, 0.0f);

        float playerCenter = playerSlot.Position.X + playerSlot.Size.X * 0.5f;
        float desired = viewWidth * 0.5f - playerCenter;
        float leftLimit = Mathf.Min(0.0f, viewWidth - ContentWidth - EdgePadding);
        float rightLimit = Mathf.Max(0.0f, EdgePadding);
        return Mathf.Clamp(desired, leftLimit, rightLimit);
    }

    private TrackSlot FindPlayerSlot()
    {
        if (!GodotObject.IsInstanceValid(_distanceTrack)
            || !GodotObject.IsInstanceValid(_player))
            return null;

        foreach (Node child in _distanceTrack.GetChildren())
            if (child is TrackSlot exact && exact.OccupantRef == _player)
                return exact;
        foreach (Node child in _distanceTrack.GetChildren())
            if (child is TrackSlot slot && slot.CellNumber == _player.MapPosition)
                return slot;
        return null;
    }

    private void ApplyOffset()
    {
        if (!IsInstalled)
            return;
        // Horizontal camera only. Vertical position remains exactly zero.
        _distanceTrack.Position = new Vector2(Mathf.Round(CurrentOffsetX), 0.0f);
    }

    private void RestoreOriginalHierarchy()
    {
        if (!GodotObject.IsInstanceValid(_distanceTrack))
        {
            _cameraViewport = null;
            _originalParent = null;
            return;
        }

        if (_distanceTrack.HasMeta(CameraOwnerMeta))
        {
            GodotObject owner = _distanceTrack.GetMeta(CameraOwnerMeta).AsGodotObject();
            if (owner == this)
                _distanceTrack.RemoveMeta(CameraOwnerMeta);
        }

        if (GodotObject.IsInstanceValid(_originalParent)
            && !_originalParent.IsQueuedForDeletion()
            && GodotObject.IsInstanceValid(_cameraViewport)
            && _distanceTrack.GetParent() == _cameraViewport)
        {
            _distanceTrack.Reparent(_originalParent, false);
            int targetIndex = Mathf.Clamp(_originalIndex, 0, _originalParent.GetChildCount() - 1);
            _originalParent.MoveChild(_distanceTrack, targetIndex);
            _distanceTrack.Position = _originalTrackPosition;
        }

        if (GodotObject.IsInstanceValid(_cameraViewport)
            && !_cameraViewport.IsQueuedForDeletion())
            _cameraViewport.QueueFree();
        _cameraViewport = null;
        _originalParent = null;
        _originalIndex = -1;
        _hasOffset = false;
    }

    public override void _ExitTree()
    {
        RestoreOriginalHierarchy();
    }

    private static T FindDescendant<T>(Node root) where T : Node
    {
        if (root == null) return null;
        if (root is T match) return match;
        foreach (Node child in root.GetChildren())
        {
            T found = FindDescendant<T>(child);
            if (found != null) return found;
        }
        return null;
    }

    private static T FindInAncestorScopes<T>(Node start) where T : Node
    {
        Node scope = start;
        while (scope != null)
        {
            T found = FindDescendant<T>(scope);
            if (found != null) return found;
            scope = scope.GetParent();
        }
        return null;
    }
}
