using Godot;

/// <summary>
/// Core-00 phase-two ending: configurable black-screen rolling credits,
/// a timed pause, then the project poster. Input is accepted only after the
/// poster is fully visible so an earlier battle key cannot close it by accident.
/// </summary>
[GlobalClass]
public partial class F4EndingScreen : Control
{
    [Signal] public delegate void ExitRequestedEventHandler();

    [ExportGroup("Rolling credits")]
    [Export(PropertyHint.MultilineText)]
    public string CreditsText { get; set; } =
        "PIPELINE RECORD\n\n开发记录\n\n【黑匣子实验室】\n" +
        "海桐　策划、程序\n隐水　策划\n时针　程序\n" +
        "Popkey49　美术\nPPY　美术\nJosie　音乐\n\n感谢游玩";

    [Export(PropertyHint.Range, "20,400,1")]
    public float ScrollPixelsPerSecond { get; set; } = 72.0f;

    [Export(PropertyHint.Range, "0,10,0.1")]
    public float AfterCreditsDelaySeconds { get; set; } = 3.0f;

    [ExportGroup("Poster")]
    [Export(PropertyHint.MultilineText)]
    public string PosterTeamText { get; set; } =
        "【黑匣子实验室】\n\n" +
        "海桐+策划、程序，隐水+策划\n" +
        "时针+程序、Popkey49+美术、PPY+美术\n" +
        "Josie+音乐";

    [Export] public string ExitHintText { get; set; } = "点击任意键退出";

    [Export(PropertyHint.Range, "0,3,0.05")]
    public float PosterFadeSeconds { get; set; } = 0.65f;

    [ExportGroup("Exit")]
    [Export] public bool ExitApplicationOnInput { get; set; } = true;
    [Export(PropertyHint.File, "*.tscn")]
    public string ExitScenePath { get; set; } = "";

    [ExportGroup("Scene references")]
    [Export] public RichTextLabel CreditsLabel { get; set; }
    [Export] public Control PosterLayer { get; set; }
    [Export] public Label PosterText { get; set; }
    [Export] public Label ExitHint { get; set; }

    public bool CreditsFinished { get; private set; }
    public bool PosterVisible { get; private set; }

    private enum EndingState { Preparing, Scrolling, Waiting, Poster }
    private EndingState _state = EndingState.Preparing;
    private float _waitElapsed;
    private float _creditsHeight = 1.0f;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Stop;
        CreditsLabel ??= GetNodeOrNull<RichTextLabel>("CreditsClip/ScrollingCredits");
        PosterLayer ??= GetNodeOrNull<Control>("PosterLayer");
        PosterText ??= GetNodeOrNull<Label>("PosterLayer/PosterText");
        ExitHint ??= GetNodeOrNull<Label>("PosterLayer/ExitHint");

        if (CreditsLabel == null || PosterLayer == null || PosterText == null || ExitHint == null)
        {
            GD.PushError("F4EndingScreen: scene references are incomplete.");
            SetProcess(false);
            SetProcessInput(false);
            return;
        }

        CreditsLabel.Text = CreditsText;
        PosterText.Text = PosterTeamText;
        ExitHint.Text = ExitHintText;
        PosterLayer.Visible = false;
        PosterLayer.Modulate = new Color(1, 1, 1, 0);
        SetProcessInput(true);
        CallDeferred(MethodName.BeginCredits);
    }

    private async void BeginCredits()
    {
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        LayoutCreditsAtBottom();
        _state = EndingState.Scrolling;
    }

    public override void _Process(double delta)
    {
        switch (_state)
        {
            case EndingState.Scrolling:
                CreditsLabel.Position += Vector2.Up * ScrollPixelsPerSecond * (float)delta;
                if (CreditsLabel.Position.Y + _creditsHeight <= 0.0f)
                {
                    CreditsFinished = true;
                    CreditsLabel.Visible = false;
                    _waitElapsed = 0.0f;
                    _state = EndingState.Waiting;
                }
                break;
            case EndingState.Waiting:
                _waitElapsed += (float)delta;
                if (_waitElapsed >= AfterCreditsDelaySeconds)
                    ShowPoster();
                break;
        }
    }

    private void LayoutCreditsAtBottom()
    {
        float width = Mathf.Clamp(Size.X - 160.0f, 480.0f, 1040.0f);
        CreditsLabel.Size = new Vector2(width, Mathf.Max(1.0f, CreditsLabel.GetContentHeight()));
        _creditsHeight = Mathf.Max(CreditsLabel.Size.Y, CreditsLabel.GetContentHeight());
        CreditsLabel.Position = new Vector2((Size.X - width) * 0.5f, Size.Y + 24.0f);
    }

    private void ShowPoster()
    {
        if (_state == EndingState.Poster)
            return;
        _state = EndingState.Poster;
        PosterLayer.Visible = true;
        Tween tween = CreateTween();
        tween.SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(PosterLayer, "modulate:a", 1.0f, PosterFadeSeconds);
        tween.Finished += () => PosterVisible = true;
        if (PosterFadeSeconds <= 0.0f)
            PosterVisible = true;
    }

    public override void _Input(InputEvent @event)
    {
        if (!PosterVisible || !IsAcceptInput(@event))
            return;

        GetViewport().SetInputAsHandled();
        EmitSignal(SignalName.ExitRequested);

        if (!string.IsNullOrWhiteSpace(ExitScenePath))
        {
            SceneTransition.Instance?.ChangeScene(ExitScenePath);
            return;
        }
        if (ExitApplicationOnInput)
            GetTree().Quit();
    }

    private static bool IsAcceptInput(InputEvent @event)
    {
        if (@event is InputEventKey key)
            return key.Pressed && !key.Echo;
        if (@event is InputEventMouseButton mouse)
            return mouse.Pressed;
        if (@event is InputEventJoypadButton joypad)
            return joypad.Pressed;
        return false;
    }
}
