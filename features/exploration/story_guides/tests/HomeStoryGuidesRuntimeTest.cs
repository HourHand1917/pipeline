using Godot;
using Godot.Collections;

public partial class HomeStoryGuidesRuntimeTest : Node
{
    private int _checks;
    private int _failures;
    private WorkbenchUI _workbenchUi;
    private ShopUI _shopUi;

    public override async void _Ready()
    {
        GameState.Instance.Reset();
        GameState.Instance.SetObjectState("f1_2", "enemy_rocky_f1_2",
            new Dictionary { { "defeated", true } });

        PlayerController player = GD.Load<PackedScene>(
            "res://features/exploration/scenes/player.tscn").Instantiate<PlayerController>();
        AddChild(player);
        player.GlobalPosition = new Vector2(500, 530);

        _workbenchUi = new WorkbenchUI { Visible = false };
        _shopUi = new ShopUI { Visible = false };
        WorkbenchInteractable workbench = MakeWorkbench(_workbenchUi);
        StoreInteractable store = MakeStore(_shopUi);
        AddChild(workbench);
        AddChild(store);
        workbench.GlobalPosition = new Vector2(100, 530);
        store.GlobalPosition = new Vector2(1000, 530);

        HomeStoryGuideController firstVisit = MakeController(player, workbench, store);
        AddChild(firstVisit);
        await WaitFrames(8);

        Check(firstVisit.IsPostF1Active, "Rocky victory starts the F1 home stage");
        NPCBase firstMousy = firstVisit.ActiveMousy;
        Check(firstMousy != null && firstMousy.IsDialogueActive,
            "F1 Mousy spawns beside the player and immediately starts dialogue");
        Check(firstMousy != null && firstVisit.PostF1MousySpawn != null &&
              firstMousy.GlobalPosition.DistanceTo(firstVisit.PostF1MousySpawn.GlobalPosition) < 1f,
            "F1 Mousy is positioned at the editable scene marker");

        EndDialogicTimeline();
        await WaitFrames(5);
        Check(firstVisit.WorkbenchGuide.Phase == ExplorationInteractionGuide.GuidePhase.Approach,
            "workbench guide begins after the F1 dialogue");

        firstVisit.WorkbenchGuide.CompleteGuide();
        await WaitFrames(2);
        Check(firstMousy != null && IsInstanceValid(firstMousy),
            "F1 Mousy remains for the rest of the current home visit");

        // An out-of-order/debug save can already contain Sharkk. It must not
        // create a second Mousy during this same visit.
        GameState.Instance.SetObjectState("f2_4", "enemy_sharkk_f2_4",
            new Dictionary { { "defeated", true } });
        await WaitFrames(2);
        Check(!firstVisit.IsPostF2Active && CountMousy() == 1,
            "F1 completion never stacks a second F2 Mousy in the same visit");

        firstVisit.QueueFree();
        firstMousy?.QueueFree();
        await WaitFrames(3);
        Dictionary f1State = GameState.Instance.GetObjectState(
            "__story_guides__", "home_after_f1_workbench");
        Check(ReadBool(f1State, "departed"),
            "leaving home records that the first Mousy portrait has departed");

        HomeStoryGuideController secondVisit = MakeController(player, workbench, store);
        AddChild(secondVisit);
        await WaitFrames(5);
        Check(secondVisit.IsPostF2Active,
            "next home visit advances to the Sharkk/store stage");
        Check(!secondVisit.IsPostF1Active,
            "completed F1 stage and departed Mousy do not reappear");

        secondVisit.StoreGuide.ForceTargetReachedForTesting();
        await WaitFrames(8);
        Check(secondVisit.ActiveMousy != null && secondVisit.ActiveMousy.IsDialogueActive,
            "approaching the store starts the existing post-Sharkk Mousy dialogue");

        EndDialogicTimeline();
        await WaitFrames(5);
        Check(secondVisit.StoreGuide.Phase == ExplorationInteractionGuide.GuidePhase.Interact,
            "store click is highlighted only after Mousy dialogue finishes");

        secondVisit.StoreGuide.CompleteGuide();
        await WaitFrames(3);
        Dictionary f2State = GameState.Instance.GetObjectState(
            "__story_guides__", "home_after_f2_shop");
        Check(ReadBool(f2State, "dialogue_completed") && ReadBool(f2State, "guide_completed"),
            "F2 dialogue and shop guide completion persist together");

        secondVisit.QueueFree();
        workbench.QueueFree();
        store.QueueFree();
        player.QueueFree();
        _workbenchUi.Free();
        _shopUi.Free();
        await WaitFrames(2);

        if (_failures == 0)
            GD.Print($"HOME_STORY_GUIDES_RUNTIME_TEST PASS checks={_checks}");
        else
            GD.PushError($"HOME_STORY_GUIDES_RUNTIME_TEST FAIL failures={_failures} checks={_checks}");
        GetTree().Quit(_failures == 0 ? 0 : 1);
    }

    private HomeStoryGuideController MakeController(PlayerController player,
        WorkbenchInteractable workbench, StoreInteractable store)
    {
        var controller = GD.Load<PackedScene>(
            "res://features/exploration/story_guides/home_story_guides.tscn")
            .Instantiate<HomeStoryGuideController>();
        controller.Player = player;
        controller.Workbench = workbench;
        controller.WorkbenchUI = _workbenchUi;
        controller.Store = store;
        controller.ShopUI = _shopUi;
        return controller;
    }

    private static WorkbenchInteractable MakeWorkbench(WorkbenchUI ui)
    {
        var target = new WorkbenchInteractable { Name = "Workbench", WorkbenchUI = ui };
        AddInteractableChildren(target);
        return target;
    }

    private static StoreInteractable MakeStore(ShopUI ui)
    {
        var target = new StoreInteractable { Name = "Store", ShopUI = ui, RequiredLevel = 1 };
        AddInteractableChildren(target);
        return target;
    }

    private static void AddInteractableChildren(InteractableBase target)
    {
        target.AddChild(new Sprite2D { Name = "Sprite" });
        target.AddChild(new CollisionShape2D
        {
            Name = "DetectionRange",
            Shape = new CircleShape2D { Radius = 120 },
        });
        var clickZone = new Area2D { Name = "ClickZone", CollisionLayer = 2, CollisionMask = 0 };
        clickZone.AddChild(new CollisionShape2D
        {
            Name = "ClickShape",
            Shape = new RectangleShape2D { Size = new Vector2(180, 180) },
        });
        target.AddChild(clickZone);
    }

    private int CountMousy()
    {
        int count = 0;
        foreach (Node child in GetChildren())
            if (child is NPCBase) count++;
        return count;
    }

    private void EndDialogicTimeline()
    {
        Node dialogic = GetNodeOrNull<Node>("/root/Dialogic");
        if (dialogic?.Get("current_timeline").AsGodotObject() != null)
            dialogic.Call("end_timeline", true);
    }

    private async System.Threading.Tasks.Task WaitFrames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private static bool ReadBool(Dictionary state, string key) =>
        state != null && state.TryGetValue(key, out Variant value) && value.AsBool();

    private void Check(bool condition, string description)
    {
        _checks++;
        if (condition) return;
        _failures++;
        GD.PushError($"HOME_STORY_GUIDES_RUNTIME_TEST: {description}");
    }
}
