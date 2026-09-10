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
            Check(sequence.SequenceAnimationPlayer != null
                && sequence.SequenceAnimationPlayer.HasAnimation("phase_one_intro")
                && sequence.SequenceAnimationPlayer.HasAnimation("phase_one_defeat")
                && sequence.SequenceAnimationPlayer.HasAnimation("phase_two_defeat"),
                "phase intro/defeat animations are driven by the authored AnimationPlayer");
            Check(sequence.PhaseOneDefeatTimeline != null
                && sequence.PhaseOneDefeatTimeline.ResourcePath.EndsWith("Code_00战前.dtl"),
                "phase-one defeat dialogue is the authored pre-phase-two Code_00 timeline");
            Check(sequence.PhaseTwoDefeatTimeline != null
                && sequence.PhaseTwoDefeatTimeline.ResourcePath.EndsWith("Code_00战后.dtl"),
                "phase-two victory owns the authored forced post-battle dialogue");
            Check(sequence.PhaseTwoOutroPersistenceId == "f4_core00_phase_two_outro",
                "phase-two post-battle dialogue has an independent replay guard");
            Check(sequence.PhaseTwoLeftAnimation.IsEmpty && sequence.PhaseTwoRightAnimation.IsEmpty,
                "phase-two entrance remains an explicit empty Inspector hook");
            Check(!sequence.ForcedDialogueNpc.AutoTriggerByRange
                && !sequence.ForcedDialogueNpc.AutoStartBattleAfterDialogue
                && sequence.ForcedDialogueNpc.AllowEscapeToExitDialogue,
                "Core-00 dialogue stays sequence-owned but ESC can skip into the next phase");
            PackagedHostileNPC packagedDialogue = sequence.ForcedDialogueNpc as PackagedHostileNPC;
            Check(packagedDialogue != null
                && !string.IsNullOrWhiteSpace(packagedDialogue.DefaultTimelinePath)
                && ResourceLoader.Exists(packagedDialogue.DefaultTimelinePath),
                "existing Code_00 timeline is configured for runtime loading");

            Resource phaseTwoMap = ResourceLoader.Load<Resource>(
                "res://features/enemy_ai_node/battle_maps/core00_phase_two_map.tres");
            Check(phaseTwoMap != null
                && phaseTwoMap.Get("cell_count").AsInt32() == 7
                && phaseTwoMap.Get("player_start_cell").AsInt32() == 1,
                "Core-00 phase two is a seven-cell track with player at cell 1");
            Godot.Collections.Array<int> enemyStarts =
                phaseTwoMap.Get("enemy_start_cells").As<Godot.Collections.Array<int>>();
            Check(enemyStarts.Count == 1 && enemyStarts[0] == 7,
                "Core-00 phase-two body starts at the opposite endpoint");
            Check(phaseTwoMap.Call("is_configuration_valid").AsBool(),
                "Core-00 phase-two seven-cell map remains valid");

            string[] expectedOutroLines =
            {
                "Code_00: ……系统故障，受到外力破坏……",
                "Code_00: 无法保持系统完整……社会集群断裂……娱乐设施断裂……",
                "Code_00: 紧急维护……紧急维护……无法恢复……无法恢复……",
                "Code_00: 需要修复……需要修复……需要科研人员协助……",
                "Code_00: 谁来救救我……lichen先生…………鼠鼠工程师……",
                "Code_00: 来个人，来个人来救救我……",
                "Code_00: 我很难受……我很难受……",
                "Code_00: …………",
                "reb: 扫描完毕，code00已经停止运转。需要带回去修理吗？",
                "RUBBER: 不了。它带来了太多的伤亡，这样是他最好的结局了。",
                "RUBBER: 但我们可以待一会，为这个AI默哀一下。",
                "RUBBER: 他努力的完成了自己的构想，并为了维持他的构想付出了生命。",
                "reb: 扫描显示，你为他的死亡而高兴，为什么还要为它哀悼？",
                "RUBBER: 因为我是人呀，允许一些灰色地带存在。",
                "RUBBER: 算了，走吧，去找冰淇淋吃！",
            };
            string[] actualOutroLines = FileAccess
                .GetFileAsString("res://features/dialogue/npc/timelines/Code_00战后.dtl")
                .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
            Check(actualOutroLines.Length == expectedOutroLines.Length,
                "Core-00 post-battle dialogue contains exactly the supplied 15 speech segments");
            for (int line = 0; line < expectedOutroLines.Length; line++)
                Check(actualOutroLines[line] == expectedOutroLines[line],
                    $"Core-00 post-battle dialogue line {line + 1} is preserved verbatim");

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
