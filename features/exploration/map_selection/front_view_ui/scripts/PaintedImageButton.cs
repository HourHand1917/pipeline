using Godot;

/// <summary>Transparent input Button with supplied normal/pressed painted images.</summary>
[GlobalClass]
public partial class PaintedImageButton : Button
{
    [Export] private TextureRect _art;
    [Export] private Texture2D _normalTexture;
    [Export] private Texture2D _pressedTexture;
    [Export(PropertyHint.Range, "1.0,1.15,0.005")]
    private float _hoverScale = 1.04f;

    public Texture2D NormalTexture => _normalTexture;
    public Texture2D PressedTexture => _pressedTexture;
    public bool IsShowingPressedTexture => _art != null && _art.Texture == _pressedTexture;

    private bool _hovered;
    private bool _held;
    private bool _lastDisabled;
    private Tween _tween;

    public override void _Ready()
    {
        Flat = true;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        PivotOffset = Size * 0.5f;
        if (_art != null)
        {
            _art.MouseFilter = MouseFilterEnum.Ignore;
            _art.PivotOffset = _art.Size * 0.5f;
            _art.Texture = _normalTexture;
        }

        MouseEntered += () => { _hovered = true; Refresh(true); };
        MouseExited += () => { _hovered = false; _held = false; Refresh(true); };
        ButtonDown += () => { _held = true; Refresh(true); };
        ButtonUp += () => { _held = false; Refresh(true); };
        _lastDisabled = Disabled;
        Refresh(false);
    }

    public override void _Process(double delta)
    {
        if (_lastDisabled == Disabled)
            return;
        _lastDisabled = Disabled;
        Refresh(true);
    }

    public override void _ExitTree()
    {
        _tween?.Kill();
    }

    private void Refresh(bool animate)
    {
        if (_art == null)
            return;

        _art.Texture = _held && !Disabled ? _pressedTexture : _normalTexture;
        _art.Modulate = Disabled ? new Color(0.48f, 0.46f, 0.52f, 0.82f) : Colors.White;
        float multiplier = _held && !Disabled ? 0.965f : (_hovered && !Disabled ? _hoverScale : 1.0f);
        Vector2 target = Vector2.One * multiplier;

        _tween?.Kill();
        if (!animate)
        {
            _art.Scale = target;
            return;
        }

        _tween = CreateTween();
        _tween.TweenProperty(_art, "scale", target, 0.1f)
            .SetTrans(Tween.TransitionType.Cubic)
            .SetEase(Tween.EaseType.Out);
    }
}
