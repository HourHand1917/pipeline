using Godot;
using Godot.Collections;

public partial class HomeStoryGuidesContractTest : Node
{
    private int _checks;
    private int _failures;

    public override async void _Ready()
    {
        Check(!HomeStoryGuideController.IsDefeatedState(null), "missing state is not a victory");
        Check(!HomeStoryGuideController.IsDefeatedState(new Dictionary { { "defeated", false } }),
            "false defeated state is not a victory");
        Check(HomeStoryGuideController.IsDefeatedState(new Dictionary { { "defeated", true } }),
            "real defeated state activates story flow");

        PackedScene packaged = GD.Load<PackedScene>(
            "res://features/exploration/story_guides/home_story_guides.tscn");
        Check(packaged != null, "packaged home guide scene loads");
        Node guideRoot = packaged?.Instantiate();
        AddChild(guideRoot);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        var controller = guideRoot as HomeStoryGuideController;
        Check(controller != null, "packaged root is HomeStoryGuideController");
        Check(controller?.RockyMapId == "f1_2" && controller.RockyPersistenceId == "enemy_rocky_f1_2",
            "F1 guide is gated by Rocky persistence, not default level 1");
        Check(controller?.SharkkMapId == "f2_4" && controller.SharkkPersistenceId == "enemy_sharkk_f2_4",
            "F2 guide is gated by Sharkk persistence");
        Check(controller?.PostF1MousyScene != null && controller.PostF2MousyScene != null,
            "both Mousy dialogue prefabs are assigned");
        Check(controller?.PostF1MousySpawn != null && controller.PostF2MousySpawn != null,
            "both Mousy spawn positions are editable Marker2D nodes");

        var workbenchGuide = guideRoot?.GetNodeOrNull<ExplorationInteractionGuide>("WorkbenchGuide");
        var storeGuide = guideRoot?.GetNodeOrNull<ExplorationInteractionGuide>("StoreGuide");
        Check(workbenchGuide != null && storeGuide != null, "both reusable guides exist");
        Check(storeGuide?.WaitForDialogueAtTarget == true,
            "store guide pauses at the target for Mousy dialogue");

        CheckMousyScene(controller?.PostF1MousyScene, "home鼠鼠.dtl", "F1");
        CheckMousyScene(controller?.PostF2MousyScene, "Sharkk战后鼠鼠.dtl", "F2");

        PackedScene homePacked = GD.Load<PackedScene>("res://features/exploration/scenes/home.tscn");
        Check(homePacked != null, "production home scene loads");
        Node home = homePacked?.Instantiate();
        Check(home?.GetNodeOrNull("HomeStoryGuides") != null,
            "production home scene contains the packaged guide system");
        home?.QueueFree();

        if (_failures == 0)
            GD.Print($"HOME_STORY_GUIDES_TEST PASS checks={_checks}");
        else
            GD.PushError($"HOME_STORY_GUIDES_TEST FAIL failures={_failures} checks={_checks}");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private void CheckMousyScene(PackedScene packed, string expectedTimelineName, string label)
    {
        Node instance = packed?.Instantiate();
        var npc = instance as PackagedFriendlyNPC;
        Check(npc != null, $"{label} Mousy prefab is a packaged friendly NPC");
        Check(npc?.DefaultTimelinePath.EndsWith(expectedTimelineName) == true,
            $"{label} Mousy uses the existing configured timeline");
        Check(npc?.AllowEscapeToExitDialogue == true,
            $"{label} non-combat story dialogue can be skipped with ESC");
        var sprite = instance?.GetNodeOrNull<AnimatedSprite2D>("Sprite");
        var spriteScript = sprite?.Get("script").AsGodotObject() as Script;
        Check(spriteScript?.ResourcePath.EndsWith("npc_frame_animation.gd") == true &&
              sprite.Get("frames_directory").AsString().EndsWith("/animations/frames/mousy"),
            $"{label} Mousy uses the configured 47-frame animation instead of a placeholder");
        instance?.QueueFree();
    }

    private void Check(bool condition, string description)
    {
        _checks++;
        if (condition) return;
        _failures++;
        GD.PushError($"HOME_STORY_GUIDES_TEST: {description}");
    }
}
