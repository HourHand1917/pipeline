using Godot;
using System;

/// <summary>
/// Contract coverage for the presentation-only battle additions: danger text
/// never enters the TrackSlot VBox, and exploration capture produces a wide,
/// exploration-height static stage without changing the 4:3 project viewport.
/// </summary>
public partial class BattlePresentationContractTest : Node2D
{
    private int _checks;

    public override void _Ready() => CallDeferred(MethodName.Run);

    private async void Run()
    {
        try
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            await CheckDangerOverlay();
            await CheckBackdropCapture();
            GD.Print($"BATTLE_PRESENTATION_CONTRACT_PASS checks={_checks} backdrop=exploration_size lower=black ground=locked");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"BATTLE_PRESENTATION_CONTRACT_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async System.Threading.Tasks.Task CheckDangerOverlay()
    {
        PackedScene packed = ResourceLoader.Load<PackedScene>(
            "res://Scenes/resource_scene/trackslot.tscn");
        TrackSlot slot = packed.Instantiate<TrackSlot>();
        AddChild(slot);
        slot.Configure(3, "玩家", Colors.White, null);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        Control occupant = slot.GetNode<Control>("Vbox/occupant");
        Label slotLabel = slot.GetNode<Label>("Vbox/slotlabel");
        Label danger = slot.GetNode<Label>("DangerLabel");
        float footLine = occupant.Position.Y + occupant.Size.Y;
        Vector2 minimumBefore = slot.GetCombinedMinimumSize();

        slot.SetDangerState(true, false);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(danger.Visible && danger.Text.Contains("危险"),
            "danger text is shown in the dedicated overlay");
        Check(danger.GetParent() == slot && danger.GetParent() != slotLabel.GetParent(),
            "danger text is outside the VBox that lays out actor and cell number");
        Check(slotLabel.Text == "3", "cell number text is never replaced by danger copy");
        Check(Mathf.Abs((occupant.Position.Y + occupant.Size.Y) - footLine) <= 0.01f,
            "danger display cannot move the authored actor foot line");
        Check(slot.GetCombinedMinimumSize().IsEqualApprox(minimumBefore),
            "danger display cannot resize the TrackSlot");

        slot.SetDangerState(false, true);
        Check(danger.Visible && danger.Text.Contains("预警"),
            "future danger uses the same non-layout overlay");
        slot.QueueFree();
    }

    private async System.Threading.Tasks.Task CheckBackdropCapture()
    {
        Image image = Image.CreateEmpty(3000, 1000, false, Image.Format.Rgba8);
        image.Fill(new Color("#26383f"));
        ImageTexture texture = ImageTexture.CreateFromImage(image);
        var background = new Sprite2D
        {
            Name = "测试探索背景",
            Texture = texture,
            Centered = false,
        };
        AddChild(background);
        var focus = new Node2D { Name = "EncounterFocus", Position = new Vector2(1500, 760) };
        AddChild(focus);

        BattleDirector.Instance.PrepareBattleBackdrop(focus);
        Rect2 region = BattleDirector.Instance.PendingBattleBackdropRegion;
        Check(BattleDirector.Instance.PendingBattleBackdropTexture == texture,
            "capture carries the current exploration background texture into battle");
        Check(region.Size.X > 1440.0f && region.Size.X <= 3000.0f,
            "capture keeps a wider panorama for long horizontal battle maps");
        Check(Mathf.Abs(region.Size.X / region.Size.Y - (1440.0f / 700.0f)) <= 0.01f,
            "capture uses the authored exploration-height battle stage aspect");
        Check(BattleDirector.Instance.PendingBattleBackdropSource.Contains("测试探索背景"),
            "capture identifies the exploration background rather than an actor or UI");
        Check(ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32() == 1440
            && ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32() == 1080,
            "presentation changes preserve the 1440x1080 4:3 viewport");

        PackedScene screenScene = ResourceLoader.Load<PackedScene>(
            "res://Scenes/game_scene/battlescreen.tscn");
        BattleScreen screen = screenScene.Instantiate<BattleScreen>();
        AddChild(screen);
        var presenter = new BattleBackdropPresenter { Name = "BackdropPresenterContract" };
        screen.AddChild(presenter);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        ColorRect black = screen.GetNodeOrNull<ColorRect>("BattleLowerBlackFill");
        Control mount = screen.GetNodeOrNull<Control>("ExplorationBattleStage");
        PanelContainer stagePanel = screen.GetNode<PanelContainer>("stagepanel");
        VBoxContainer stageVbox = screen.GetNode<VBoxContainer>("stagepanel/margin/stageVbox");
        Check(presenter.IsInstalled && mount != null,
            "presenter installs the exploration background as an independent stage");
        Check(Mathf.Abs(presenter.BackdropOpacity - 0.30f) <= 0.001f,
            "exploration background defaults to thirty-percent opacity");
        Check(black != null && black.Color == Colors.Black
            && black.MouseFilter == Control.MouseFilterEnum.Ignore,
            "the whole area below the exploration stage is pure mouse-transparent black");
        Check(Mathf.Abs(mount.AnchorBottom - 0.65f) <= 0.001f
            && Mathf.Abs(stagePanel.AnchorBottom - mount.AnchorBottom) <= 0.001f,
            "background and actor layout use the same lower-stage reference");
        Check(Mathf.Abs(stagePanel.OffsetBottom + 76.0f) <= 0.01f,
            "track bottom stays two centimetres above the exploration-stage bottom");
        Check(stageVbox.Alignment == BoxContainer.AlignmentMode.End,
            "the combatant row is bottom-aligned to the exploration ground");
        screen.QueueFree();
        background.QueueFree();
        focus.QueueFree();
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
