using Godot;
using System;

public partial class F4SequenceContractTest : Node
{
    private int _checks;

    public override void _Ready() => CallDeferred(MethodName.Run);

    private void Run()
    {
        try
        {
            PackedScene f4Packed = ResourceLoader.Load<PackedScene>("res://features/exploration/scenes/f4/f4.tscn");
            Check(f4Packed != null, "F4 scene loads");
            Node2D f4 = f4Packed.Instantiate<Node2D>();
            F4BossSequence sequence = f4.GetNode<F4BossSequence>("Core00Encounter");
            Check(sequence != null, "F4 owns the two-stage sequence trigger");
            Check(f4.GetNode<PlayerController>("Player").Position.X == 150.0f,
                "player enters at the authored F4 entrance, before the arena trigger");
            Check(f4.GetNode<SpawnPoint>("f4_core_return").SpawnId == "f4_core_return",
                "both battle phases return to a real F4 spawn");
            Check(sequence.PhaseOneRulesPath.EndsWith("core00_phase_one_rules.tres"),
                "phase one uses the existing phase-one rules");
            Check(sequence.PhaseTwoRulesPath.EndsWith("core00_phase_two_rules.tres"),
                "phase two uses the existing phase-two rules");
            Check(sequence.PhaseTwoVictoryScenePath.EndsWith("f4_ending_screen.tscn")
                && ResourceLoader.Exists(sequence.PhaseTwoVictoryScenePath),
                "phase-two victory opens the configurable ending scene");
            Check(sequence.LeftStageActor.SpriteFrames.HasAnimation("enter_left")
                && sequence.RightStageActor.SpriteFrames.HasAnimation("enter_right"),
                "phase-one hand-entry animations remain a safe fallback");
            Check(sequence.PhaseOneIntroVideo?.Stream != null,
                "phase one uses the subtitle-free authored intro video");
            Check(sequence.PhaseTwoLeftAnimation.IsEmpty && sequence.PhaseTwoRightAnimation.IsEmpty,
                "phase-two entrance remains an explicit empty Inspector hook");
            Check(!sequence.ForcedDialogueNpc.AutoTriggerByRange
                && !sequence.ForcedDialogueNpc.AutoStartBattleAfterDialogue
                && !sequence.ForcedDialogueNpc.AllowEscapeToExitDialogue,
                "Core-00 dialogue is forced and sequence-owned");
            PackagedHostileNPC packagedDialogue = sequence.ForcedDialogueNpc as PackagedHostileNPC;
            Check(packagedDialogue != null
                && !string.IsNullOrWhiteSpace(packagedDialogue.DefaultTimelinePath)
                && ResourceLoader.Exists(packagedDialogue.DefaultTimelinePath),
                "existing Code_00 timeline is configured for runtime loading");

            PackedScene rockyPacked = ResourceLoader.Load<PackedScene>(
                "res://features/exploration/scenes/f1/f1_2.tscn");
            PackedScene sharkPacked = ResourceLoader.Load<PackedScene>(
                "res://features/exploration/scenes/f2/f2_4.tscn");
            Node rockyRoot = rockyPacked.Instantiate();
            Node sharkRoot = sharkPacked.Instantiate();
            HostileNPC rocky = FindHostile(rockyRoot);
            HostileNPC shark = FindHostile(sharkRoot);
            Check(rocky?.PlayerLevelAfterVictory == 1, "Rocky victory grants story level 1");
            Check(shark?.PlayerLevelAfterVictory == 2, "Sharkk victory grants story level 2");

            PackedScene casinoPacked = ResourceLoader.Load<PackedScene>(
                "res://features/dialogue/npc/scenes/casino_dealer_npc.tscn");
            FriendlyNPC dealer = casinoPacked.Instantiate<FriendlyNPC>();
            Check(dealer.PlayerLevelAfterDialogue == 3,
                "casino robot/dealer normal dialogue grants story level 3");

            DataManager.Instance.SetLevelAtLeast(1);
            DataManager.Instance.SetLevelAtLeast(1);
            Check(DataManager.Instance.Lv == 1, "story level checkpoint is idempotent");
            DataManager.Instance.SetLevelAtLeast(3);
            Check(DataManager.Instance.Lv == 3, "story level checkpoints only move forward");

            f4.QueueFree();
            rockyRoot.QueueFree();
            sharkRoot.QueueFree();
            dealer.QueueFree();
            GD.Print($"F4_SEQUENCE_CONTRACT_TEST_PASS checks={_checks} phases=2 levels=3");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"F4_SEQUENCE_CONTRACT_TEST_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private static HostileNPC FindHostile(Node root)
    {
        if (root is HostileNPC hostile) return hostile;
        foreach (Node child in root.GetChildren())
        {
            HostileNPC found = FindHostile(child);
            if (found != null) return found;
        }
        return null;
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
