using Godot;
using Godot.Collections;
using System;
using System.Collections.Generic;

/// <summary>
/// Read-only display observer. Drop the packaged scene anywhere below a battle
/// root and it discovers the runtime, reads combatant positions/events, then
/// displays the matching animation. It never mutates HP, AI, cards, movement,
/// input, viewport settings or turn state.
/// </summary>
[GlobalClass]
public partial class BattleAnimationHub : Node
{
    public enum FacingDirection
    {
        Right = 0,
        Left = 1,
    }

    [ExportGroup("Auto Bind")]
    [Export] public NodePath BattleManagerPath { get; set; } = "../battlemanager";
    [Export] public NodePath EffectResolverPath { get; set; } = "../effectresolver";
    [Export] public NodePath BattleScreenPath { get; set; } = "../battlescreen";

    [ExportGroup("Profiles")]
    [Export] public Array<Resource> Profiles { get; set; } = new();
    [Export] public bool VerboseLogging { get; set; }

    [ExportGroup("Visual Facing Only")]
    [Export] public bool EnemiesAlwaysFacePlayer { get; set; } = true;
    [Export] public bool ForceEnemyFacing { get; set; } = false;
    [Export] public FacingDirection ForcedEnemyFacing { get; set; } = FacingDirection.Left;
    [Export] public FacingDirection PlayerInitialFacing { get; set; } = FacingDirection.Right;
    [Export] public FacingDirection EnemyInitialFacing { get; set; } = FacingDirection.Left;

    [ExportGroup("Visual Scale Only")]
    [Export(PropertyHint.Range, "0.1,4,0.01")]
    public float PlayerScaleMultiplier { get; set; } = 1.0f;
    [Export(PropertyHint.Range, "0.1,4,0.01")]
    public float EnemyScaleMultiplier { get; set; } = 1.0f;

    private readonly System.Collections.Generic.Dictionary<ulong, Node> _machines = new();
    private readonly HashSet<ulong> _entryPlayed = new();
    private readonly System.Collections.Generic.Dictionary<ulong, int> _initialPositions = new();
    private readonly System.Collections.Generic.Dictionary<ulong, int> _visualFacings = new();
    private readonly HashSet<ulong> _actorsThatMoved = new();
    private BattleManager _battle;
    private EffectResolver _resolver;
    private BattleScreen _screen;
    private EnemyManager _enemies;
    private PlayerBattle _player;
    private HBoxContainer _distanceTrack;
    private ulong _lastPlayerEffectFrame = ulong.MaxValue;
    private string _lastPlayerEffectSource = "";
    private int _playerActionSerial;
    private EffectResolver _boundResolver;
    private EnemyManager _boundEnemies;

    public override void _Process(double delta)
    {
        DiscoverRuntime();
        DiscoverCombatants();
        RefreshSlots();
    }

    public Node GetAnimationMachine(GodotObject actor)
    {
        if (actor is not Node node) return null;
        return EnsureMachine(node);
    }

    private void DiscoverRuntime()
    {
        _battle ??= GetNodeOrNull<BattleManager>(BattleManagerPath)
            ?? FindInAncestorScopes<BattleManager>(this);

        if (_battle != null)
        {
            _player = _battle.Player ?? _player;
            _enemies = _battle.EnemyManager ?? _enemies;
            _screen = _battle.BattleScreenRef ?? _screen;
            _resolver = _battle.EffectResolver ?? _resolver;
        }

        _screen ??= GetNodeOrNull<BattleScreen>(BattleScreenPath)
            ?? FindInAncestorScopes<BattleScreen>(this);
        _resolver ??= GetNodeOrNull<EffectResolver>(EffectResolverPath)
            ?? FindInAncestorScopes<EffectResolver>(this);

        if (_resolver != null && _boundResolver != _resolver)
        {
            _resolver.EffectExecuted += OnEffectExecuted;
            _boundResolver = _resolver;
        }
        if (_enemies != null && _boundEnemies != _enemies)
        {
            _enemies.EnemySpawned += OnEnemySpawned;
            _boundEnemies = _enemies;
        }

        if (_screen?.UIManager?.DistanceTrack != null)
            _distanceTrack = _screen.UIManager.DistanceTrack;
    }

