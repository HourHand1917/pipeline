using Godot;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

/// <summary>
/// Opens every standalone BattleRules preset built from the copied production
/// BattleScene. It verifies real TrackSlot mouse movement, local animation
/// anchors, a complete enemy turn, and clean battle completion.
/// </summary>
public partial class AnimationBattleScenesSmoke : Node
{
    private readonly List<string> _failures = new();
    private int _checks;

    private static readonly (string path, string[] roles)[] Cases =
    {
        ("res://anime/scenes/battle_rule_presets/boom_animation_test.tscn", new[] { "boom" }),
        ("res://anime/scenes/battle_rule_presets/rocky_boom_animation_test.tscn", new[] { "rocky", "boom" }),
        ("res://anime/scenes/battle_rule_presets/sharkk_animation_test.tscn", new[] { "sharkk" }),
        ("res://anime/scenes/battle_rule_presets/core00_hands_animation_test.tscn", new[] { "true_hand", "false_hand" }),
        ("res://anime/scenes/battle_rule_presets/core00_body_animation_test.tscn", new[] { "body" }),
    };

    public override async void _Ready()
    {
        try
        {
            foreach (var testCase in Cases)
                await RunCase(testCase.path, testCase.roles);
        }
        catch (Exception exception)
        {
            _failures.Add($"Unhandled exception: {exception}");
        }

        await Frames(2);
        // Godot.Collections.Array owns a native handle. Force managed wrappers
        // to finalize while the engine is still alive, never after Quit().
        GC.Collect();
        GC.WaitForPendingFinalizers();
        await Frames(1);
        if (_failures.Count == 0)
        {
            GD.Print($"ANIMATION_BATTLE_SCENES_SMOKE_PASS checks={_checks} scenes={Cases.Length} turns={Cases.Length} completed={Cases.Length}");
            GetTree().Quit(0);
            return;
        }

        foreach (string failure in _failures)
            GD.PushError($"ANIMATION_BATTLE_SCENES_SMOKE_FAIL: {failure}");
        GetTree().Quit(1);
    }

