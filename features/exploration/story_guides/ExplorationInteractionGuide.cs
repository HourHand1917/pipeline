using Godot;

/// <summary>
/// Reusable exploration guide for a real InteractableBase. During the approach
/// step it leaves movement/input untouched and keeps a marker on the target.
/// During the interaction step it exposes only the production click area.
/// </summary>
[GlobalClass]
public partial class ExplorationInteractionGuide : CanvasLayer
{
    [Signal] public delegate void TargetReachedEventHandler();
    [Signal] public delegate void GuideCompletedEventHandler();

    public enum GuidePhase { Inactive, Approach, WaitingForDialogue, Interact, Complete }

    [ExportGroup("Guide text")]
    [Export] public string GuideName { get; set; } = "探索引导";
    [Export] public string ApproachTitle { get; set; } = "前往目标";
    [Export] public string ApproachInstruction { get; set; } = "跟随标记前往目标位置。";
    [Export] public string InteractTitle { get; set; } = "进行互动";
    [Export] public string InteractInstruction { get; set; } = "点击高亮位置继续。";

    [ExportGroup("Behaviour")]
    [Export] public bool WaitForDialogueAtTarget { get; set; }
    [Export] public bool LockMovementDuringInteraction { get; set; } = true;

    [ExportGroup("Presentation")]
    [Export(PropertyHint.Range, "0,40,1")] public float TargetPadding { get; set; } = 12f;
    [Export(PropertyHint.Range, "0,0.9,0.01")] public float DimOpacity { get; set; } = 0.58f;

    public GuidePhase Phase { get; private set; } = GuidePhase.Inactive;
    public bool IsRunning => Phase is GuidePhase.Approach or GuidePhase.WaitingForDialogue or GuidePhase.Interact;
    public InteractableBase Target { get; private set; }
    public Control CompletionControl { get; private set; }
    public Rect2 CurrentInputRect => _inputRect;

    private readonly ColorRect[] _blockers = new ColorRect[4];
    private Control _root;
    private Panel _highlight;
    private PanelContainer _guideBubble;
    private Label _stepLabel;
    private Label _titleLabel;
    private Label _bodyLabel;
    private Label _arrowLabel;
    private Rect2 _inputRect;
    private Rect2 _targetRect;
    private bool _targetSignalBound;
    private bool _movementLocked;
    private bool _targetReachedEmitted;

    public override void _Ready()
    {
        Layer = 89;
        ProcessMode = ProcessModeEnum.Always;
        CreateOverlay();
        Visible = false;
        SetProcess(true);
        SetProcessInput(true);
    }

    public override void _ExitTree()
    {
        UnbindTarget();
        UnlockMovement();
    }

    public override void _Process(double delta)
    {
        if (!IsRunning || Target == null || !IsInstanceValid(Target))
            return;

        if (Phase == GuidePhase.Approach && Target.IsPlayerInRange)
            OnTargetEnteredRange();

        if (Phase == GuidePhase.Interact && CompletionControl != null &&
            IsInstanceValid(CompletionControl) && CompletionControl.Visible)
        {
            CompleteGuide();
            return;
        }

        if (Phase is GuidePhase.Approach or GuidePhase.Interact)
        {
            ResolveTargetRect();
            LayoutOverlay();
        }
    }

    public override void _Input(InputEvent @event)
    {
        if (!Visible)
            return;

        // ESC：退出引导。对话进行中不拦截，把 ESC 留给对话系统跳行。
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.Escape)
        {
            if (Phase is GuidePhase.Approach or GuidePhase.Interact)
            {
                CompleteGuide();
                GetViewport().SetInputAsHandled();
            }
            return;
        }

        if (Phase != GuidePhase.Interact)
            return;

        if (@event is InputEventMouseButton mouse)
        {
            if (!_inputRect.HasPoint(mouse.Position))
                GetViewport().SetInputAsHandled();
            return;
        }

