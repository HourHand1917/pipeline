using Godot;
using System;

/// <summary>Smoke coverage for the isolated painted front-view adapter.</summary>
public partial class FrontMapUiSmoke : Node
{
    private int _checks;

    public override void _Ready()
    {
        CallDeferred(MethodName.Run);
    }

    private void Run()
    {
        try
        {
            const string uiPath =
                "res://features/exploration/map_selection/scenes/map_select_ui.tscn";
            PackedScene packed = ResourceLoader.Load<PackedScene>(uiPath);
            Check(packed != null, "map selector scene loads with front-view layer");

            MapSelectUI ui = packed.Instantiate<MapSelectUI>();
            AddChild(ui);
            FrontMapPresenter presenter = ui.GetNode<FrontMapPresenter>("FrontViewVisualLayer");
            Check(presenter != null, "isolated visual presenter is instanced");
            Check(presenter.ChoiceCount == 4, "four selectable/inspectable painted map slots are present");

            FrontMapChoice boss = presenter.GetChoiceByMapId("f4");
            FrontMapChoice market = presenter.GetChoiceByMapId("f2_1");
            FrontMapChoice casino = presenter.GetChoiceByMapId("f3_0");
            FrontMapChoice wasteland = presenter.GetChoiceByName("Wasteland");
            Control pipeSlot = presenter.GetNode<Control>("DesignCanvas/EntranceLayer/Pipe");
            Check(boss != null && market != null && casino != null && !casino.Locked,
                "real route mapping is f4=Boss, f3_0=Casino, f2_1=Market");
            Check(pipeSlot != null && !pipeSlot.Visible && presenter.GetChoiceByName("Pipe") == null,
                "Pipe slot is removed from the visible and interactive route list");
            Check(wasteland != null && wasteland.Locked
                && wasteland.DestinationMapId == "f1_0",
                "Wasteland represents the locked tutorial origin");
            Check(wasteland.LockedDescription == "这是你来的地方，你已然带走你该带走的，不可再进入",
                "Wasteland exposes the exact non-reentry explanation in its tooltip");

            presenter.ApplyViewportLayoutForSize(new Vector2(1440, 1080));
            Check(presenter.DesignCanvasScale.IsEqualApprox(Vector2.One),
                "1440x1080 keeps the 1920x1080 design at height scale 1");
            Check(presenter.DesignCanvasPosition.IsEqualApprox(new Vector2(-240, 0)),
                "1440x1080 crops equal 240px side margins without stretching");

            Check(ui.GetNode<Control>("Design").Visible == false,
                "legacy functional rows/details stay hidden");
            Check(ui.GetNode<Control>("ModalShield").MouseFilter == Control.MouseFilterEnum.Stop,
                "modal still blocks all background mouse input");

            foreach (Node node in presenter.FindChildren("*", "TextureRect", true, false))
            {
                TextureRect image = node as TextureRect;
                Check(image != null && image.MouseFilter == Control.MouseFilterEnum.Ignore,
                    $"image {node.Name} ignores mouse input");
            }

            DataManager.Instance.SetLevelAtLeast(3);
            ui.Open();
            Check(ui.IsOpen && ui.Visible, "Open keeps the existing modal lifecycle");
            Check(presenter.OpeningAnimationCount == 1, "open animation starts once");
            Check(presenter.TooltipBoundChoiceCount == 4,
                "all four active slots reuse the global TooltipService");
            Check(ui.SelectedIndex == 0 && market.IsSelected && market.CurrentUsesSelectedTexture,
                "first configured destination selects the Market painted state");

            boss.EmitSignal(BaseButton.SignalName.Pressed);
            Check(ui.SelectedIndex == 2 && boss.IsSelected,
                "Boss click selects F4 by configured map id");
            Check(ui.IsOpen, "map-card click only selects and does not travel");

            casino.EmitSignal(BaseButton.SignalName.Pressed);
            Check(ui.SelectedIndex == 1 && casino.IsSelected,
                "Casino click selects F3 by configured map id");
            Check(!boss.IsSelected && !market.IsSelected,
                "only one real map keeps the selected texture");

            wasteland.EmitSignal(BaseButton.SignalName.Pressed);
            Check(ui.SelectedIndex == 1, "locked Wasteland cannot replace selection");

            Check(presenter.ConfirmImageButton != null
                && presenter.ConfirmImageButton.NormalTexture != null
                && presenter.ConfirmImageButton.PressedTexture != null,
                "right-side confirm uses supplied normal/pressed images");
            Check(presenter.CloseImageButton != null
                && presenter.CloseImageButton.NormalTexture != null
                && presenter.CloseImageButton.PressedTexture != null,
                "right-side close uses supplied normal/pressed images");

            int closedCount = 0;
            ui.Closed += () => closedCount++;
            ui.Close();
            Check(!ui.IsOpen && !ui.Visible && closedCount == 1,
                "Close still restores the existing lifecycle exactly once");

            GD.Print($"FRONT_MAP_UI_SMOKE_PASS checks={_checks} choices=4 real=3 locked=1");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"FRONT_MAP_UI_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition)
            throw new InvalidOperationException(message);
    }
}