    private void DiscoverCombatants()
    {
        if (IsInstanceValid(_player) && !_player.IsQueuedForDeletion()) EnsureMachine(_player);
        if (_enemies != null)
        {
            foreach (EnemyBattle enemy in _enemies.Enemies)
                if (IsInstanceValid(enemy) && !enemy.IsQueuedForDeletion()) EnsureMachine(enemy);
        }

        foreach (ulong key in new List<ulong>(_machines.Keys))
        {
            Node machine = _machines[key];
            GodotObject actor = machine?.Get("actor").AsGodotObject();
            if (IsInstanceValid(machine)
                && actor is Node actorNode
                && IsInstanceValid(actorNode)
                && !actorNode.IsQueuedForDeletion())
                continue;
            _machines.Remove(key);
            _entryPlayed.Remove(key);
            _initialPositions.Remove(key);
            _visualFacings.Remove(key);
            _actorsThatMoved.Remove(key);
            if (IsInstanceValid(machine)) machine.QueueFree();
        }
    }

    private Node EnsureMachine(Node actor)
    {
        if (!IsInstanceValid(actor) || actor.IsQueuedForDeletion()) return null;
        ulong key = actor.GetInstanceId();
        if (_machines.TryGetValue(key, out Node existing) && IsInstanceValid(existing))
            return existing;

        Resource profile = FindProfile(actor);
        if (profile == null) return null;
        var script = GD.Load<GDScript>("res://anime/scripts/battle_animation_machine.gd");
        Node machine = script?.New().As<Node>();
        if (machine == null) return null;

        AddChild(machine);
        _initialPositions[key] = ReadMapPosition(actor);
        machine.Call("bind", actor, profile, _battle);
        ApplyMachinePresentation(machine, actor);
        ConnectActorSignals(actor, machine);
        SyncMachineState(machine, actor);
        _machines[key] = machine;
        if (VerboseLogging)
            GD.Print($"[BattleAnimationHub] {actor.Name} -> {profile.Get("profile_id")}");
        return machine;
    }

    private void ConnectActorSignals(Node actor, Node machine)
    {
        ulong actorId = actor.GetInstanceId();
        if (actor is PlayerBattle player)
        {
            player.HealthChanged += (current, maximum) =>
            {
                if (IsInstanceValid(machine)) machine.Call("notify_health_changed", current, maximum);
            };
            player.ShieldChanged += current =>
            {
                if (IsInstanceValid(machine)) machine.Call("notify_shield_changed", current);
            };
            player.PositionChanged += position =>
            {
                MarkActorMoved(actorId, position);
                if (IsInstanceValid(machine)) machine.Call("notify_position_changed", position);
            };
            player.Died += () =>
            {
                if (IsInstanceValid(machine)) machine.Call("notify_died");
            };
        }
        else if (actor is EnemyBattle enemy)
        {
            enemy.HealthChanged += (current, maximum) =>
            {
                if (IsInstanceValid(machine)) machine.Call("notify_health_changed", current, maximum);
            };
            enemy.ShieldChanged += current =>
            {
                if (IsInstanceValid(machine)) machine.Call("notify_shield_changed", current);
            };
            enemy.PositionChanged += position =>
            {
                MarkActorMoved(actorId, position);
                if (IsInstanceValid(machine)) machine.Call("notify_position_changed", position);
            };
            enemy.Died += () =>
            {
                if (IsInstanceValid(machine)) machine.Call("notify_died");
            };
            enemy.IntentChanged += action =>
            {
                if (IsInstanceValid(machine)) machine.Call("notify_intent_changed", action);
            };
        }
    }

