using Godot;

/// <summary>
/// A small painted-style volume control used by the pause machine.
/// It deliberately draws its own track and grabber so the three interactive
/// rows keep the cyan hand-painted look without baking a fixed slider value
/// into the artwork.
/// </summary>
[GlobalClass]
public partial class PauseVolumeSlider : Control
{
    [Signal]
    public delegate void ValueChangedEventHandler(float value);

    [Export]
    public Color TrackColor { get; set; } = new("38d8d8");

    [Export]
    public Color FillColor { get; set; } = new("71ffff");

    [Export(PropertyHint.Range, "0,1,0.01")]
    public float Value
    {
        get => _value;
        set => SetValue(value, false);
    }

    private const float TrackInset = 12.0f;
    private float _value = 0.8f;
    private bool _hovered;

    public override void _Ready()
    {
        FocusMode = FocusModeEnum.All;
        MouseDefaultCursorShape = CursorShape.PointingHand;
        CustomMinimumSize = new Vector2(350, 48);
        MouseEntered += () => { _hovered = true; QueueRedraw(); };
        MouseExited += () => { _hovered = false; QueueRedraw(); };
        FocusEntered += QueueRedraw;
        FocusExited += QueueRedraw;
        QueueRedraw();
    }

    public void SetValue(float value, bool emitSignal = true)
    {
        float next = Mathf.Clamp(value, 0.0f, 1.0f);
        if (Mathf.IsEqualApprox(next, _value))
            return;

        _value = next;
        QueueRedraw();
        if (emitSignal)
            EmitSignal(SignalName.ValueChanged, _value);
    }

    public override void _GuiInput(InputEvent @event)
    {
        switch (@event)
        {
            case InputEventMouseButton mouse when mouse.ButtonIndex == MouseButton.Left:
                if (mouse.Pressed)
                {
                    GrabFocus();
                    SetFromLocalX(mouse.Position.X);
                }
                AcceptEvent();
                break;

            case InputEventMouseMotion motion
                when (motion.ButtonMask & MouseButtonMask.Left) != 0:
                SetFromLocalX(motion.Position.X);
                AcceptEvent();
                break;

            case InputEventKey key when key.Pressed && !key.Echo:
                if (key.Keycode is Key.Left or Key.Down)
                {
                    SetValue(_value - 0.05f);
                    AcceptEvent();
                }
                else if (key.Keycode is Key.Right or Key.Up)
                {
                    SetValue(_value + 0.05f);
                    AcceptEvent();
                }
                else if (key.Keycode == Key.Home)
                {
                    SetValue(0.0f);
                    AcceptEvent();
                }
                else if (key.Keycode == Key.End)
                {
                    SetValue(1.0f);
                    AcceptEvent();
                }
                break;
        }
    }

    public override void _Draw()
    {
        float width = Mathf.Max(Size.X, CustomMinimumSize.X);
        float centerY = Size.Y * 0.5f;
        float trackStart = TrackInset;
        float trackEnd = width - TrackInset;
        float knobX = Mathf.Lerp(trackStart, trackEnd, _value);
        Color glow = (_hovered || HasFocus()) ? FillColor.Lightened(0.16f) : FillColor;

        DrawLine(new Vector2(trackStart, centerY), new Vector2(trackEnd, centerY),
            TrackColor.Darkened(0.35f), 9.0f, true);
        DrawLine(new Vector2(trackStart, centerY), new Vector2(knobX, centerY),
            glow, 7.0f, true);

        for (int tick = 0; tick <= 10; tick++)
        {
            float x = Mathf.Lerp(trackStart, trackEnd, tick / 10.0f);
            DrawLine(new Vector2(x, centerY + 8.0f), new Vector2(x, centerY + 13.0f),
                TrackColor, 2.0f, true);
        }

        Rect2 knob = new(new Vector2(knobX - 8.0f, centerY - 17.0f), new Vector2(16.0f, 34.0f));
        DrawRect(knob, new Color("082c2f"), true);
        DrawRect(knob, glow, false, 4.0f);

        if (HasFocus())
            DrawRect(new Rect2(Vector2.Zero, Size), glow.Darkened(0.25f), false, 2.0f);
    }

    private void SetFromLocalX(float localX)
    {
        float usableWidth = Mathf.Max(1.0f, Size.X - TrackInset * 2.0f);
        SetValue((localX - TrackInset) / usableWidth);
    }
}