    private async Task RunCase(string scenePath, string[] expectedRoles)
    {
        PackedScene packed = GD.Load<PackedScene>(scenePath);
        Check(packed != null, $"{scenePath} loads");
        if (packed == null) return;

        AnimeBattleRuleScene host = packed.Instantiate<AnimeBattleRuleScene>();
        AddChild(host);
        await Frames(6);
        Check(host.BattleManager != null && host.Player != null && host.EnemyManager != null,
            $"{scenePath} initializes the production battle runtime");
        Check(host.GetNodeOrNull("BattleAnimationOverlay") == null
            && host.BattleScreen.GetNodeOrNull("BattleAnimationOverlay") == null,
            $"{scenePath} does not create a full-screen animation overlay");
        if (host.BattleManager == null)
        {
            host.QueueFree();
            await Frames(3);
            return;
        }

        Check(host.BoardManager.runtime_cards.Count > 0,
            $"{scenePath} has a playable fallback or saved weapon build");
        Check(host.BattleManager.CurrentPhase == BattleManager.Phase.PlayerTurn,
            $"{scenePath} starts in PlayerTurn");

        BattleAnimationHub hub = FindDescendant<BattleAnimationHub>(host);
        Check(hub != null && hub.Profiles.Count == 7, $"{scenePath} has the full seven-profile Hub");
        BattleAnimationDisplay display = FindDescendant<BattleAnimationDisplay>(host);
        BattleTrackCamera camera = display?.TrackCamera ?? FindDescendant<BattleTrackCamera>(host);
        Check(display != null && display.Position == Vector2.Zero,
            $"{scenePath} uses the drag-and-drop display at origin");
        Check(camera != null && camera.IsInstalled,
            $"{scenePath} installs the horizontal track camera");
        Check(hub != null && !ContainsGuiNode(hub),
            $"{scenePath} animation Hub adds no Control or CanvasLayer that could intercept clicks");
        Check(TrackContainsOnlyOriginalSlots(host),
            $"{scenePath} keeps the original DistanceTrack child structure");
        Check(hub?.GetAnimationMachine(host.Player) != null,
            $"{scenePath} binds the Rubber player animation machine");
        CheckAnchored(host, hub, host.Player, $"{scenePath} Rubber");
        if (scenePath.Contains("boom_animation_test", StringComparison.Ordinal))
        {
            await CheckInspectorFacingSettings(host, display, hub, scenePath);
            await CheckPlayerAnimationEvents(host, hub, scenePath);
        }

        foreach (string role in expectedRoles)
        {
            EnemyBattle enemy = host.EnemyManager.GetAliveByRole(role);
            Check(enemy != null, $"{scenePath} spawns role {role}");
            Check(enemy != null && hub?.GetAnimationMachine(enemy) != null,
                $"{scenePath} binds role {role} animation machine");
            if (enemy != null)
            {
                CheckAnchored(host, hub, enemy, $"{scenePath} {role}");
                await CheckEnemyFacesPlayer(host, hub, enemy, $"{scenePath} {role}");
            }
        }

        await CheckCameraContract(host, camera, scenePath);

        // Send a real viewport mouse event. This verifies both GUI hit-testing
        // and the complete TrackSlot -> UIManager -> BattleScreen ->
        // TryMoveToCell route; directly emitting GuiInput would miss the bug
        // where a decorative child Control intercepts the click.
        TrackSlot moveTarget = FindAdjacentEmptySlot(host);
        Check(moveTarget != null, $"{scenePath} has an adjacent empty movement cell");
        if (moveTarget != null)
        {
            int energyBefore = host.Player.Energy;
            Vector2 clickPosition = moveTarget.GetGlobalRect().GetCenter();
            Viewport viewport = GetViewport();
            viewport.NotifyMouseEntered();
            viewport.PushInput(new InputEventMouseMotion
            {
                Position = clickPosition,
                GlobalPosition = clickPosition,
            }, true);
            viewport.UpdateMouseCursorState();
            await Frames(2);
            // Verify every decorative child is mouse-transparent. The actual
            // position/energy assertions below prove that the viewport event
            // reached this TrackSlot through GUI hit-testing.
            Check(DecorativeControlsIgnoreMouse(moveTarget),
                $"{scenePath} TrackSlot decorations cannot intercept the mouse");

            viewport.PushInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = true,
                Position = clickPosition,
                GlobalPosition = clickPosition,
            }, true);
            viewport.PushInput(new InputEventMouseButton
            {
                ButtonIndex = MouseButton.Left,
                Pressed = false,
                Position = clickPosition,
                GlobalPosition = clickPosition,
            }, true);
            await Frames(3);
            Check(host.Player.MapPosition == moveTarget.CellNumber,
                $"{scenePath} moves Rubber by clicking the empty TrackSlot");
            Check(host.Player.Energy < energyBefore,
                $"{scenePath} charges the BattleRules movement energy cost");
            await ToSignal(GetTree().CreateTimer(0.25), SceneTreeTimer.SignalName.Timeout);
            CheckAnchored(host, hub, host.Player, $"{scenePath} moved Rubber");
        }

        int roundBefore = host.BattleManager.RoundNumber;
        host.BattleManager.EndTurn();
        await WaitFor(() => host.BattleManager.CurrentPhase == BattleManager.Phase.BattleEnd
            || (host.BattleManager.CurrentPhase == BattleManager.Phase.PlayerTurn
                && host.BattleManager.RoundNumber > roundBefore),
            $"{scenePath} did not finish its enemy turn");
        Check(host.Player.CurrentHp > 0, $"{scenePath} leaves the player alive after the smoke turn");

        // Finish the battle through the real death/end path as well. This
        // exercises death animation locking and cleanup of the last slots.
        Godot.Collections.Array<EnemyBattle> alive = host.EnemyManager.GetAliveEnemies();
        foreach (EnemyBattle enemy in alive)
            host.BattleManager.DamageEnemy(9999, enemy);
        alive.Clear();
        alive = null;
        host.BattleManager.EndTurn();
        await WaitFor(() => host.BattleManager.CurrentPhase == BattleManager.Phase.BattleEnd,
            $"{scenePath} did not reach BattleEnd");
        Check(!host.EnemyManager.HasAliveEnemies(), $"{scenePath} completes with all enemies defeated");

        host.QueueFree();
        await Frames(4);
        host = null;
        GC.Collect();
        GC.WaitForPendingFinalizers();
    }

    private TrackSlot FindAdjacentEmptySlot(AnimeBattleRuleScene host)
    {
        HBoxContainer track = host.BattleScreen?.UIManager?.DistanceTrack;
        if (track == null) return null;
        int playerCell = host.Player.MapPosition;
        foreach (Node child in track.GetChildren())
            if (child is TrackSlot slot
                && Mathf.Abs(slot.CellNumber - playerCell) == 1
                && slot.OccupantRef == null)
                return slot;
        return null;
    }

    private bool TrackContainsOnlyOriginalSlots(AnimeBattleRuleScene host)
    {
        HBoxContainer track = host.BattleScreen?.UIManager?.DistanceTrack;
        if (track == null || track.GetChildCount() == 0) return false;
        foreach (Node child in track.GetChildren())
            if (child is not TrackSlot) return false;
        return true;
    }

    private static bool DecorativeControlsIgnoreMouse(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Control control
                && control.MouseFilter != Control.MouseFilterEnum.Ignore)
                return false;
            if (!DecorativeControlsIgnoreMouse(child)) return false;
        }
        return true;
    }

    private static bool ContainsGuiNode(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is Control or CanvasLayer) return true;
            if (ContainsGuiNode(child)) return true;
        }
        return false;
    }

    private TrackSlot FindActorSlot(AnimeBattleRuleScene host, GodotObject actor)
    {
        HBoxContainer track = host.BattleScreen?.UIManager?.DistanceTrack;
        if (track == null) return null;
        foreach (Node child in track.GetChildren())
            if (child is TrackSlot slot && slot.OccupantRef == actor)
                return slot;
        return null;
    }

    private void CheckAnchored(
        AnimeBattleRuleScene host,
        BattleAnimationHub hub,
        GodotObject actor,
        string label)
    {
        Node machine = hub?.GetAnimationMachine(actor);
        TrackSlot slot = FindActorSlot(host, actor);
        Node2D anchor = machine?.Get("anchor").As<Node2D>();
        AnimatedSprite2D sprite = machine?.Get("sprite").As<AnimatedSprite2D>();
        Control occupant = slot?.GetNodeOrNull<Control>("Vbox/occupant");
        Check(slot != null && anchor != null && occupant != null,
            $"{label} has a TrackSlot-local animation anchor");
        Control visualHost = occupant?.GetParent() as Control;
        Check(anchor != null && visualHost != null && anchor.GetParent() == visualHost,
            $"{label} anchor is parented to the slot-local visual host");
        Check(sprite != null && anchor != null && sprite.GetParent() == anchor,
            $"{label} sprite is isolated below its own anchor");
        Check(sprite != null && sprite.Modulate.A >= 0.99f,
            $"{label} sprite does not inherit the translucent glyph tint");
        if (anchor != null && occupant != null && machine != null)
        {
            Resource profile = machine.Get("profile").As<Resource>();
            Vector2 expected = occupant.Position + new Vector2(occupant.Size.X * 0.5f, occupant.Size.Y);
            expected += profile?.Get("visual_offset").AsVector2() ?? Vector2.Zero;
            Check(anchor.Position.DistanceTo(expected) <= 2.0f,
                $"{label} anchor stays inside the slot's local coordinates");
        }
    }

    private async Task CheckPlayerAnimationEvents(
        AnimeBattleRuleScene host,
        BattleAnimationHub hub,
        string label)
    {
        Node machine = hub?.GetAnimationMachine(host.Player);
        AnimatedSprite2D sprite = machine?.Get("sprite").As<AnimatedSprite2D>();
        Check(sprite != null && sprite.FlipH,
            $"{label} horizontally flips the naturally left-facing Rubber source art");
        if (machine == null || sprite == null) return;

        GodotObject attackRuntime = AddSyntheticRuntimeCard(
            host,
            "res://Resource/card/cards/pea_gun.tres",
            900001);
        if (attackRuntime != null)
        {
            GodotObject data = attackRuntime.Get("data").As<GodotObject>();
            host.EffectResolver.EmitSignal(
                EffectResolver.SignalName.EffectExecuted,
                data.Get("display_name").AsString(),
                "damage",
                1);
            await Frames(2);
            Check(sprite.Animation == "attack",
                $"{label} plays attack when a weapon card resolves");
            Check(sprite.FlipH, $"{label} keeps attack horizontally flipped");
            host.BoardManager.runtime_cards.Remove(attackRuntime);
        }

        GodotObject buffRuntime = AddSyntheticRuntimeCard(
            host,
            "res://Resource/card/cards/armored_shield.tres",
            900002);
        if (buffRuntime != null)
        {
            GodotObject data = buffRuntime.Get("data").As<GodotObject>();
            host.EffectResolver.EmitSignal(
                EffectResolver.SignalName.EffectExecuted,
                data.Get("display_name").AsString(),
                "shield",
                1);
            await Frames(2);
            Check(sprite.Animation == "buff",
                $"{label} plays buff when a non-attack card resolves");
            Check(sprite.FlipH, $"{label} keeps buff horizontally flipped");
            host.BoardManager.runtime_cards.Remove(buffRuntime);
        }

        host.Player.AddShield(2);
        int hpBeforeShieldHit = host.Player.CurrentHp;
        host.Player.TakeDamage(1);
        await Frames(2);
        Check(sprite.Animation == "hurt",
            $"{label} plays hurt when an attack is absorbed by player shield");
        Check(host.Player.CurrentHp == hpBeforeShieldHit,
            $"{label} shield-only hurt test does not require HP loss");
        Check(sprite.FlipH, $"{label} keeps hurt horizontally flipped");

        int hpBefore = host.Player.CurrentHp;
        host.Player.TakeDamage(3);
        await Frames(2);
        Check(sprite.Animation == "hurt" && host.Player.CurrentHp < hpBefore,
            $"{label} plays hurt when player HP decreases");
    }

    private async Task CheckInspectorFacingSettings(
        AnimeBattleRuleScene host,
        BattleAnimationDisplay display,
        BattleAnimationHub hub,
        string label)
    {
        Node playerMachine = hub?.GetAnimationMachine(host.Player);
        AnimatedSprite2D playerSprite = playerMachine?.Get("sprite").As<AnimatedSprite2D>();
        EnemyBattle enemy = host.EnemyManager.GetAliveEnemies().Count > 0
            ? host.EnemyManager.GetAliveEnemies()[0]
            : null;
        Node enemyMachine = enemy == null ? null : hub?.GetAnimationMachine(enemy);
        AnimatedSprite2D enemySprite = enemyMachine?.Get("sprite").As<AnimatedSprite2D>();
        Check(display != null && playerSprite != null && enemySprite != null,
            $"{label} exposes initial-facing settings on the display root");
        if (display == null || playerSprite == null || enemySprite == null)
            return;

        display.PlayerInitialFacing = BattleAnimationHub.FacingDirection.Left;
        display.EnemiesAlwaysFacePlayer = false;
        display.EnemyInitialFacing = BattleAnimationHub.FacingDirection.Right;
        display.ApplyInspectorSettings();
        await Frames(2);
        Check(playerSprite.FlipH == ExpectedFlip(playerMachine, 1),
            $"{label} applies Inspector player initial facing Left");
        Check(enemySprite.FlipH == ExpectedFlip(enemyMachine, 0),
            $"{label} applies Inspector enemy initial facing Right");

        display.PlayerInitialFacing = BattleAnimationHub.FacingDirection.Right;
        display.EnemyInitialFacing = BattleAnimationHub.FacingDirection.Left;
        display.EnemiesAlwaysFacePlayer = true;
        display.ApplyInspectorSettings();
        await Frames(2);
        Check(playerSprite.FlipH == ExpectedFlip(playerMachine, 0),
            $"{label} restores Inspector player initial facing Right");
    }

    private async Task CheckEnemyFacesPlayer(
        AnimeBattleRuleScene host,
        BattleAnimationHub hub,
        EnemyBattle enemy,
        string label)
    {
        Node machine = hub?.GetAnimationMachine(enemy);
        AnimatedSprite2D sprite = machine?.Get("sprite").As<AnimatedSprite2D>();
        HBoxContainer track = host.BattleScreen?.UIManager?.DistanceTrack;
        Check(machine != null && sprite != null && track != null,
            $"{label} has a visual-facing machine");
        if (machine == null || sprite == null || track == null)
            return;

        int originalPlayerPosition = host.Player.MapPosition;
        int actorFacingBefore = enemy.Facing;
        bool checkedLeft = false;
        bool leftFlip = false;
        if (enemy.MapPosition > 1)
        {
            host.Player.SetMapPosition(enemy.MapPosition - 1);
            await Frames(2);
            leftFlip = sprite.FlipH;
            checkedLeft = true;
            Check(leftFlip == ExpectedFlip(machine, 1),
                $"{label} visually faces left when Rubber is on its left");
        }

        if (enemy.MapPosition < track.GetChildCount())
        {
            host.Player.SetMapPosition(enemy.MapPosition + 1);
            await Frames(2);
            bool rightFlip = sprite.FlipH;
            Check(rightFlip == ExpectedFlip(machine, 0),
                $"{label} visually faces right when Rubber is on its right");
            if (checkedLeft)
                Check(rightFlip != leftFlip,
                    $"{label} flips when Rubber crosses to the other side");

            host.Player.SetMapPosition(enemy.MapPosition);
            await Frames(2);
            Check(sprite.FlipH == rightFlip,
                $"{label} preserves its last visual direction in the same cell");
        }

        Check(enemy.Facing == actorFacingBefore,
            $"{label} visual facing never mutates EnemyBattle.Facing");
        host.Player.SetMapPosition(originalPlayerPosition);
        await Frames(2);
    }

    private async Task CheckCameraContract(
        AnimeBattleRuleScene host,
        BattleTrackCamera camera,
        string label)
    {
        HBoxContainer track = host.BattleScreen?.UIManager?.DistanceTrack;
        Control viewport = track?.GetParent() as Control;
        Check(camera != null && camera.IsInstalled && track != null && viewport != null,
            $"{label} camera wraps the original DistanceTrack");
        if (camera == null || track == null || viewport == null)
            return;

        Check(viewport.Name == "BattleTrackCameraViewport"
            && viewport.ClipContents
            && viewport.MouseFilter == Control.MouseFilterEnum.Ignore,
            $"{label} camera viewport clips horizontally without intercepting input");
        Check(Mathf.Abs(track.Position.Y) <= 0.01f,
            $"{label} camera never moves the track vertically");
        Check(camera.ContentWidth + 1.0f >= track.GetCombinedMinimumSize().X,
            $"{label} camera measures the complete track width");

        int originalPosition = host.Player.MapPosition;
        int cellCount = track.GetChildCount();
        Control staticUi = host.BattleScreen.GetNodeOrNull<Control>("stageWrapper");
        Vector2 staticOrigin = staticUi?.GetGlobalTransformWithCanvas().Origin ?? Vector2.Zero;

        if (camera.ContentWidth <= camera.ViewportWidth + 1.0f)
        {
            host.Player.SetMapPosition(1);
            await Frames(2);
            camera.SnapToPlayer();
            float leftOffset = camera.CurrentOffsetX;
            host.Player.SetMapPosition(cellCount);
            await Frames(2);
            camera.SnapToPlayer();
            Check(Mathf.Abs(camera.CurrentOffsetX - leftOffset) <= 1.0f,
                $"{label} keeps a short map centered instead of scrolling");

            TrackSlot first = FindSlot(track, 1);
            TrackSlot last = FindSlot(track, cellCount);
            if (first != null && last != null)
            {
                float mapCenter = (first.GetGlobalRect().Position.X + last.GetGlobalRect().End.X) * 0.5f;
                Check(Mathf.Abs(mapCenter - viewport.GetGlobalRect().GetCenter().X) <= 2.0f,
                    $"{label} centers the complete short map in the camera");
            }
        }
        else
        {
            host.Player.SetMapPosition(1);
            await Frames(2);
            camera.SnapToPlayer();
            TrackSlot first = FindSlot(track, 1);
            Check(Mathf.Abs(camera.CurrentOffsetX) <= 1.0f,
                $"{label} stops at the left map edge");
            if (first != null)
                Check(Mathf.Abs(first.GetGlobalRect().Position.X - viewport.GetGlobalRect().Position.X) <= 2.0f,
                    $"{label} aligns the first cell with the left camera edge");

            int middleCell = (cellCount + 1) / 2;
            host.Player.SetMapPosition(middleCell);
            await Frames(2);
            camera.SnapToPlayer();
            TrackSlot middle = FindSlot(track, middleCell);
            if (middle != null)
                Check(Mathf.Abs(middle.GetGlobalRect().GetCenter().X
                    - viewport.GetGlobalRect().GetCenter().X) <= 2.0f,
                    $"{label} keeps Rubber in the horizontal camera center");

            host.Player.SetMapPosition(cellCount);
            await Frames(2);
            camera.SnapToPlayer();
            TrackSlot last = FindSlot(track, cellCount);
            float minimumOffset = camera.ViewportWidth - camera.ContentWidth;
            Check(Mathf.Abs(camera.CurrentOffsetX - minimumOffset) <= 1.0f,
                $"{label} stops at the right map edge");
            if (last != null)
                Check(Mathf.Abs(last.GetGlobalRect().End.X - viewport.GetGlobalRect().End.X) <= 2.0f,
                    $"{label} aligns the last cell with the right camera edge");
        }

        if (staticUi != null)
            Check(staticUi.GetGlobalTransformWithCanvas().Origin.DistanceTo(staticOrigin) <= 0.5f,
                $"{label} leaves non-track battle UI stationary");
        Check(Mathf.Abs(track.Position.Y) <= 0.01f,
            $"{label} remains horizontal after following Rubber");
        Check(ProjectSettings.GetSetting("display/window/size/viewport_width").AsInt32() == 1440
            && ProjectSettings.GetSetting("display/window/size/viewport_height").AsInt32() == 1080,
            $"{label} preserves the 1440x1080 4:3 project viewport");

        host.Player.SetMapPosition(originalPosition);
        await Frames(2);
        camera.SnapToPlayer();
    }

    private static TrackSlot FindSlot(HBoxContainer track, int cellNumber)
    {
        if (track == null) return null;
        foreach (Node child in track.GetChildren())
            if (child is TrackSlot slot && slot.CellNumber == cellNumber)
                return slot;
        return null;
    }

    private static bool ExpectedFlip(Node machine, int visualFacing)
    {
        Resource profile = machine?.Get("profile").As<Resource>();
        bool sourceFacesRight = profile?.Get("source_faces_right").AsBool() ?? true;
        return sourceFacesRight ? visualFacing == 1 : visualFacing != 1;
    }

    private static GodotObject AddSyntheticRuntimeCard(
        AnimeBattleRuleScene host,
        string cardPath,
        int instanceId)
    {
        GodotObject data = GD.Load<GodotObject>(cardPath);
        GDScript runtimeScript = GD.Load<GDScript>("res://Script/GD/refcounted/card_runtime.gd");
        if (data == null || runtimeScript == null) return null;
        var cells = new Godot.Collections.Array<Vector2I>();
        GodotObject runtime = runtimeScript.New(instanceId, data, Vector2I.Zero, 0, cells).As<GodotObject>();
        if (runtime != null) host.BoardManager.runtime_cards.Add(runtime);
        return runtime;
    }

    private static T FindDescendant<T>(Node root) where T : Node
    {
        if (root == null) return null;
        if (root is T match) return match;
        foreach (Node child in root.GetChildren())
        {
            T found = FindDescendant<T>(child);
            if (found != null) return found;
        }
        return null;
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