    private Resource FindProfile(Node actor)
    {
        foreach (Resource profile in Profiles)
        {
            if (profile == null) continue;
            bool isPlayer = profile.Get("is_player").AsBool();
            if (isPlayer && actor == _player) return profile;
            if (isPlayer || actor is not EnemyBattle enemy) continue;
            string[] ids = profile.Get("enemy_ids").AsStringArray();
            string[] roles = profile.Get("roles").AsStringArray();
            if (System.Array.IndexOf(ids, enemy.EnemyId) >= 0
                || System.Array.IndexOf(roles, enemy.Role.ToString()) >= 0)
                return profile;
        }
        return null;
    }

    private void RefreshSlots()
    {
        if (_distanceTrack == null) return;
        foreach (Node machine in _machines.Values)
        {
            if (!IsInstanceValid(machine)) continue;
            Node actor = machine.Get("actor").As<Node>();
            if (!IsInstanceValid(actor) || actor.IsQueuedForDeletion()) continue;
            SyncMachineState(machine, actor);
            TrackSlot slot = FindSlot(actor);
            bool deathStarted = machine.Get("death_started").AsBool();
            if (slot != null)
            {
                // The visual is parented directly below this TrackSlot's
                // occupant anchor.  No full-screen overlay is created, so the
                // original Control layout and GUI hit-test remain untouched.
                machine.Call("attach_to_slot", slot, true);
                ulong key = actor.GetInstanceId();
                if (!_entryPlayed.Contains(key))
                {
                    machine.Call("play_entry");
                    _entryPlayed.Add(key);
                }
            }
            else if (!deathStarted)
            {
                machine.Call("detach_from_slot");
            }
        }
    }

    private TrackSlot FindSlot(Node actor)
    {
        if (_distanceTrack == null) return null;
        foreach (Node child in _distanceTrack.GetChildren())
            if (child is TrackSlot slot && slot.OccupantRef == actor)
                return slot;

        int mapPosition = actor switch
        {
            PlayerBattle p => p.MapPosition,
            EnemyBattle e => e.MapPosition,
            _ => -1,
        };
        // UIManager may choose one OccupantRef when two actors temporarily
        // share a logical cell. MapPosition remains authoritative for visual
        // placement, so both animation machines must still find that cell.
        foreach (Node child in _distanceTrack.GetChildren())
            if (child is TrackSlot slot && slot.CellNumber == mapPosition)
                return slot;
        return null;
    }

    private void SyncMachineState(Node machine, Node actor)
    {
        int mapPosition = 0;
        int facing = 0;
        int hp = 0;
        int shield = 0;
        if (actor is PlayerBattle player)
        {
            mapPosition = player.MapPosition;
            facing = _actorsThatMoved.Contains(actor.GetInstanceId())
                ? player.Facing
                : (int)PlayerInitialFacing;
            hp = player.CurrentHp;
            shield = player.Shield;
        }
        else if (actor is EnemyBattle enemy)
        {
            mapPosition = enemy.MapPosition;
            ulong actorId = actor.GetInstanceId();
            if (ForceEnemyFacing)
            {
                facing = (int)ForcedEnemyFacing;
            }
            else if (EnemiesAlwaysFacePlayer
                && IsInstanceValid(_player)
                && _player.MapPosition != enemy.MapPosition)
            {
                facing = _player.MapPosition > enemy.MapPosition
                    ? (int)FacingDirection.Right
                    : (int)FacingDirection.Left;
            }
            else if (EnemiesAlwaysFacePlayer
                && IsInstanceValid(_player)
                && _player.MapPosition == enemy.MapPosition
                && _visualFacings.TryGetValue(actorId, out int previousFacing))
            {
                // If both combatants temporarily share one cell there is no
                // meaningful left/right vector. Preserve the previous visual
                // direction instead of flickering between sides.
                facing = previousFacing;
            }
            else
            {
                facing = _actorsThatMoved.Contains(actorId)
                    ? enemy.Facing
                    : (int)EnemyInitialFacing;
            }
            hp = enemy.CurrentHp;
            shield = enemy.Shield;
        }
        _visualFacings[actor.GetInstanceId()] = facing;
        machine.Call("sync_runtime_state", mapPosition, facing, hp, shield);
    }

