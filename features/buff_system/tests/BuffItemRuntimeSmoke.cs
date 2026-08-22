using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Integration acceptance for the designed Buff and item set.  It instantiates
/// the production campaign scene and drives the production BattleManager,
/// EffectResolver, Stats, BoardManager and DataManager paths.
/// </summary>
public partial class BuffItemRuntimeSmoke : Node
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

        if (IsInstanceValid(_host)) _host.QueueFree();
        GetNodeOrNull("/root/DataManager")?.QueueFree();
        GetNodeOrNull("/root/GameState")?.QueueFree();
        GetNodeOrNull("/root/TooltipService")?.QueueFree();
        _campaign = null;
        _host = null;
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (_failures.Count == 0)
        {
            GD.Print($"BUFF_ITEM_RUNTIME_SMOKE_PASS checks={_checks} buffs=9 items=10");
            CallDeferred(nameof(FinishAndQuit), 0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError($"BUFF_ITEM_RUNTIME_SMOKE: {failure}");
        GD.Print($"BUFF_ITEM_RUNTIME_SMOKE_FAIL failures={_failures.Count} checks={_checks}");
        CallDeferred(nameof(FinishAndQuit), 1);
    }

    public void FinishAndQuit(int exitCode) => GetTree().Quit(exitCode);

    private async Task RunAcceptance()
    {
        var packed = GD.Load<PackedScene>("res://features/combat_campaign/scenes/combat_campaign.tscn");
        Check(packed != null, "production campaign scene must load");
        if (packed == null) return;

        _host = packed.Instantiate<idk>();
        AddChild(_host);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        _campaign = _host.CampaignController
            ?? _host.GetNodeOrNull<CombatCampaignController>("CombatCampaignController");
        if (_host.CampaignController == null && _campaign != null)
        {
            _host.CampaignController = _campaign;
            _campaign.Bind(_host);
            _campaign.InitializeCampaign();
        }
        Check(_campaign != null, "campaign controller must be available");
        Check(_host.BattleManager != null && _host.EffectResolver != null
            && _host.BoardManager != null && _host.Player != null,
            "production combat runtime must initialize");
        if (_campaign == null || _host.BattleManager == null) return;

        _host.BattleManager.StartBattle();
        _campaign.NotifyWaveStarted();
        Check(_host.BattleManager.CurrentPhase == BattleManager.Phase.PlayerTurn,
            "battle must start in PlayerTurn");

        TestDamageAndLifecycleBuffs();
        TestBoardBuffs();
        await TestFarthestGrenade();
    }

    private void TestDamageAndLifecycleBuffs()
    {
        var battle = _host.BattleManager;
        var resolver = _host.EffectResolver;
        var playerStats = _host.Player.GetStats();
        var enemy = _host.EnemyManager.GetAliveByRole("boom");
        Check(enemy != null, "Boom must exist for Buff damage tests");
        if (enemy == null) return;

        ClearStats(playerStats);
        HealToFull(enemy);
        AddBuff(playerStats, "strength", 3);
        battle.DamageEnemy(1, enemy);
        Check(enemy.CurrentHp == 4, $"strength 3 must turn damage 1 into 4, HP={enemy.CurrentHp}");

        ClearStats(playerStats);
        HealToFull(enemy);
        AddBuff(playerStats, "true", 3);
        battle.DamageEnemy(5, enemy);
        Check(enemy.CurrentHp == 6, $"True must floor 5/2 to 2, HP={enemy.CurrentHp}");

        ClearStats(playerStats);
        HealToFull(enemy);
        AddBuff(playerStats, "false", 3);
        battle.DamageEnemy(3, enemy);
        Check(enemy.CurrentHp == 3, $"False must turn 3 into 5, HP={enemy.CurrentHp}");

        ClearStats(playerStats);
        AddBuff(playerStats, "true", 3);
        AddBuff(playerStats, "false", 3);
        Check(!HasBuff(playerStats, "true") && !HasBuff(playerStats, "false"),
            "adding opposite True/False must remove both");

        ClearStats(playerStats);
        HealPlayerToFull();
        AddBuff(playerStats, "holographic", 1);
        int hpBefore = _host.Player.CurrentHp;
        battle.DamagePlayer(9);
        Check(_host.Player.CurrentHp == hpBefore - 1,
            "holographic must resolve incoming damage as exactly one");
        resolver.TickActorBuffs(_host.Player, battle, true);
        Check(!HasBuff(playerStats, "holographic"),
            "holographic must expire at the next player turn start");

        battle.DamagePlayer(5);
        int wounded = _host.Player.CurrentHp;
        AddBuff(playerStats, "deployed_medkit", 3);
        resolver.TickActorBuffs(_host.Player, battle, true);
        Check(_host.Player.CurrentHp == Math.Min(_host.Player.MaxHp, wounded + 3),
            "deployed medkit must heal its stack count on owner turn start");
        Check(!HasBuff(playerStats, "deployed_medkit"),
            "deployed medkit must consume itself after healing");

        ClearStats(playerStats);
        _host.Player.ResetEnergy();
        AddBuff(playerStats, "false", 3);
        resolver.TickActorBuffs(_host.Player, battle, true);
        Check(_host.Player.Energy == Math.Max(0, _host.Player.MaxEnergy - 1),
            "False must drain one energy at player turn start");
        resolver.TickActorBuffs(_host.Player, battle, false);
        Check(StackCount(playerStats, "false") == 2,
            "False must lose one layer at player turn end");

        ClearStats(playerStats);
        AddBuff(playerStats, "temporary_strength", 3);
        resolver.TickActorBuffs(_host.Player, battle, false);
        Check(!HasBuff(playerStats, "temporary_strength"),
            "temporary strength must expire at owner turn end");
        ClearStats(playerStats);
    }

    private void TestBoardBuffs()
    {
        var board = _host.BoardManager;
        var battle = _host.BattleManager;
        var resolver = _host.EffectResolver;
        board.ConfigureBoard(new Vector2I(4, 3), false);
        var card = GD.Load<Resource>("res://Resource/card/cards/knuckle_striker.tres");
        var runtime = board.PlaceCard(card, Vector2I.Zero, 0);
        Check(runtime != null, "one-cell card must be placeable for cooldown tests");
        if (runtime == null) return;

        var cell = board.GetCell(Vector2I.Zero);
        var cellStats = cell?.Get(GDScriptKeys.CellRuntime.Stats).As<GodotObject>();
        AddBuff(cellStats, "dust", 2);
        Check(resolver.CanLightCell(cellStats, cell), "dust must not block lighting");
        Check(resolver.GetLightExtraCost(cellStats, cell) == 2,
            "dust two layers must add two lighting energy");
        cellStats.Call(GDScriptKeys.Stats.TickTurnEnd);
        Check(StackCount(cellStats, "dust") == 1, "dust must lose one layer at player turn end");

        var playerStats = _host.Player.GetStats();
        ClearStats(playerStats);
        runtime.Set(GDScriptKeys.CardRuntime.CooldownRemaining, 0);
        runtime.Set(GDScriptKeys.CardRuntime.IsReady, true);
        AddBuff(playerStats, "disabled", 1);
        resolver.TickActorBuffs(_host.Player, battle, true);
        Check(runtime.Get(GDScriptKeys.CardRuntime.CooldownRemaining).AsInt32() >= 1,
            "disabled must place every card into cooldown at turn start");
        Check(!runtime.Get(GDScriptKeys.CardRuntime.IsReady).AsBool(),
            "disabled must clear card ready state");
        resolver.TickActorBuffs(_host.Player, battle, false);
        Check(!HasBuff(playerStats, "disabled"), "disabled one layer must expire at turn end");

        runtime.Set(GDScriptKeys.CardRuntime.CooldownRemaining, 3);
        runtime.Set(GDScriptKeys.CardRuntime.IsReady, false);
        AddBuff(playerStats, "anneal", 1);
        Check(runtime.Get(GDScriptKeys.CardRuntime.CooldownRemaining).AsInt32() == 0,
            "anneal must reset every card cooldown on apply");
        Check(!HasBuff(playerStats, "anneal"), "anneal must remove itself after apply");
        ClearStats(playerStats);
    }

    private async Task TestFarthestGrenade()
    {
        var boom = _host.EnemyManager.GetAliveByRole("boom");
        if (boom == null) return;
        boom.TakeDamage(999);
        _host.BattleManager.EndTurn();
        await WaitFor(() => _campaign.CurrentWaveIndex == 1
            && _campaign.State == CombatCampaignController.CampaignState.Preparing,
            "campaign must advance to the two-enemy wave for grenade test");
        _host.BattleManager.StartBattle();
        _campaign.NotifyWaveStarted();

        var rocky = _host.EnemyManager.GetAliveByRole("rocky");
        boom = _host.EnemyManager.GetAliveByRole("boom");
        Check(rocky != null && boom != null, "grenade test requires Rocky and Boom");
        if (rocky == null || boom == null) return;
        int rockyHp = rocky.CurrentHp;
        int boomHp = boom.CurrentHp;
        Check(Math.Abs(rocky.MapPosition - _host.Player.MapPosition)
            > Math.Abs(boom.MapPosition - _host.Player.MapPosition),
            "Rocky must be the farthest enemy in the configured wave");
        Check(UseItem("grenade"), "grenade must execute with a living enemy");
        Check(rocky.CurrentHp == rockyHp - 5, "grenade must damage the farthest enemy for five");
        Check(boom.CurrentHp == boomHp, "grenade must not damage the nearer enemy");
    }

    private void AddBuff(GodotObject stats, string id, int stacks)
    {
        var buff = GD.Load<Resource>($"res://features/buff_system/resources/buffs/{id}.tres");
        Check(buff != null, $"Buff resource {id} must load");
        if (stats == null || buff == null) return;
        stats.Call(GDScriptKeys.Stats.AddBuff, buff, stacks);
        _host.EffectResolver.ResolvePendingBuffApplications(_host.BattleManager);
    }

    private bool UseItem(string id)
    {
        ClearBag();
        var item = GD.Load<Resource>($"res://Resource/item/mvp/{id}.tres");
        Check(item != null, $"item resource {id} must load");
        if (item == null || !DataManager.Instance.AddItem(item)) return false;
        bool used = _host.BattleManager.UseItem(0);
        if (used)
            Check(DataManager.Instance.ItemBag.Count == 0, $"consumed item {id} must leave the bag");
        return used;
    }

    private void ClearBag()
    {
        var data = DataManager.Instance;
        if (data == null) return;
        while (data.ItemBag.Count > 0) data.DiscardItem(data.ItemBag.Count - 1);
    }

    private static bool HasBuff(GodotObject stats, string id)
        => stats != null && stats.Call(GDScriptKeys.Stats.HasBuff, id).AsBool();

    private static int StackCount(GodotObject stats, string id)
        => stats?.Call(GDScriptKeys.Stats.GetBuffStacks, id).AsInt32() ?? 0;

    private static void ClearStats(GodotObject stats)
    {
        if (stats != null) stats.Call(GDScriptKeys.Stats.ClearBuffs);
    }

    private static void HealToFull(EnemyBattle enemy)
    {
        if (enemy != null && enemy.IsAlive) enemy.Heal(enemy.MaxHp);
    }

    private void HealPlayerToFull()
    {
        if (_host.Player.IsAlive) _host.Player.Heal(_host.Player.MaxHp);
    }

    private async Task WaitFor(Func<bool> predicate, string failure)
    {
        for (int frame = 0; frame < 240 && !predicate(); frame++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        Check(predicate(), failure);
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) _failures.Add(message);
    }
}
