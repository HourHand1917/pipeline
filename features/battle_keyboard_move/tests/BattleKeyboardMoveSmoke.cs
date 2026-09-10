using Godot;
using System;
using System.Threading.Tasks;

/// <summary>
/// A/D 键盘移动验收：加载真实 boom 战斗场景，走真实输入管线按键，
/// 验证一格移动、能量消耗、能量不足与敌人阻挡。
/// </summary>
public partial class BattleKeyboardMoveSmoke : Node
{
    private int _checks;

    public override void _Ready()
    {
        GetWindow().Size = new Vector2I(1440, 1080);
        CallDeferred(MethodName.Run);
    }

    private async void Run()
    {
        try
        {
            // 战斗场景 _Ready 读 BattleDirector.PendingRulesPath（私有 setter）。
            // 测试用反射注入规则，避免走 StartBattle 的真实场景转场。
            typeof(BattleDirector)
                .GetProperty("PendingRulesPath")
                ?.SetValue(BattleDirector.Instance, "res://features/enemy_ai_node/rules/boom_rules.tres");

            PackedScene packed = ResourceLoader.Load<PackedScene>(
                "res://Scenes/game_scene/boom_battle_scene.tscn");
            Check(packed != null, "production boom battle scene loads");
            Node level = packed.Instantiate();
            AddChild(level);
            await WaitFrames(12);

            BattleScene battle = level as BattleScene;
            Check(battle != null && battle.BattleManager != null && battle.BattleManager.Player != null,
                "battle scene wires its BattleManager and player");
            if (battle == null) return;

            BattleManager bm = battle.BattleManager;
            PlayerBattle player = bm.Player;
            Check(bm.CurrentPhase == BattleManager.Phase.PlayerTurn, "battle starts on the player turn");

            // boom_test_map：8 格，玩家起点 2，敌人起点 6；move_energy_cost=1，每回合能量 3。
            int startPos = player.MapPosition;
            int startEnergy = player.Energy;
            Check(startEnergy >= 3, "player starts with the expected turn energy");

            // A 向左一格 + 消耗 1
            await PressKey(Key.A);
            Check(player.MapPosition == startPos - 1, "A moves one cell left");
            Check(player.Energy == startEnergy - 1, "A move consumes one energy");

            // D 向右一格 + 消耗 1
            await PressKey(Key.D);
            Check(player.MapPosition == startPos, "D moves one cell right");
            Check(player.Energy == startEnergy - 2, "D move consumes one energy");

            // 能量耗尽后按键：位置与能量都不变（能量门）
            await PressKey(Key.D);
            Check(player.MapPosition == startPos + 1, "third move still succeeds on the last energy");
            int exhaustedPos = player.MapPosition;
            await PressKey(Key.D);
            Check(player.MapPosition == exhaustedPos && player.Energy == 0,
                "move without energy is refused and consumes nothing");

            // 补能量后连续向右，直到被敌人（6 格）阻挡
            bm.AddPlayerEnergy(10);
            bool blocked = false;
            for (int i = 0; i < 4 && !blocked; i++)
            {
                int before = player.MapPosition;
                int energyBefore = player.Energy;
                await PressKey(Key.D);
                if (player.MapPosition == before)
                {
                    Check(player.Energy == energyBefore, "blocked move consumes no energy");
                    blocked = true;
                }
                else
                {
                    Check(player.MapPosition == before + 1, "D keeps moving one cell right");
                    Check(player.Energy == energyBefore - 1, "each D move consumes one energy");
                }
            }
            Check(blocked, "movement is blocked by the enemy");

            GD.Print($"BATTLE_KEYBOARD_MOVE_SMOKE_PASS checks={_checks}");
            GetTree().Quit(0);
        }
        catch (Exception exception)
        {
            GD.PushError($"BATTLE_KEYBOARD_MOVE_SMOKE_FAIL: {exception}");
            GetTree().Quit(1);
        }
    }

    private async Task PressKey(Key key)
    {
        Viewport viewport = GetViewport();
        viewport.PushInput(new InputEventKey { Keycode = key, Pressed = true }, true);
        viewport.PushInput(new InputEventKey { Keycode = key, Pressed = false }, true);
        await WaitFrames(3);
    }

    private async Task WaitFrames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) throw new InvalidOperationException(message);
    }
}