        if (@event is InputEventScreenTouch touch)
        {
            if (!_inputRect.HasPoint(touch.Position))
                GetViewport().SetInputAsHandled();
        }
    }

    public void Configure(InteractableBase target, Control completionControl)
    {
        if (Target == target && CompletionControl == completionControl)
            return;
        UnbindTarget();
        Target = target;
        CompletionControl = completionControl;
        BindTarget();
    }

    public void BeginGuide()
    {
        if (Target == null || !IsInstanceValid(Target))
        {
            GD.PushWarning($"{Name}: 引导没有配置目标交互物。");
            return;
        }

        _targetReachedEmitted = false;
        Phase = GuidePhase.Approach;
        Visible = true;
        SetBlockersVisible(false);
        UpdateText(true);
        ResolveTargetRect();
        LayoutOverlay();
        if (Target.IsPlayerInRange)
            Callable.From(OnTargetEnteredRange).CallDeferred();
    }

    public void ContinueToInteraction()
    {
        if (Target == null || !IsInstanceValid(Target))
            return;
        Phase = GuidePhase.Interact;
        Visible = true;
        UpdateText(false);
        if (LockMovementDuringInteraction)
            LockMovement();
        ResolveTargetRect();
        LayoutOverlay();
    }

    public void SuspendForDialogue()
    {
        if (!IsRunning) return;
        Phase = GuidePhase.WaitingForDialogue;
        Visible = false;
        SetBlockersVisible(false);
        UnlockMovement();
    }

    public void CancelGuide()
    {
        Phase = GuidePhase.Inactive;
        Visible = false;
        _inputRect = new Rect2();
        _targetRect = new Rect2();
        SetBlockersVisible(false);
        UnlockMovement();
    }

    public void CompleteGuide()
    {
        if (Phase == GuidePhase.Complete) return;
        Phase = GuidePhase.Complete;
        Visible = false;
        _inputRect = new Rect2();
        _targetRect = new Rect2();
        SetBlockersVisible(false);
        UnlockMovement();
        EmitSignal(SignalName.GuideCompleted);
    }

    public void ForceTargetReachedForTesting() => OnTargetEnteredRange();

    private void BindTarget()
    {
        if (Target == null || !IsInstanceValid(Target) || _targetSignalBound) return;
        Target.PlayerEnteredRange += OnTargetEnteredRange;
        _targetSignalBound = true;
    }

    private void UnbindTarget()
    {
        if (!_targetSignalBound || Target == null || !IsInstanceValid(Target))
        {
            _targetSignalBound = false;
            return;
        }
        Target.PlayerEnteredRange -= OnTargetEnteredRange;
        _targetSignalBound = false;
    }

    private void OnTargetEnteredRange()
    {
        if (Phase != GuidePhase.Approach || _targetReachedEmitted) return;
        _targetReachedEmitted = true;
        if (WaitForDialogueAtTarget)
            SuspendForDialogue();
        else
            ContinueToInteraction();
        EmitSignal(SignalName.TargetReached);
    }

    private void LockMovement()
    {
        if (_movementLocked) return;
        PlayerController.Instance?.LockMovement();
        _movementLocked = true;
    }

    private void UnlockMovement()
    {
        if (!_movementLocked) return;
        PlayerController.Instance?.UnlockMovement();
        _movementLocked = false;
    }

    private void ResolveTargetRect()
    {
        CollisionShape2D shape = Target?.GetNodeOrNull<CollisionShape2D>("ClickZone/ClickShape");
        Rect2 raw = GetShapeScreenRect(shape);
        Vector2 viewport = GetViewport().GetVisibleRect().Size;

        if (Phase == GuidePhase.Approach)
        {
            Vector2 center = raw.GetCenter();
            // Headless tests and embedded game windows can briefly report a
            // viewport smaller than the desired margins. Keep clamp bounds
            // ordered instead of throwing while the window is being sized.
            float horizontalMargin = Mathf.Min(42.0f, viewport.X * 0.5f);
            float topMargin = Mathf.Min(82.0f, viewport.Y * 0.5f);
            float bottomMargin = Mathf.Min(42.0f, viewport.Y * 0.5f);
            center.X = Mathf.Clamp(center.X, horizontalMargin,
                Mathf.Max(horizontalMargin, viewport.X - horizontalMargin));
            center.Y = Mathf.Clamp(center.Y, topMargin,
                Mathf.Max(topMargin, viewport.Y - bottomMargin));
            _inputRect = new Rect2(center - new Vector2(34, 34), new Vector2(68, 68));
        }
        else
        {
            _inputRect = ClampRect(raw);
        }
        _targetRect = ExpandAndClamp(_inputRect, TargetPadding);
    }

    private Rect2 GetShapeScreenRect(CollisionShape2D shapeNode)
    {
        if (shapeNode?.Shape == null || !shapeNode.IsVisibleInTree()) return new Rect2();
        Vector2 half = shapeNode.Shape switch
        {
            CircleShape2D circle => Vector2.One * circle.Radius,
            RectangleShape2D rectangle => rectangle.Size * 0.5f,
            CapsuleShape2D capsule => new Vector2(capsule.Radius, capsule.Height * 0.5f),
            _ => new Vector2(48, 48),
        };
        Transform2D transform = shapeNode.GetGlobalTransformWithCanvas();
        Vector2[] points = {
            transform * new Vector2(-half.X, -half.Y), transform * new Vector2(half.X, -half.Y),
            transform * new Vector2(half.X, half.Y), transform * new Vector2(-half.X, half.Y),
        };
        float left = points[0].X, right = points[0].X, top = points[0].Y, bottom = points[0].Y;
        foreach (Vector2 point in points)
        {
            left = Mathf.Min(left, point.X); right = Mathf.Max(right, point.X);
            top = Mathf.Min(top, point.Y); bottom = Mathf.Max(bottom, point.Y);
        }
        return new Rect2(left, top, right - left, bottom - top);
    }

    private void CreateOverlay()
    {
        _root = new Control { Name = "ExplorationGuideOverlay", MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(_root);
        _root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);

        Color dim = new(0.015f, 0.025f, 0.035f, DimOpacity);
        for (int i = 0; i < _blockers.Length; i++)
        {
            _blockers[i] = new ColorRect
            {
                Name = $"InputBlocker{i}", Color = dim,
                MouseFilter = Control.MouseFilterEnum.Stop,
            };
            _root.AddChild(_blockers[i]);
        }

        _highlight = new Panel { Name = "HighlightFrame", MouseFilter = Control.MouseFilterEnum.Ignore };
        var frame = new StyleBoxFlat
        {
            BgColor = new Color(0.2f, 0.9f, 0.95f, 0.08f), BorderColor = new Color("#74e8ec"),
            BorderWidthLeft = 5, BorderWidthTop = 5, BorderWidthRight = 5, BorderWidthBottom = 5,
            CornerRadiusTopLeft = 10, CornerRadiusTopRight = 10,
            CornerRadiusBottomLeft = 10, CornerRadiusBottomRight = 10,
        };
        _highlight.AddThemeStyleboxOverride("panel", frame);
        _root.AddChild(_highlight);

        _arrowLabel = NewLabel(36, new Color("#f1c453"));
        _arrowLabel.HorizontalAlignment = HorizontalAlignment.Center;
        _root.AddChild(_arrowLabel);

        _guideBubble = new PanelContainer
        {
            Name = "GuideBubble", CustomMinimumSize = new Vector2(490, 142),
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        var bubble = new StyleBoxFlat
        {
            BgColor = new Color("#101b20f2"), BorderColor = new Color("#74e8ec"),
            BorderWidthLeft = 3, BorderWidthTop = 3, BorderWidthRight = 3, BorderWidthBottom = 3,
            CornerRadiusTopLeft = 12, CornerRadiusTopRight = 12,
            CornerRadiusBottomLeft = 12, CornerRadiusBottomRight = 12,
            ContentMarginLeft = 24, ContentMarginRight = 24,
            ContentMarginTop = 16, ContentMarginBottom = 16,
        };
        _guideBubble.AddThemeStyleboxOverride("panel", bubble);
        _root.AddChild(_guideBubble);

        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 5);
        _guideBubble.AddChild(stack);
        _stepLabel = NewLabel(16, new Color("#f1c453"));
        _titleLabel = NewLabel(23, new Color("#74e8ec"));
        _bodyLabel = NewLabel(20, Colors.White);
        _bodyLabel.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        stack.AddChild(_stepLabel); stack.AddChild(_titleLabel); stack.AddChild(_bodyLabel);
    }

    private void UpdateText(bool approach)
    {
        _stepLabel.Text = $"{GuideName}  {(approach ? "1/2" : "2/2")}";
        _titleLabel.Text = approach ? ApproachTitle : InteractTitle;
        _bodyLabel.Text = approach ? ApproachInstruction : InteractInstruction;
    }

    private void LayoutOverlay()
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        bool interaction = Phase == GuidePhase.Interact;
        SetBlockersVisible(interaction);

        if (_targetRect.Size.X <= 0 || _targetRect.Size.Y <= 0)
        {
            _highlight.Visible = false;
            _arrowLabel.Visible = false;
            return;
        }

        _highlight.Visible = true;
        _arrowLabel.Visible = true;
        SetRect(_highlight, _targetRect);
        _highlight.SelfModulate = new Color(1, 1, 1,
            0.78f + Mathf.Sin(Time.GetTicksMsec() * 0.006f) * 0.22f);

        if (interaction)
        {
            SetRect(_blockers[0], new Rect2(0, 0, viewport.X, _inputRect.Position.Y));
            SetRect(_blockers[1], new Rect2(0, _inputRect.Position.Y, _inputRect.Position.X, _inputRect.Size.Y));
            SetRect(_blockers[2], new Rect2(_inputRect.End.X, _inputRect.Position.Y,
                Mathf.Max(0, viewport.X - _inputRect.End.X), _inputRect.Size.Y));
            SetRect(_blockers[3], new Rect2(0, _inputRect.End.Y, viewport.X,
                Mathf.Max(0, viewport.Y - _inputRect.End.Y)));
        }

        float width = Mathf.Max(1.0f, Mathf.Min(520.0f, viewport.X - 40.0f));
        float height = Mathf.Max(1.0f, Mathf.Min(150.0f, viewport.Y - 40.0f));
        float x = Mathf.Max(0.0f, (viewport.X - width) * 0.5f);
        SetRect(_guideBubble, new Rect2(x, 24, width, height));

        Vector2 center = _targetRect.GetCenter();
        _arrowLabel.Text = center.Y < 190 ? "▲" : "▼";
        SetRect(_arrowLabel, new Rect2(center.X - 24,
            center.Y < 190 ? _targetRect.End.Y + 2 : _targetRect.Position.Y - 38, 48, 36));
    }

    private void SetBlockersVisible(bool visible)
    {
        foreach (ColorRect blocker in _blockers)
            if (blocker != null) blocker.Visible = visible;
    }

    private static Label NewLabel(int size, Color color)
    {
        var label = new Label { MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_color", color);
        label.AddThemeColorOverride("font_outline_color", new Color("#071014"));
        label.AddThemeConstantOverride("outline_size", 4);
        return label;
    }

    private Rect2 ClampRect(Rect2 rect) => ExpandAndClamp(rect, 0);

    private Rect2 ExpandAndClamp(Rect2 source, float padding)
    {
        Vector2 viewport = GetViewport().GetVisibleRect().Size;
        float left = Mathf.Clamp(source.Position.X - padding, 0, viewport.X);
        float top = Mathf.Clamp(source.Position.Y - padding, 0, viewport.Y);
        float right = Mathf.Clamp(source.End.X + padding, 0, viewport.X);
        float bottom = Mathf.Clamp(source.End.Y + padding, 0, viewport.Y);
        return new Rect2(left, top, Mathf.Max(0, right - left), Mathf.Max(0, bottom - top));
    }

    private static void SetRect(Control control, Rect2 rect)
    {
        control.Position = rect.Position;
        control.Size = new Vector2(Mathf.Max(0, rect.Size.X), Mathf.Max(0, rect.Size.Y));
    }
}
