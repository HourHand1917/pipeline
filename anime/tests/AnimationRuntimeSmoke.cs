using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Real-scene smoke test for the additive animation Hub.  It does not replace
/// production combat: it starts the first campaign wave and observes the same
/// runtime PlayerBattle, EnemyBattle, TrackSlot and EffectResolver nodes.
/// </summary>
public partial class AnimationRuntimeSmoke : Node
{
    private readonly List<string> _failures = new();
    private int _checks;
    private idk _host;

    public override async void _Ready()
    {
        try
        {
            await RunSmoke();
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        if (IsInstanceValid(_host)) _host.QueueFree();
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);

        if (_failures.Count == 0)
        {
            GD.Print($"ANIMATION_RUNTIME_SMOKE_PASS checks={_checks} waves=5 profiles=7 local_anchors=1");
            GetTree().Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError($"ANIMATION_RUNTIME_SMOKE_FAIL: {failure}");
        GetTree().Quit(1);
    }

    private async Task RunSmoke()
    {
        var packed = GD.Load<PackedScene>("res://anime/scenes/animated_combat_campaign.tscn");
        Check(packed != null, "animated campaign wrapper loads");
        if (packed == null) return;

        _host = packed.Instantiate<idk>();
        AddChild(_host);
        await Frames(3);

        var campaign = _host.CampaignController
            ?? _host.GetNodeOrNull<CombatCampaignController>("CombatCampaignController");
        if (_host.CampaignController == null && campaign != null)
        {
            _host.CampaignController = campaign;
            campaign.Bind(_host);
            campaign.InitializeCampaign();
        }

        Check(campaign != null, "campaign controller initializes");
        Check(_host.BattleManager != null && _host.Player != null && _host.EnemyManager != null,
            "real combat runtime initializes");
        if (campaign == null || _host.BattleManager == null) return;

        _host.BattleManager.StartBattle();
        campaign.NotifyWaveStarted();
        await Frames(5);

        BattleAnimationHub hub = _host.GetNodeOrNull<BattleAnimationHub>("BattleAnimationHub");
        Check(hub != null, "wrapper contains BattleAnimationHub");
        Node oldOverlay = _host.BattleScreen?.GetNodeOrNull("BattleAnimationOverlay");
        Check(oldOverlay == null, "Hub does not create a full-screen animation overlay");
        if (hub == null) return;
        Check(hub.Profiles.Count == 7, "Hub receives seven configured profiles");

        EnemyBattle boom = _host.EnemyManager.GetAliveByRole("boom");
        Check(boom != null && boom.CurrentHp == 8, "first wave contains real Boom");
        if (boom == null) return;
        GodotObject playerMachine = hub.GetAnimationMachine(_host.Player);
        GodotObject boomMachine = hub.GetAnimationMachine(boom);
        Check(playerMachine != null && boomMachine != null, "Hub creates player and Boom machines");

        var playerSprite = playerMachine?.Get("sprite").As<AnimatedSprite2D>();
        var boomSprite = boomMachine?.Get("sprite").As<AnimatedSprite2D>();
        var playerAnchor = playerMachine?.Get("anchor").As<Node2D>();
        var boomAnchor = boomMachine?.Get("anchor").As<Node2D>();
        Check(playerSprite != null && playerAnchor != null
            && playerSprite.GetParent() == playerAnchor
            && playerAnchor.GetParent() is Control && playerSprite.Visible,
            "player sprite is visible on its TrackSlot-local anchor");
        Check(boomSprite != null && boomAnchor != null
            && boomSprite.GetParent() == boomAnchor
            && boomAnchor.GetParent() is Control && boomSprite.Visible,
            "Boom sprite is visible on its TrackSlot-local anchor");
        string playerAnimation = playerSprite?.Animation.ToString() ?? "";
        Check(playerAnimation == "idle" || playerAnimation == "move_forward" || playerAnimation == "move_backward",
            "player starts in an available idle/movement sequence");
        Check(boomSprite?.Animation == "idle", "Boom starts in idle animation");

        boom.TakeDamage(1);
        await Frames(1);
        Check(boomSprite?.Animation == "hurt", "health signal plays Boom hurt animation");

        boomMachine.Call("play_action", new StringName("boom_attack"), 1);
        await Frames(1);
        Check(boomSprite?.Animation == "hurt",
            "hurt priority is not interrupted by an action in the same frame");

        await ToSignal(GetTree().CreateTimer(0.55), SceneTreeTimer.SignalName.Timeout);
        boomMachine.Call("play_action", new StringName("boom_attack"), 2);
        await Frames(1);
        Check(boomSprite?.Animation == "attack", "Boom action id resolves to attack sequence");

        boom.TakeDamage(999);
        await Frames(1);
        Check(boomSprite?.Animation == "death", "death signal locks Boom death sequence");
        boomMachine.Call("play_action", new StringName("boom_attack"), 3);
        await Frames(1);
        Check(boomSprite?.Animation == "death", "death cannot be interrupted by later actions");

        // Walk the same production campaign through all remaining waves. This
        // verifies dynamic EnemySpawned binding, multi-enemy profiles, Core's
        // seamless phase switch, and safe glyph fallback for missing artwork.
        _host.BattleManager.EndTurn();
        await WaitFor(() => campaign.CurrentWaveIndex == 1
            && campaign.State == CombatCampaignController.CampaignState.Preparing,
            "campaign did not advance to Rocky + Boom");

        StartPreparedWave(campaign);
        await Frames(3);
        EnemyBattle rocky = _host.EnemyManager.GetAliveByRole("rocky");
        boom = _host.EnemyManager.GetAliveByRole("boom");
        Check(rocky != null && boom != null, "Rocky + Boom wave spawns both actors");
        Check(hub.GetAnimationMachine(rocky) != null && hub.GetAnimationMachine(boom) != null,
            "Hub creates independent Rocky and Boom machines");
        var rockySprite = hub.GetAnimationMachine(rocky)?.Get("sprite").As<AnimatedSprite2D>();
        Check(rockySprite != null && !rockySprite.Visible,
            "Rocky without source frames safely keeps the original glyph");
        KillCurrentWave();
        _host.BattleManager.EndTurn();
        await WaitFor(() => campaign.CurrentWaveIndex == 2
            && campaign.State == CombatCampaignController.CampaignState.Preparing,
            "campaign did not advance to Sharkk");

        StartPreparedWave(campaign);
        await Frames(3);
        EnemyBattle sharkk = _host.EnemyManager.GetAliveByRole("sharkk");
        Check(sharkk != null && hub.GetAnimationMachine(sharkk) != null,
            "trained Sharkk receives its configured animation machine");
        var sharkkSprite = hub.GetAnimationMachine(sharkk)?.Get("sprite").As<AnimatedSprite2D>();
        Check(sharkkSprite != null && !sharkkSprite.Visible,
            "Sharkk without source frames safely keeps the original glyph");
        KillCurrentWave();
        _host.BattleManager.EndTurn();
        await WaitFor(() => campaign.CurrentWaveIndex == 3
            && campaign.State == CombatCampaignController.CampaignState.Preparing,
            "campaign did not advance to Core hands");

        StartPreparedWave(campaign);
        await Frames(3);
        EnemyBattle trueHand = _host.EnemyManager.GetAliveByRole("true_hand");
        EnemyBattle falseHand = _host.EnemyManager.GetAliveByRole("false_hand");
        Check(trueHand != null && falseHand != null, "Core phase one spawns both hands");
        var trueMachine = hub.GetAnimationMachine(trueHand);
        var falseMachine = hub.GetAnimationMachine(falseHand);
        Check(trueMachine != null && falseMachine != null,
            "Core hands receive separate role profiles");
        Check(trueMachine?.Get("sprite").As<AnimatedSprite2D>()?.Visible == true
            && falseMachine?.Get("sprite").As<AnimatedSprite2D>()?.Visible == true,
            "both Core hand frame sets are visible");
        KillCurrentWave();
        _host.BattleManager.EndTurn();
        await WaitFor(() => campaign.CurrentWaveIndex == 4
            && campaign.State == CombatCampaignController.CampaignState.Active
            && _host.EnemyManager.GetAliveByRole("body") != null,
            "Core body phase did not auto-start");

        EnemyBattle body = _host.EnemyManager.GetAliveByRole("body");
        Check(body != null && hub.GetAnimationMachine(body) != null,
            "Core body receives its configured animation machine");
        var bodySprite = hub.GetAnimationMachine(body)?.Get("sprite").As<AnimatedSprite2D>();
        Check(bodySprite != null && !bodySprite.Visible,
            "Core body without source frames safely keeps the original glyph");
    }

    private void StartPreparedWave(CombatCampaignController campaign)
    {
        Check(campaign.State == CombatCampaignController.CampaignState.Preparing,
            $"wave {campaign.CurrentWaveIndex} must be preparing before start");
        _host.BattleManager.StartBattle();
        campaign.NotifyWaveStarted();
    }

    private void KillCurrentWave()
    {
        foreach (EnemyBattle enemy in _host.EnemyManager.GetAliveEnemies())
            _host.BattleManager.DamageEnemy(9999, enemy);
    }

    private async Task WaitFor(Func<bool> predicate, string failure, double timeoutSeconds = 5.0)
    {
        double elapsed = 0.0;
        while (!predicate() && elapsed < timeoutSeconds)
        {
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
            elapsed += GetProcessDeltaTime();
        }
        Check(predicate(), failure);
    }

    private async Task Frames(int count)
    {
        for (int i = 0; i < count; i++)
            await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    }

    private void Check(bool condition, string message)
    {
        _checks++;
        if (!condition) _failures.Add(message);
    }
}
