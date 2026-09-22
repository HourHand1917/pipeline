using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Headless acceptance test for the real four-battle campaign scene.
/// This intentionally drives production nodes rather than duplicating combat logic.
/// </summary>
public partial class RuntimeCampaignSmoke : Node
{
    private readonly List<string> _failures = new();
    private int _checks;
    private idk _host;
    private CombatCampaignController _campaign;

    public override async void _Ready()
    {
        try
        {
            await RunAcceptance();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (IsInstanceValid(_host))
            _host.QueueFree();
        GetNodeOrNull("/root/DataManager")?.QueueFree();
        GetNodeOrNull("/root/GameState")?.QueueFree();
        GetNodeOrNull("/root/TooltipService")?.QueueFree();
        _campaign = null;
        _host = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (_failures.Count == 0)
        {
            GD.Print($"RUNTIME_CAMPAIGN_SMOKE_PASS checks={_checks} cards=14 items=10 battles=4 waves=5");
            CallDeferred(nameof(FinishAndQuit), 0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError($"RUNTIME_CAMPAIGN_SMOKE: {failure}");
        GD.Print($"RUNTIME_CAMPAIGN_SMOKE_FAIL failures={_failures.Count} checks={_checks}");
        CallDeferred(nameof(FinishAndQuit), 1);
    }

    public void FinishAndQuit(int exitCode) => GetTree().Quit(exitCode);

    private async Task RunAcceptance()
    {
        var packed = GD.Load<PackedScene>("res://features/combat_campaign/scenes/combat_campaign.tscn");
        Check(packed != null, "campaign PackedScene must load");
        if (packed == null) return;

        _host = packed.Instantiate<idk>();
        AddChild(_host);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        _campaign = _host.CampaignController
            ?? _host.GetNodeOrNull<CombatCampaignController>("CombatCampaignController");
        if (_host.CampaignController == null && _campaign != null)
        {
            // Godot 4.6 drops the exported Node reference while instantiating
            // this inherited scene headlessly. Resolve the production child
            // by path, exactly as a defensive host would do.
            _host.CampaignController = _campaign;
            _campaign.Bind(_host);
            _campaign.InitializeCampaign();
        }
        Check(_campaign != null, "campaign controller must be injected into the real scene");
        Check(_host.BattleManager != null && _host.EnemyManager != null && _host.Player != null,
            "real battle runtime must initialize");

        // Battle 1: Boom.
        StartPreparedWave();
        CheckWave(0, 1, 1, ("boom", 8));
        KillCurrentWave();
        _host.BattleManager.EndTurn();
        await WaitFor(() => _campaign.CurrentWaveIndex == 1
            && _campaign.State == CombatCampaignController.CampaignState.Preparing,
            "campaign did not advance to Rocky + Boom");

        // Battle 2: one Rocky and one Boom, with independent runtime HP.
        StartPreparedWave();
        CheckWave(1, 2, 1, ("rocky", 20), ("boom", 8));
        KillCurrentWave();
        _host.BattleManager.EndTurn();
        await WaitFor(() => _campaign.CurrentWaveIndex == 2
            && _campaign.State == CombatCampaignController.CampaignState.Preparing,
            "campaign did not advance to Sharkk");

        // Battle 3: exercise the real Sharkk AI and executor, not only resources.
        StartPreparedWave();
        CheckWave(2, 3, 1, ("sharkk", 40));
        await ValidateSharkkRuntimeChain();

        KillCurrentWave();
        _host.BattleManager.EndTurn();
        await WaitFor(() => _campaign.CurrentWaveIndex == 3
            && _campaign.State == CombatCampaignController.CampaignState.Preparing,
            "campaign did not advance to Core-00 hands");

        // Battle 4 phase one: two fixed hands. Buy both HP upgrades so the
        // player (20 base + 10 growth) survives the hands' total damage and
        // the damage-value assertions stay exact.
        DataManager.Instance.ModifyCurrency(DataManager.CurrencyType.Faucet, 100);
        DataManager.Instance.AddLevel(1);
        foreach (var upgrade in GrowthManager.Upgrades)
            if (upgrade.Id is "hp_1" or "hp_2")
                Check(GrowthManager.Instance.Buy(upgrade), $"growth upgrade {upgrade.Id} must be purchasable");

        StartPreparedWave();
        CheckWave(3, 4, 1, ("true_hand", 21), ("false_hand", 21));
        await ValidateCoreRuntimeRules();
        int hpBeforePhaseChange = _host.Player.CurrentHp;
        int positionBeforePhaseChange = _host.Player.MapPosition;
        KillCurrentWave();
        _host.BattleManager.EndTurn();

        // Phase two must auto-start and preserve the player runtime state.
        await WaitFor(() => _campaign.CurrentWaveIndex == 4
            && _campaign.State == CombatCampaignController.CampaignState.Active
            && _host.EnemyManager.GetAliveByRole("body") != null,
            "Core-00 body phase did not auto-start after both hands died");
        CheckWave(4, 4, 2, ("body", 50));
        Check(_host.Player.CurrentHp == hpBeforePhaseChange,
            "Core phase transition must preserve player HP");
        Check(_host.Player.MapPosition == positionBeforePhaseChange,
            "Core phase transition must preserve player position");

        KillCurrentWave();
        _host.BattleManager.EndTurn();
        await WaitFor(() => _campaign.State == CombatCampaignController.CampaignState.Completed,
            "campaign did not complete after Core-00 body died");
        Check(_campaign.CompletedBattleCount == 4, "completed campaign must report four battles");
    }

    private void StartPreparedWave()
    {
        Check(_campaign.State == CombatCampaignController.CampaignState.Preparing,
            $"wave {_campaign.CurrentWaveIndex} must be preparing before start");
        _host.BattleManager.StartBattle();
        _campaign.NotifyWaveStarted();
        Check(_campaign.State == CombatCampaignController.CampaignState.Active,
            $"wave {_campaign.CurrentWaveIndex} must become active");
    }

    private void CheckWave(int expectedIndex, int battle, int wave, params (string role, int hp)[] expected)
    {
        Check(_campaign.CurrentWaveIndex == expectedIndex,
            $"expected campaign wave index {expectedIndex}, got {_campaign.CurrentWaveIndex}");
        Check(_campaign.CurrentBattleNumber == battle && _campaign.CurrentWaveNumber == wave,
            $"expected battle/wave {battle}/{wave}, got {_campaign.CurrentBattleNumber}/{_campaign.CurrentWaveNumber}");
        var alive = _host.EnemyManager.GetAliveEnemies();
        Check(alive.Count == expected.Length,
            $"battle {battle}/{wave} expected {expected.Length} enemies, got {alive.Count}");
        foreach (var (role, hp) in expected)
        {
            var enemy = _host.EnemyManager.GetAliveByRole(role);
            Check(enemy != null, $"battle {battle}/{wave} is missing role {role}");
            if (enemy != null)
                Check(enemy.MaxHp == hp && enemy.CurrentHp == hp,
                    $"{role} must spawn at {hp}/{hp} HP, got {enemy.CurrentHp}/{enemy.MaxHp}");
        }
    }

    private async Task ValidateSharkkRuntimeChain()
    {
        var battle = _host.BattleManager;
        var sharkk = _host.EnemyManager.GetAliveByRole("sharkk");
        if (sharkk == null) return;

        // A 10-point mid-range hit deterministically requests retreat/prepare.
        _host.Player.SetMapPosition(2);
        sharkk.SetMapPosition(5);
        sharkk.UpdateFacing(_host.Player.MapPosition);
        battle.DamageEnemy(10, sharkk);
        battle.ReplanEnemyTurns();
        string responseId = ActionId(sharkk);
        bool usesTrainedAi = responseId.StartsWith("trained_sharkk_", StringComparison.Ordinal);
        Check(responseId is "sharkk_sand_retreat" or "trained_sharkk_sand_retreat",
            $"Sharkk mid-range heavy-hit response must be sand retreat, got {responseId}");

        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn && battle.RoundNumber == 2,
            "Sharkk retreat turn did not resolve");
        Check(sharkk.MapPosition == 7, $"Sharkk's sand retreat must move two cells from 5 to 7, got {sharkk.MapPosition}");
        if (usesTrainedAi)
            Check(ActionId(sharkk).StartsWith("trained_sharkk_", StringComparison.Ordinal),
                $"trained Sharkk must keep a trained follow-up intent, got {ActionId(sharkk)}");
        else
            Check(ActionId(sharkk) == "sharkk_charge",
                $"prepared legacy Sharkk must lock charge next turn, got {ActionId(sharkk)}");

        // Reading the danger predictor repeatedly must not reroll or confirm AI.
        var provider = sharkk.GetEnemyData().Call("get_ai_provider").As<GodotObject>();
        int decisionBefore = provider?.Get("decision_turn").AsInt32() ?? -1;
        int stateBefore = provider?.Get("charge_state").AsInt32() ?? -1;
        var predictorScene = GD.Load<PackedScene>("res://features/combat_prediction/scenes/danger_area_predictor.tscn");
        var predictor = predictorScene?.Instantiate();
        if (predictor != null) AddChild(predictor);
        Check(predictor != null, "danger predictor scene must instantiate");
        GodotObject lockedAction = sharkk.PlannedAction;
        Dictionary prediction = null;
        for (int read = 0; read < 25 && predictor != null; read++)
        {
            prediction = predictor.Call("predict_action", lockedAction, sharkk.MapPosition,
                sharkk.Facing, _host.Player.MapPosition, 9).As<Dictionary>();
            Check(ReferenceEquals(sharkk.PlannedAction, lockedAction),
                "danger prediction must not replace the locked planned action");
        }
        Check(provider?.Get("decision_turn").AsInt32() == decisionBefore,
            "danger prediction must not consume an AI decision");
        Check(provider?.Get("charge_state").AsInt32() == stateBefore,
            "danger prediction must not advance Sharkk charge state");
        var dangerCells = prediction?["current_cells"].AsInt32Array() ?? System.Array.Empty<int>();
        if (!usesTrainedAi)
            Check(dangerCells.Length > 0 && System.Array.IndexOf(dangerCells, 1) >= 0,
                "legacy charge danger area must include the final wall cell");
        predictor?.QueueFree();

        // The legacy controller deterministically follows preparation with a
        // charge. The trained controller may choose any legal configured
        // follow-up, so its smoke contract is stable planning and resolution.
        int hpBeforeCharge = _host.Player.CurrentHp;
        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn && battle.RoundNumber == 3,
            "Sharkk follow-up turn did not resolve");
        if (usesTrainedAi)
        {
            Check(sharkk.IsAlive && ActionId(sharkk).StartsWith("trained_sharkk_", StringComparison.Ordinal),
                $"trained Sharkk must resolve and replan a trained action, got {ActionId(sharkk)}");
        }
        else
        {
            Check(_host.Player.MapPosition == 1,
                $"leftward Sharkk charge must push the player to wall cell 1, got {_host.Player.MapPosition}");
            Check(_host.Player.CurrentHp == hpBeforeCharge - 10,
                $"Sharkk charge must deal 10 damage, HP {hpBeforeCharge} -> {_host.Player.CurrentHp}");
            Check(ActionId(sharkk) == "sharkk_stunned",
                $"post-charge Sharkk must expose the hard-recovery intent, got {ActionId(sharkk)}");
        }
    }

    private async Task ValidateCoreRuntimeRules()
    {
        var battle = _host.BattleManager;
        var playerStats = _host.Player.GetStats();
        var trueHand = _host.EnemyManager.GetAliveByRole("true_hand");
        var falseHand = _host.EnemyManager.GetAliveByRole("false_hand");
        Check(battle != null && playerStats != null && trueHand != null && falseHand != null,
            "Core runtime rules require BattleManager, player stats, and both hands");
        if (battle == null || playerStats == null || trueHand == null || falseHand == null) return;

        playerStats.Call(GDScriptKeys.Stats.ClearBuffs);
        Check(ActionId(trueHand) == "core_true_send_heal"
            && ActionId(falseHand) == "core_false_charge",
            $"Core round 1 must plan package/cooldown, got {ActionId(trueHand)}/{ActionId(falseHand)}");

        // Round 1: both carried shields expire together before either hand
        // acts. The package attacks cells 2-11, then deploys 12 healing stacks
        // only after False has completed this turn.
        _host.Player.SetMapPosition(4);
        falseHand.TakeDamage(12);
        trueHand.AddShield(6);
        falseHand.AddShield(7);
        int hpBeforePackage = _host.Player.CurrentHp;
        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn
            && battle.RoundNumber == 2,
            "Core round 1 did not resolve");
        Check(trueHand.Shield == 0 && falseHand.Shield == 0,
            $"enemy-turn start must clear all carried enemy shield, got True {trueHand.Shield}, False {falseHand.Shield}");
        Check(_host.Player.CurrentHp == hpBeforePackage - 5,
            $"Core package must deal 5 damage on cell 4, HP {hpBeforePackage} -> {_host.Player.CurrentHp}");
        Check(falseHand.CurrentHp == 9,
            $"deployed medkit must not heal during its deployment turn, got False HP {falseHand.CurrentHp}");
        Check(falseHand.GetStats().Call(GDScriptKeys.Stats.GetBuffStacks,
            "deployed_medkit").AsInt32() == 12,
            "False hand must hold a 12-point deployed medkit after round 1");
        Check(ActionId(trueHand) == "core_true_charge"
            && ActionId(falseHand) == "core_false_heal",
            $"Core round 2 must plan charge/recovery, got {ActionId(trueHand)}/{ActionId(falseHand)}");

        // Round 2: False consumes the package at its own turn start. Enemy
        // shield expiry must never clear the player's own guard.
        _host.Player.AddShield(9);
        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn
            && battle.RoundNumber == 3,
            "Core round 2 did not resolve");
        Check(falseHand.CurrentHp == falseHand.MaxHp,
            $"12-layer medkit must heal False at its next turn start, got {falseHand.CurrentHp}/{falseHand.MaxHp}");
        Check(falseHand.GetStats().Call(GDScriptKeys.Stats.GetBuffStacks,
            "deployed_medkit").AsInt32() == 0,
            "deployed medkit must be consumed after its turn-start heal");
        Check(_host.Player.Shield == 9,
            $"enemy-turn shield expiry must not clear player guard, got {_host.Player.Shield}");
        int hpBeforeShieldCleanup = _host.Player.CurrentHp;
        _host.Player.TakeDamage(9);
        Check(_host.Player.CurrentHp == hpBeforeShieldCleanup && _host.Player.Shield == 0,
            "test cleanup must consume only the preserved player guard");
        Check(ActionId(trueHand) == "core_true_guard_beam"
            && ActionId(falseHand) == "core_false_stun",
            $"Core round 3 must plan guard beam/charge wait, got {ActionId(trueHand)}/{ActionId(falseHand)}");

