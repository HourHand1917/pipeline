using Godot;

/// <summary>
/// One image-only map slot. The Button owns input; its TextureRect is presentation only.
/// Selection is visual state only and never starts travel by itself.
/// </summary>
[GlobalClass]
public partial class FrontMapChoice : Button
{
    [ExportGroup("Identity")]
    [Export] public StringName DestinationMapId { get; set; } = "";
    [Export] public bool Locked { get; set; }
    [Export] public string LockedDisplayName { get; set; } = "尚未开放";
    [Export(PropertyHint.MultilineText)]
    public string LockedDescription { get; set; } = "这条路线尚未开放，当前无法前往。";

    [ExportGroup("Presentation")]
    [Export] private TextureRect _art;
    [Export] private Texture2D _selectedTexture;
    [Export] private Texture2D _unselectedTexture;
    [Export(PropertyHint.Range, "1.0,1.2,0.005")]
    private float _selectedScale = 1.08f;
    [Export(PropertyHint.Range, "1.0,1.15,0.005")]
    private float _hoverScale = 1.035f;

    public bool IsSelected { get; private set; }
    public bool CurrentUsesSelectedTexture => _art != null && _art.Texture == _selectedTexture;
    public Texture2D TooltipIcon => _unselectedTexture;

    private bool _hovered;
    private bool _focused;
    private bool _held;
    private Tween _stateTween;
    private Tween _entranceTween;

    public override void _Ready()
    {
        Flat = true;
        ToggleMode = false;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        PivotOffset = Size * 0.5f;

        if (_art != null)
        {
            _art.MouseFilter = MouseFilterEnum.Ignore;
            _art.PivotOffset = _art.Size * 0.5f;
        }

        MouseEntered += OnPointerEntered;
        MouseExited += OnPointerExited;
        FocusEntered += OnFocusEntered;
        FocusExited += OnFocusExited;
        ButtonDown += OnButtonDown;
        ButtonUp += OnButtonUp;
        UpdateVisual(false);
    }

    public override void _ExitTree()
    {
        _stateTween?.Kill();
        _entranceTween?.Kill();
    }

    public void SetSelected(bool selected, bool animate = true)
    {
        bool changed = IsSelected != selected;
        IsSelected = selected;
        UpdateVisual(animate && changed);
    }

    public void PlayEntrance(float delaySeconds)
    {
        _entranceTween?.Kill();
        Modulate = new Color(1, 1, 1, 0);
        Scale = new Vector2(0.94f, 0.94f);
        _entranceTween = CreateTween().SetParallel(true);
        _entranceTween.TweenProperty(this, "modulate", Colors.White, 0.18f)
            .SetDelay(delaySeconds)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
        _entranceTween.TweenProperty(this, "scale", Vector2.One, 0.22f)
            .SetDelay(delaySeconds)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
    }

    public void PlayRejectedFeedback()
    {
        _stateTween?.Kill();
        _stateTween = CreateTween();
        _stateTween.TweenProperty(this, "position:x", Position.X - 7.0f, 0.045f);
        _stateTween.TweenProperty(this, "position:x", Position.X + 7.0f, 0.08f);
        _stateTween.TweenProperty(this, "position:x", Position.X, 0.045f);
    }

    private void OnPointerEntered()
    {
        _hovered = true;
        UpdateVisual(true);
    }

    private void OnPointerExited()
    {
        _hovered = false;
        _held = false;
        UpdateVisual(true);
    }

    private void OnFocusEntered()
    {
        _focused = true;
        UpdateVisual(true);
    }

    private void OnFocusExited()
    {
        _focused = false;
        UpdateVisual(true);
    }

    private void OnButtonDown()
    {
        _held = true;
        UpdateVisual(true);
    }

    private void OnButtonUp()
    {
        _held = false;
        UpdateVisual(true);
    }

    private void UpdateVisual(bool animate)
    {
        if (_art == null)
            return;

        _art.Texture = IsSelected ? _selectedTexture : _unselectedTexture;
        _art.Modulate = Locked
            ? new Color(0.72f, 0.70f, 0.76f, 0.9f)
            : Colors.White;

        float multiplier = IsSelected
            ? _selectedScale
            : (_hovered || _focused ? _hoverScale : 1.0f);
        if (IsSelected && (_hovered || _focused))
            multiplier += 0.018f;
        if (_held)
            multiplier *= 0.965f;

        Vector2 target = Vector2.One * multiplier;
        _stateTween?.Kill();
        if (!animate)
        {
            _art.Scale = target;
            return;
        }

        _stateTween = CreateTween();
        _stateTween.TweenProperty(_art, "scale", target, 0.14f)
            .SetTrans(Tween.TransitionType.Back)
            .SetEase(Tween.EaseType.Out);
    }
}