    /// <summary>
    /// Re-applies display-only Inspector overrides to existing machines. This
    /// method never writes scale or facing into PlayerBattle/EnemyBattle.
    /// </summary>
    public void ApplyPresentationOverrides()
    {
        foreach (Node machine in _machines.Values)
        {
            if (!IsInstanceValid(machine)) continue;
            Node actor = machine.Get("actor").As<Node>();
            if (!IsInstanceValid(actor)) continue;
            ApplyMachinePresentation(machine, actor);
            SyncMachineState(machine, actor);
        }
    }

    private void ApplyMachinePresentation(Node machine, Node actor)
    {
        float multiplier = actor is PlayerBattle
            ? PlayerScaleMultiplier
            : EnemyScaleMultiplier;
        machine.Call("set_visual_scale_multiplier", Mathf.Max(0.01f, multiplier));
    }

    private void MarkActorMoved(ulong actorId, int newPosition)
    {
        if (!_initialPositions.TryGetValue(actorId, out int initialPosition))
        {
            _initialPositions[actorId] = newPosition;
            return;
        }
        if (newPosition != initialPosition)
            _actorsThatMoved.Add(actorId);
    }

    private static int ReadMapPosition(Node actor) => actor switch
    {
        PlayerBattle player => player.MapPosition,
        EnemyBattle enemy => enemy.MapPosition,
        _ => 0,
    };

    private void OnEnemySpawned(EnemyBattle enemy) => EnsureMachine(enemy);

    private void OnEffectExecuted(string sourceName, string effectType, int value)
    {
        if (_battle == null) return;
        if (_battle.CurrentPhase != BattleManager.Phase.PlayerTurn || _player == null) return;

        ulong frame = Engine.GetProcessFrames();
        // One card can emit move + damage + status effects in the same frame.
        // Treat them as one play, while allowing the same card to be used
        // again later in the same round.
        if (frame == _lastPlayerEffectFrame
            && string.Equals(sourceName, _lastPlayerEffectSource, StringComparison.Ordinal))
            return;
        _lastPlayerEffectFrame = frame;
        _lastPlayerEffectSource = sourceName;
        Node playerMachine = EnsureMachine(_player);
        if (playerMachine == null) return;

        // EffectResolver exposes the configured card display name. Resolve it
        // back to the runtime CardData id so all fourteen card mappings in the
        // player profile are actually used. Multi-hit cards emit several
        // effects in one frame; the frame/key guard above prevents restarts.
        StringName cardId = ResolvePlayerCardId(sourceName);
        if (cardId.IsEmpty) return; // Ignore items and unrelated effects.
        _playerActionSerial++;
        playerMachine.Call("play_action", cardId, _playerActionSerial);
    }

    private StringName ResolvePlayerCardId(string sourceName)
    {
        if (string.IsNullOrEmpty(sourceName) || _battle?.BoardManager == null)
            return new StringName();

        foreach (GodotObject runtime in _battle.BoardManager.runtime_cards)
        {
            GodotObject data = runtime?.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
            if (data == null) continue;
            if (!string.Equals(
                    data.Get(GDScriptKeys.CardData.DisplayName).AsString(),
                    sourceName,
                    StringComparison.Ordinal))
                continue;
            return new StringName(data.Get(GDScriptKeys.CardData.Id).AsString());
        }
        return new StringName();
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

    private static T FindInAncestorScopes<T>(Node start) where T : Node
    {
        Node scope = start;
        while (scope != null)
        {
            T found = FindDescendant<T>(scope);
            if (found != null) return found;
            scope = scope.GetParent();
        }
        return null;
    }
}