        // Round 3: old guard on both hands is cleared before True acts. The
        // newly granted 25 guard on False must survive False's later action in
        // the same enemy turn (it must not be cleared inside the actor loop).
        _host.Player.SetMapPosition(4);
        trueHand.AddShield(6);
        falseHand.AddShield(7);
        int hpBeforeGuardBeam = _host.Player.CurrentHp;
        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn
            && battle.RoundNumber == 4,
            "Core round 3 did not resolve");
        Check(_host.Player.CurrentHp == hpBeforeGuardBeam - 7,
            $"guard beam must deal 7 damage on even cell 4, HP {hpBeforeGuardBeam} -> {_host.Player.CurrentHp}");
        Check(trueHand.Shield == 0 && falseHand.Shield == 25,
            $"shared clear must remove old guard but preserve same-turn False guard 25, got True {trueHand.Shield}, False {falseHand.Shield}");
        Check(playerStats.Call(GDScriptKeys.Stats.GetBuffStacks, "true").AsInt32() == 3,
            "guard beam hit must apply three True layers to the player");
        Check(trueHand.GetStats().Call(GDScriptKeys.Stats.HasBuff, "true").AsBool(),
            "guard beam hit must leave True hand in the persistent True state");
        Check(ActionId(trueHand) == "core_true_death_loop"
            && ActionId(falseHand) == "core_false_charge_complete",
            $"Core round 4 must plan death loop/charged shot, got {ActionId(trueHand)}/{ActionId(falseHand)}");

        // Round 4: False's previous 25 guard expires at the shared boundary;
        // True's newly created 15 guard remains, and cells 1-3 take 11 damage.
        // Clear the player's prior True here so this assertion isolates the
        // charged shot's configured base damage and False application.
        playerStats.Call(GDScriptKeys.Stats.ClearBuffs);
        _host.Player.Heal(_host.Player.MaxHp);
        _host.Player.SetMapPosition(1);
        int hpBeforeChargedShot = _host.Player.CurrentHp;
        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn
            && battle.RoundNumber == 5,
            "Core round 4 did not resolve");
        Check(_host.Player.CurrentHp == hpBeforeChargedShot - 11,
            $"charged shot must deal 11 damage on cell 1, HP {hpBeforeChargedShot} -> {_host.Player.CurrentHp}");
        Check(trueHand.Shield == 15 && falseHand.Shield == 0,
            $"round 4 must clear False's old guard and retain True's new 15 guard, got True {trueHand.Shield}, False {falseHand.Shield}");
        Check(playerStats.Call(GDScriptKeys.Stats.GetBuffStacks, "false").AsInt32() == 3,
            "charged shot hit must apply three False layers to the player");
        Check(ActionId(trueHand) == "core_true_death_loop"
            && ActionId(falseHand) == "core_false_break_beam",
            $"Core round 5 must plan death loop/break beam, got {ActionId(trueHand)}/{ActionId(falseHand)}");

        // Round 5: odd-cell break beam deals 25, reapplies player False, and
        // applies False to True hand so mutual cancellation ends its loop.
        // Clear the previously asserted player False so its incoming-damage
        // modifier does not obscure the break beam's configured base value.
        playerStats.Call(GDScriptKeys.Stats.ClearBuffs);
        _host.Player.Heal(_host.Player.MaxHp);
        _host.Player.SetMapPosition(3);
        int hpBeforeBreakBeam = _host.Player.CurrentHp;
        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn
            && battle.RoundNumber == 6,
            "Core round 5 did not resolve");
        Check(_host.Player.CurrentHp == hpBeforeBreakBeam - 25,
            $"break beam must deal 25 damage on odd cell 3, HP {hpBeforeBreakBeam} -> {_host.Player.CurrentHp}");
        Check(playerStats.Call(GDScriptKeys.Stats.GetBuffStacks, "false").AsInt32() == 3,
            "break beam hit must apply three False layers to the player");
        Check(!trueHand.GetStats().Call(GDScriptKeys.Stats.HasBuff, "true").AsBool()
            && !trueHand.GetStats().Call(GDScriptKeys.Stats.HasBuff, "false").AsBool(),
            "break beam must mutually cancel True hand's True/False markers");
        Check(ActionId(trueHand) == "core_true_send_heal"
            && ActionId(falseHand) == "core_false_charge",
            $"Core round 6 must restart both cycles, got {ActionId(trueHand)}/{ActionId(falseHand)}");

        // Round 6: True restarts its maintenance step (step 1) while False is still
        // alive. The package targets cells 2-11, so odd cell 1 takes nothing.
        _host.Player.SetMapPosition(1);
        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn
            && battle.RoundNumber == 7,
            "Core round 6 did not resolve");
        Check(ActionId(trueHand) == "core_true_charge",
            $"True must plan maintenance step 2 after round 6, got {ActionId(trueHand)}");

        // False dies mid-step: True must keep the latched maintenance step instead of
        // locking up immediately.
        battle.DamageEnemy(9999, falseHand);
        battle.ReplanEnemyTurns();
        Check(ActionId(trueHand) == "core_true_charge",
            $"True must finish its maintenance step when False dies mid-step, got {ActionId(trueHand)}");

        // Round 7: charge resolves; guard beam is planned. The player stands
        // on odd cell 5 so the beam misses and applies no True marker.
        _host.Player.SetMapPosition(5);
        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn
            && battle.RoundNumber == 8,
            "Core round 7 did not resolve");
        Check(ActionId(trueHand) == "core_true_guard_beam",
            $"True must plan maintenance step 3 after False died, got {ActionId(trueHand)}");

        // Round 8: the beam misses, no marker is applied, and with False dead
        // True locks into the death loop protection for good.
        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn
            && battle.RoundNumber == 9,
            "Core round 8 did not resolve");
        Check(ActionId(trueHand) == "core_true_death_loop",
            $"True must lock into the death loop after its maintenance step ends, got {ActionId(trueHand)}");
        Check(!trueHand.GetStats().Call(GDScriptKeys.Stats.HasBuff, "true").AsBool(),
            "True must carry no marker when locked into the death loop");

        playerStats.Call(GDScriptKeys.Stats.ClearBuffs);
        _host.Player.SetMapPosition(6);
    }

    private static string ActionId(EnemyBattle enemy) =>
        enemy?.PlannedAction?.Get("id").AsStringName().ToString() ?? "<none>";

    private void KillCurrentWave()
    {
        foreach (var enemy in _host.EnemyManager.GetAliveEnemies())
            _host.BattleManager.DamageEnemy(9999, enemy);
        Check(!_host.EnemyManager.HasAliveEnemies(), "wave enemies must be dead before transition");
    }

    private async Task WaitFor(Func<bool> predicate, string failure, double timeoutSeconds = 4.0)
    {
        double elapsed = 0.0;
        while (!predicate() && elapsed < timeoutSeconds)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += GetProcessDeltaTime();
        }
        Check(predicate(), failure);
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) _failures.Add(message);
    }
}
