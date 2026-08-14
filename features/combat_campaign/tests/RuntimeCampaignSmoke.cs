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
        ValidateCatalog();

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

        // Battle 4 phase one: two fixed hands.
        StartPreparedWave();
        CheckWave(3, 4, 1, ("true_hand", 21), ("false_hand", 21));
        ValidateCoreBuffActionIntegration();
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

    private void ValidateCatalog()
    {
        var data = DataManager.Instance;
        Check(data != null, "DataManager autoload must exist");
        if (data == null) return;
        data.EnsureMvpCatalogLoaded();
        Check(data.CardData.Count == 14, $"MVP card catalog must contain exactly 14 cards, got {data.CardData.Count}");
        Check(data.CardCounts.Count == 14, $"MVP card inventory must contain exactly 14 card IDs, got {data.CardCounts.Count}");
        Check(data.GetOwnedCards().Count == 14, "all 14 MVP cards must be selectable in the inventory");
        Check(data.ItemData.Count == 10, $"MVP item catalog must contain exactly 10 items, got {data.ItemData.Count}");
        Check(data.ItemCounts.Count == 10, $"MVP item inventory must contain exactly 10 item IDs, got {data.ItemCounts.Count}");
        int totalItemStock = 0;
        foreach (var pair in data.ItemCounts)
        {
            Check(pair.Value > 0, $"item {pair.Key} must have positive stock");
            totalItemStock += pair.Value;
        }
        Check(totalItemStock == 10, $"configured item stock total must be 10, got {totalItemStock}");
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
        Check(ActionId(sharkk) == "sharkk_sand_retreat",
            $"Sharkk mid-range heavy-hit response must be sand retreat, got {ActionId(sharkk)}");

        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn && battle.RoundNumber == 2,
            "Sharkk retreat turn did not resolve");
        Check(sharkk.MapPosition == 7, $"Sharkk's sand retreat must move two cells from 5 to 7, got {sharkk.MapPosition}");
        Check(ActionId(sharkk) == "sharkk_charge",
            $"prepared Sharkk must lock charge next turn, got {ActionId(sharkk)}");

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
        Check(dangerCells.Length > 0 && System.Array.IndexOf(dangerCells, 1) >= 0,
            "charge danger area must include the final wall cell");
        predictor?.QueueFree();

        // Charge approaches, damages, and pushes the player to the last legal wall cell.
        int hpBeforeCharge = _host.Player.CurrentHp;
        battle.EndTurn();
        await WaitFor(() => battle.CurrentPhase == BattleManager.Phase.PlayerTurn && battle.RoundNumber == 3,
            "Sharkk charge turn did not resolve");
        Check(_host.Player.MapPosition == 1,
            $"leftward Sharkk charge must push the player to wall cell 1, got {_host.Player.MapPosition}");
        Check(_host.Player.CurrentHp == hpBeforeCharge - 10,
            $"Sharkk charge must deal 10 damage, HP {hpBeforeCharge} -> {_host.Player.CurrentHp}");
        Check(ActionId(sharkk) == "sharkk_stunned",
            $"post-charge Sharkk must expose the hard-recovery intent, got {ActionId(sharkk)}");
    }

    private void ValidateCoreBuffActionIntegration()
    {
        var resolver = _host.EffectResolver;
        var battle = _host.BattleManager;
        var playerStats = _host.Player.GetStats();
        var trueHand = _host.EnemyManager.GetAliveByRole("true_hand");
        var falseHand = _host.EnemyManager.GetAliveByRole("false_hand");
        Check(resolver != null && trueHand != null && falseHand != null,
            "Core Buff integration requires resolver and both hands");
        if (resolver == null || trueHand == null || falseHand == null) return;

        playerStats.Call(GDScriptKeys.Stats.ClearBuffs);
        _host.Player.SetMapPosition(4);
        var trueBeam = GD.Load<Resource>(
            "res://features/enemy_ai_node/resources/actions/core_true_guard_beam.tres");
        Check(resolver.ExecuteEnemyPatternHitEffects(trueBeam, battle, trueHand),
            "True beam even-cell hit must execute its mounted Buff effect");
        Check(playerStats.Call(GDScriptKeys.Stats.GetBuffStacks, "true").AsInt32() == 3,
            "True beam must apply three True layers");

        playerStats.Call(GDScriptKeys.Stats.ClearBuffs);
        var falseBeam = GD.Load<Resource>(
            "res://features/enemy_ai_node/resources/actions/core_false_break_beam.tres");
        Check(resolver.ExecuteEnemyPatternHitEffects(falseBeam, battle, falseHand),
            "False beam even-cell hit must execute its mounted Buff effect");
        Check(playerStats.Call(GDScriptKeys.Stats.GetBuffStacks, "false").AsInt32() == 3,
            "False beam must apply three False layers");

        playerStats.Call(GDScriptKeys.Stats.ClearBuffs);
        var disable = GD.Load<Resource>(
            "res://features/enemy_ai_node/resources/actions/core_false_stun.tres");
        Check(resolver.ExecuteEnemyAction(disable, battle, falseHand),
            "Core disable action must execute through CombatEffectData");
        resolver.ResolvePendingBuffApplications(battle);
        Check(playerStats.Call(GDScriptKeys.Stats.GetBuffStacks, "disabled").AsInt32() == 1,
            "Core disable action must apply one Disabled layer");
        playerStats.Call(GDScriptKeys.Stats.ClearBuffs);

        falseHand.TakeDamage(5);
        int wounded = falseHand.CurrentHp;
        var healPackage = GD.Load<Resource>(
            "res://features/enemy_ai_node/resources/actions/core_true_send_heal.tres");
        Check(resolver.HasDeferredEnemyRoleEffects(healPackage),
            "Core heal action must expose a deferred role effect");
        Check(resolver.ExecuteDeferredEnemyRoleEffects(healPackage, battle),
            "Core heal package must target False hand through EffectResolver");
        Check(falseHand.CurrentHp == wounded,
            "deployed medkit must not heal before the recipient turn starts");
        Check(falseHand.GetStats().Call(GDScriptKeys.Stats.GetBuffStacks,
            "deployed_medkit").AsInt32() == 5,
            "False hand must hold a five-point deployed medkit");
        resolver.TickActorBuffs(falseHand, battle, true);
        Check(falseHand.CurrentHp == falseHand.MaxHp,
            "deployed medkit must heal False hand at its next turn start");

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
