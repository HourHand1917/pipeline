using Godot;
using Godot.Collections;
using System.Linq;

[GlobalClass]
public partial class BattleManager : Node
{
    [Signal] public delegate void BattleStateChangedEventHandler();
    [Signal] public delegate void PhaseChangedEventHandler(int newPhase);
    [Signal] public delegate void LogMessageEventHandler(string text);
    [Signal] public delegate void BattleEndedEventHandler(bool playerWon);

    public enum Phase { Build, PlayerTurn, EnemyTurn, BattleEnd }
    public enum MoveAction { Backward = -1, Forward = 1 }
    public const int FacingPositive = 0;
    public const int FacingNegative = 1;

    public Resource GameRules { get; set; }

    public PlayerBattle Player { get; set; }
    public EnemyManager EnemyManager { get; set; }
    public BoardManager BoardManager { get; set; }
    public EffectResolver EffectResolver { get; set; }
    public BattleScreen BattleScreenRef { get; set; }

    public Phase CurrentPhase { get; private set; } = Phase.Build;
    public int RoundNumber { get; private set; } = 0;
    public int Distance => CalcDistance();

    private GodotObject rules;
    private Dictionary<int, bool> usedCardIds = new();
    private bool turnTransitionLocked;
    private bool battleEndEmitted;
    private EnemyBattle _targetEnemy;
    private bool _trueDeathLoopActive;
    private bool _cancelLockedIntent;
    private EnemyBattle _smokeCancelledEnemy;
    private int _lastPlayedCardInstanceId = -1;
    private int _lastPlayerDamage;
    private StringName _lastPlayerActionType = new();
    public EnemyBattle ActingEnemy { get; private set; }

    public int PlayerHp => Player?.CurrentHp ?? 0;
    public int PlayerMaxHp => Player?.MaxHp ?? 0;
    public int PlayerEnergy => Player?.Energy ?? 0;
    public int PlayerShield => Player?.Shield ?? 0;
    public int PlayerMapPosition => Player?.MapPosition ?? 0;
    public int EnemyHp => EnemyManager?.GetPrimaryEnemy()?.CurrentHp ?? 0;
    public int EnemyMaxHp => EnemyManager?.GetPrimaryEnemy()?.MaxHp ?? 0;
    public int EnemyShield => EnemyManager?.GetPrimaryEnemy()?.Shield ?? 0;
    public int EnemyMapPosition => EnemyManager?.GetPrimaryEnemy()?.MapPosition ?? 0;
    public GodotObject GetRules() => rules;

    public static bool IsInRange(int selfPos, int targetPos, int facing, int minRange, int maxRange)
    {
        if (minRange <= 0 && maxRange <= 0) return true;
        if (facing == FacingPositive) { int dist = targetPos - selfPos; return dist >= minRange && dist <= maxRange; }
        else { int dist = selfPos - targetPos; return dist >= minRange && dist <= maxRange; }
    }

    public void Setup() { }

    public void StartBattle() => StartBattle(false, false);

    public void StartBattle(bool preservePlayerState, bool preserveBoardRuntimeState)
    {
        if (GameRules == null) { Log("战斗无法开始：请先配置 GameRules 资源。"); return; }
        rules = GameRules as GodotObject;
        if (rules == null) { Log("战斗无法开始：规则资源无效。"); return; }
        if (Player == null || EnemyManager == null || BoardManager == null) { Log("战斗无法开始：实例未注入。"); return; }

        turnTransitionLocked = false;
        battleEndEmitted = false;
        usedCardIds.Clear();
        RoundNumber = 1;

        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        var playerData = rules.Get(GDScriptKeys.GameRules.PlayerData).As<GodotObject>();
        Player.LoadForNextWave(playerData, rules, preservePlayerState);
        Player.ClearTurnModifiers();

        // 能力成长：入场力量 buff（仅首次进入时施加一次）
        if (!preservePlayerState)
        {
            int strengthStacks = GrowthManager.Instance?.GetStrengthStacks() ?? 0;
            if (strengthStacks > 0)
            {
                var strength = GD.Load<Resource>("res://features/buff_system/resources/buffs/strength.tres");
                if (strength != null)
                {
                    Player.GetStats()?.Call(GDScriptKeys.Stats.AddBuff, strength, strengthStacks);
                    Player.ApplyBuff(strength, strengthStacks);
                }
            }
        }

        int playerStartCell = battleMap.Get(GDScriptKeys.BattleMap.PlayerStartCell).AsInt32();
        int requestedPlayerCell = preservePlayerState && Player.MapPosition > 0
            ? Player.MapPosition
            : playerStartCell;
        int cellCount = battleMap.Get(GDScriptKeys.BattleMap.CellCount).AsInt32();
        requestedPlayerCell = Mathf.Clamp(requestedPlayerCell, 1, cellCount);

        EnemyManager.SpawnWave(rules, requestedPlayerCell);
        Player.SetMapPosition(requestedPlayerCell);
        if (!preservePlayerState) Player.Facing = FacingPositive;
        _trueDeathLoopActive = false;
        _cancelLockedIntent = false;
        _smokeCancelledEnemy = null;
        _lastPlayerDamage = 0;
        _lastPlayerActionType = new StringName();
        
        foreach (var enemy in EnemyManager.Enemies)
            enemy?.UpdateFacing(Player.MapPosition);
        if (preservePlayerState) UpdateAllFacings();

        if (!preserveBoardRuntimeState)
            BoardManager.ResetAllCardStates();
        SetPhase(Phase.PlayerTurn);
        Player.ResetEnergy();
        // A preserved boss phase is a real new player turn. Resolve start
        // triggers after energy reset so False/medkit/holographic keep their
        // documented timing across the Core hands -> body transition.
        if (preservePlayerState)
            EffectResolver?.TickActorBuffs(Player, this, true);
        Player.EmitHealthChanged();

        PlanEnemyTurns();

        Log($"战斗开始：{EnemyManager.Enemies.Count}名敌人，距离最近敌人{Distance}格；获得{Player.Energy}点能量。");
        EmitSignal(SignalName.BattleStateChanged);
    }

    public void ReturnToBuild()
    {
        turnTransitionLocked = false;
        usedCardIds.Clear();
        EffectResolver?.ClearActorBuffs(Player?.GetStats());
        SetPhase(Phase.Build);
        EmitSignal(SignalName.BattleStateChanged);
    }

    private void StartPlayerTurn()
    {
        bool preserve = rules.Get(GDScriptKeys.GameRules.PreservePartialCharge).AsBool();
        if (!preserve) BoardManager.ClearAllLights();
        BoardManager.TickCooldowns();
        BoardManager.TickCellBuffsAtStart();
        Player.ResetEnergy();
        EffectResolver?.TickActorBuffs(Player, this, true);
        Player.ClearTurnModifiers(); usedCardIds.Clear(); SetPhase(Phase.PlayerTurn);
        _lastPlayerDamage = 0;
        _lastPlayerActionType = new StringName();
        foreach (var enemy in EnemyManager.Enemies) enemy?.ResetTurnDamage();
        PlanEnemyTurns();
        Log($"第{RoundNumber}回合开始：能量重置为{Player.Energy}，冷却-1。");
    }

    public void TryLightCell(Vector2I position)
    {
        if (!CanAcceptPlayerAction()) { Log("当前阶段不能点亮格子。"); return; }
        var cell = BoardManager.GetCell(position);
        if (cell == null) return;
        var runtime = BoardManager.GetCardByCell(position);
        if (runtime == null) { Log("这个格子中没有物品。"); return; }
        if (cell.Get(GDScriptKeys.CellRuntime.IsLit).AsBool()) { Log("这个格子已经点亮。"); return; }
        int cooldown = runtime.Get(GDScriptKeys.CardRuntime.CooldownRemaining).AsInt32();
        if (cooldown > 0) { var cdData = runtime.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>(); Log($"{cdData.Get(GDScriptKeys.CardData.DisplayName)}仍在冷却：{cooldown}回合。"); return; }
        var stats = cell.Get(GDScriptKeys.CellRuntime.Stats).As<GodotObject>();
        if (EffectResolver != null && !EffectResolver.CanLightCell(stats, cell))
        { Log("该格子已失效，无法点亮。"); return; }
        int chargeCost = rules.Get(GDScriptKeys.GameRules.ChargeEnergyCost).AsInt32();
        int dustExtra = EffectResolver?.GetLightExtraCost(stats, cell) ?? 0;
        int totalCost = chargeCost + dustExtra;
        if (!Player.SpendEnergy(totalCost)) { Log($"能量不足，点亮需要{totalCost}点能量。"); return; }
        var cardData = runtime.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
        if (cardData.Get(GDScriptKeys.CardData.OncePerTurn).AsBool() && usedCardIds.ContainsKey(runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32()))
        { Log($"{cardData.Get(GDScriptKeys.CardData.DisplayName)}本回合已经发动过。"); return; }
        BoardManager.SetCellLit(position, true);
        EffectResolver?.NotifyCellLit(stats, cell);
        PlanEnemyTurns();
        EmitSignal(SignalName.BattleStateChanged);
    }

    public void TryPlayCard(int instanceId)
    {
        if (!CanAcceptPlayerAction()) { Log("当前阶段不能发动装备。"); return; }
        var runtime = BoardManager.GetRuntimeCard(instanceId);
        if (runtime == null) return;
        var data = runtime.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
        if (data.Get(GDScriptKeys.CardData.OncePerTurn).AsBool() && usedCardIds.ContainsKey(instanceId))
        { Log($"{data.Get(GDScriptKeys.CardData.DisplayName)}本回合已经发动过。"); return; }
        if (!BoardManager.CheckCardReady(runtime)) { Log($"{data.Get(GDScriptKeys.CardData.DisplayName)}尚未全部点亮。"); return; }
        AutoFaceEnemyTarget(data);
        if (data.Call(GDScriptKeys.CardData.HasDamageEffect).AsBool())
        {
            int minRange = data.Get(GDScriptKeys.CardData.MinRange).AsInt32();
            int maxRange = data.Get(GDScriptKeys.CardData.MaxRange).AsInt32();

            var lockedEnemy = GetLockedEnemyFromTV();
            _targetEnemy = lockedEnemy ?? EnemyManager.GetPrimaryEnemy();
            int enemyPos = _targetEnemy?.MapPosition ?? 0;

            if (!IsInRange(Player.MapPosition, enemyPos, Player.Facing, minRange, maxRange))
            { Log($"{data.Get(GDScriptKeys.CardData.DisplayName)}射程不足。"); return; }
        }
        turnTransitionLocked = true;
        if (!EffectResolver.ExecuteCard(runtime, this)) { turnTransitionLocked = false; Log($"{data.Get(GDScriptKeys.CardData.DisplayName)}效果执行失败。"); return; }
        EffectResolver.ResolvePendingBuffApplications(this);
        _lastPlayedCardInstanceId = instanceId;
        RecordPlayerCardMetadata(data);
        if (data.Get(GDScriptKeys.CardData.OncePerTurn).AsBool()) usedCardIds[instanceId] = true;
        int cooldownTurns = data.Get(GDScriptKeys.CardData.CooldownTurns).AsInt32();
        runtime.Set(GDScriptKeys.CardRuntime.CooldownRemaining, cooldownTurns);
        BoardManager.ClearCardLights(runtime);
        Log($"{data.Get(GDScriptKeys.CardData.DisplayName)}发动，进入{cooldownTurns}回合冷却。");
        PlanEnemyTurns();
        if (!CheckBattleEnd()) turnTransitionLocked = false;
        EmitSignal(SignalName.BattleStateChanged);
    }

    private void AutoFaceEnemyTarget(GodotObject cardData)
    {
        if (!cardData.Call(GDScriptKeys.CardData.HasDamageEffect).AsBool()) return;
        var lockedEnemy = GetLockedEnemyFromTV();
        if (lockedEnemy == null) return;
        int diff = lockedEnemy.MapPosition - Player.MapPosition;
        int newFacing = diff > 0 ? FacingPositive : FacingNegative;
        if (Player.Facing != newFacing)
        {
            Player.Facing = newFacing;
            foreach (var e in EnemyManager.Enemies) e?.UpdateFacing(Player.MapPosition);
        }
    }

    private EnemyBattle GetLockedEnemyFromTV() => BattleScreenRef?.UIManager?.PlayerTV?.GetTrackedEnemy();
    public EnemyBattle GetCurrentTargetEnemy() => _targetEnemy ?? EnemyManager?.GetPrimaryEnemy();

    public bool CanPlayerMove(int action)
    {
        if (CurrentPhase != Phase.PlayerTurn || turnTransitionLocked || rules == null) return false;
        if (action != (int)MoveAction.Backward && action != (int)MoveAction.Forward) return false;
        int moveCost = rules.Get(GDScriptKeys.GameRules.MoveEnergyCost).AsInt32();
        if (Player.Energy < moveCost) return false;
        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        int direction = Player.Facing == FacingPositive ? 1 : -1;
        if (action == (int)MoveAction.Backward) direction *= -1;
        int stepCount = rules.Get(GDScriptKeys.GameRules.PlayerMoveStep).AsInt32();
        for (int i = 1; i <= stepCount; i++)
        {
            int candidate = Player.MapPosition + direction * i;
            if (!battleMap.Call(GDScriptKeys.BattleMap.IsValidCell, candidate).AsBool()) return false;
            foreach (var enemy in EnemyManager.Enemies)
                if (enemy != null && enemy.IsAlive && candidate == enemy.MapPosition) return false;
        }
        return true;
    }

    public void TryMove(int action)
    {
        if (!CanAcceptPlayerAction()) { Log("当前阶段不能移动。"); return; }
        if (!CanPlayerMove(action)) { Log("该方向已到边界或被阻挡。"); return; }
        int moveCost = rules.Get(GDScriptKeys.GameRules.MoveEnergyCost).AsInt32();
        int direction = Player.Facing == FacingPositive ? 1 : -1;
        if (action == (int)MoveAction.Backward) direction *= -1;
        int oldPos = Player.MapPosition;
        int stepCount = rules.Get(GDScriptKeys.GameRules.PlayerMoveStep).AsInt32();
        Player.SetMapPosition(oldPos + direction * stepCount);
        Player.SpendEnergy(moveCost);
        foreach (var enemy in EnemyManager.Enemies) enemy?.UpdateFacing(Player.MapPosition);
        Log($"{(action == (int)MoveAction.Forward ? "前进" : "后退")}：{oldPos} → {Player.MapPosition}，消耗{moveCost}能量。");
        PlanEnemyTurns();
        EmitSignal(SignalName.BattleStateChanged);
    }

    public void MoveCombatantTowardOpponent(int target, int amount) => MoveCombatantRelative(target, amount, true);
    public void MoveCombatantAwayFromOpponent(int target, int amount) => MoveCombatantRelative(target, amount, false);
    public void MoveEnemyToward(EnemyBattle actor, int amount) => MoveSpecificEnemy(actor, amount, true);
    public void MoveEnemyAway(EnemyBattle actor, int amount) => MoveSpecificEnemy(actor, amount, false);

    private void MoveCombatantRelative(int target, int amount, bool toward)
    {
        if (rules == null || amount <= 0) return;
        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        bool isPlayer = target == 0;
        var targetEnemy = ActingEnemy ?? _targetEnemy ?? EnemyManager.GetPrimaryEnemy();
        int actorPos = isPlayer ? Player.MapPosition : targetEnemy?.MapPosition ?? 0;
        int opponentPos = isPlayer ? targetEnemy?.MapPosition ?? 0 : Player.MapPosition;
        int dir = Mathf.Sign(opponentPos - actorPos);
        if (!toward) dir *= -1;
        if (dir == 0) return;
        int oldPos = actorPos;
        for (int i = 0; i < amount; i++) { int candidate = actorPos + dir; if (!battleMap.Call(GDScriptKeys.BattleMap.IsValidCell, candidate).AsBool()) break; if (candidate == opponentPos) break; if (!isPlayer && EnemyManager.IsCellOccupiedByEnemy(candidate, targetEnemy)) break; actorPos = candidate; }
        if (isPlayer)
            Player.SetMapPosition(actorPos);
        else
        {
            var enemy = targetEnemy;
            if (enemy != null) enemy.SetMapPosition(actorPos);
        }
        foreach (var enemy in EnemyManager.Enemies) enemy?.UpdateFacing(Player.MapPosition);
        int moved = Mathf.Abs(actorPos - oldPos);
        if (moved > 0) Log($"{(isPlayer ? Player.DisplayName : "怪物")}移动{moved}格：{oldPos} → {actorPos}。");
    }

    private void MoveSpecificEnemy(EnemyBattle actor, int amount, bool toward)
    {
        if (actor == null || !actor.IsAlive || actor.FixedPosition || rules == null || amount <= 0) return;
        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        int direction = Mathf.Sign(Player.MapPosition - actor.MapPosition);
        if (!toward) direction *= -1;
        if (direction == 0) return;
        int current = actor.MapPosition;
        for (int i = 0; i < amount; i++)
        {
            int candidate = current + direction;
            if (!battleMap.Call(GDScriptKeys.BattleMap.IsValidCell, candidate).AsBool()) break;
            if (candidate == Player.MapPosition || EnemyManager.IsCellOccupiedByEnemy(candidate, actor)) break;
            current = candidate;
        }
        actor.SetMapPosition(current);
        foreach (var enemy in EnemyManager.Enemies) enemy?.UpdateFacing(Player.MapPosition);
    }

    public void SwapPosition()
    {
        var enemy = EnemyManager.GetPrimaryEnemy();
        if (enemy == null) return;
        int behindEnemy = enemy.Facing == FacingPositive ? enemy.MapPosition - 1 : enemy.MapPosition + 1;
        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        if (!battleMap.Call(GDScriptKeys.BattleMap.IsValidCell, behindEnemy).AsBool()) { Log("敌人背后无空间，无法绕后。"); return; }
        if (behindEnemy == enemy.MapPosition) { Log("绕后位置与敌人重叠，无法执行。"); return; }
        Player.SetMapPosition(behindEnemy);
        Player.Facing = Player.Facing == FacingPositive ? FacingNegative : FacingPositive;
        enemy.Facing = enemy.Facing == FacingPositive ? FacingNegative : FacingPositive;
        foreach (var e in EnemyManager.Enemies) e?.UpdateFacing(Player.MapPosition);
        Log($"绕后：移动到{behindEnemy}，双方朝向翻转。");
        EmitSignal(SignalName.BattleStateChanged);
    }

    public async void EndTurn()
    {
        if (CurrentPhase != Phase.PlayerTurn || turnTransitionLocked) return;
        turnTransitionLocked = true;

        // Enemy guard expires as the enemy turn begins. Clear every living
        // enemy together before the phase signal and before anyone acts;
        // shield granted later this same enemy turn must remain intact.
        foreach (var enemy in EnemyManager.Enemies)
            if (enemy != null && enemy.IsAlive)
                enemy.ClearShield();

        SetPhase(Phase.EnemyTurn);
        bool preserve = rules.Get(GDScriptKeys.GameRules.PreservePartialCharge).AsBool();
        if (!preserve) BoardManager.ClearAllLights();
        EffectResolver?.TickActorBuffs(Player, this, false);
        BoardManager.TickCellBuffsAtEnd();
        Player.ClearTurnModifiers();
        Log("玩家结束回合。");
        EmitSignal(SignalName.BattleStateChanged);
        await ToSignal(GetTree().CreateTimer(0.35f), "timeout");
        if (CurrentPhase == Phase.BattleEnd) { turnTransitionLocked = false; return; }
        ResolveEnemyTurn();
        EmitSignal(SignalName.BattleStateChanged);
        if (CheckBattleEnd()) { turnTransitionLocked = false; return; }
        await ToSignal(GetTree().CreateTimer(0.35f), "timeout");
        RoundNumber++;
        StartPlayerTurn();
        turnTransitionLocked = false;
        EmitSignal(SignalName.BattleStateChanged);
    }

    private void ResolveEnemyTurn()
    {
        var deferredRoleActions = new System.Collections.Generic.List<GodotObject>();
        foreach (var enemy in EnemyManager.Enemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            ActingEnemy = enemy;
            EffectResolver?.TickActorBuffs(enemy, this, true);
            if (!enemy.IsAlive) continue;
            int dist = GetDistanceTo(enemy);
            var action = enemy.PlannedAction ?? SelectActionForEnemy(enemy, dist);
            if (action == null)
            {
                EffectResolver?.TickActorBuffs(enemy, this, false);
                continue;
            }
            ConfirmPlannedAction(enemy, action, dist);
            if (_cancelLockedIntent && enemy == _smokeCancelledEnemy && CanSmokeCancel(action))
            {
                Log($"烟雾弹取消了{enemy.DisplayName}的锁定攻击。");
                enemy.ClearPlannedAction();
                EffectResolver?.TickActorBuffs(enemy, this, false);
                continue;
            }
            if (!EnemyActionUsesFixedTargets(action)
                && !IsInRange(enemy.MapPosition, Player.MapPosition, enemy.Facing,
                action.Get(GDScriptKeys.EnemyAction.MinRange).AsInt32(),
                action.Get(GDScriptKeys.EnemyAction.MaxRange).AsInt32()))
            {
                EffectResolver?.TickActorBuffs(enemy, this, false);
                enemy.ClearPlannedAction();
                continue;
            }
            Log($"{enemy.DisplayName}使用「{action.Get(GDScriptKeys.EnemyAction.DisplayName)}」。");

            enemy.PlayAttackSfx();  // ← 攻击音效

            EffectResolver.ExecuteEnemyAction(action, this, enemy);
            EffectResolver.ExecuteEnemyPatternHitEffects(action, this, enemy);
            if (EffectResolver.HasDeferredEnemyRoleEffects(action))
                deferredRoleActions.Add(action);
            EffectResolver.ResolvePendingBuffApplications(this);
            enemy.ClearPlannedAction();
            EffectResolver?.TickActorBuffs(enemy, this, false);
        }
        foreach (var action in deferredRoleActions)
            EffectResolver.ExecuteDeferredEnemyRoleEffects(action, this);
        _cancelLockedIntent = false;
        _smokeCancelledEnemy = null;
        ActingEnemy = null;
    }

    private int CalcDistanceTo(int enemyPos)
    {
        var battleMap = rules?.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        if (battleMap == null) return 0;
        return battleMap.Call(GDScriptKeys.BattleMap.CombatDistance, Player.MapPosition, enemyPos).AsInt32();
    }

    private GodotObject SelectActionForEnemy(EnemyBattle enemy, int distance)
    {
        var enemyData = enemy.GetEnemyData();
        if (enemyData == null) return null;
        UpdateEnemyAIContext(enemy, false);
        return enemyData.Call("get_action_for_distance", distance).As<GodotObject>();
    }

    private void ConfirmPlannedAction(EnemyBattle enemy, GodotObject lockedAction, int distance)
    {
        var enemyData = enemy?.GetEnemyData();
        if (enemyData == null) return;
        UpdateEnemyAIContext(enemy, true);
        // Guard expiry is part of the public context, but it must not reroll an
        // intent after the player has ended the turn. Node adapters can confirm
        // the exact locked preview; legacy data keeps the old lookup fallback.
        if (enemyData.HasMethod("confirm_locked_action"))
            enemyData.Call("confirm_locked_action", lockedAction, distance);
        else
            enemyData.Call("get_action_for_distance", distance);
    }

    private void UpdateEnemyAIContext(EnemyBattle enemy, bool confirm)
    {
        var enemyData = enemy?.GetEnemyData();
        if (enemyData == null || !enemyData.HasMethod("set_ai_runtime_context")) return;
        var trueHand = EnemyManager?.GetAliveByRole("true_hand");
        var falseHand = EnemyManager?.GetAliveByRole("false_hand");
        var body = EnemyManager?.GetAliveByRole("body");
        var context = new Dictionary
        {
            { "enemy_id", enemy.EnemyId },
            { "role", enemy.Role },
            { "enemy_hp", enemy.CurrentHp },
            { "enemy_max_hp", enemy.MaxHp },
            { "enemy_shield", enemy.Shield },
            { "enemy_position", enemy.MapPosition },
            { "player_hp", Player?.CurrentHp ?? 0 },
            { "player_max_hp", Player?.MaxHp ?? 0 },
            { "player_shield", Player?.Shield ?? 0 },
            { "player_position", Player?.MapPosition ?? 1 },
            { "distance", GetDistanceTo(enemy) },
            { "round_number", Mathf.Max(1, RoundNumber) },
            { "phase", enemy.Role == "body" ? 2 : 1 },
            { "battle_phase", confirm ? (int)Phase.EnemyTurn : (int)CurrentPhase },
            { "true_hand_hp", trueHand?.CurrentHp ?? 0 },
            { "false_hand_hp", falseHand?.CurrentHp ?? 0 },
            { "body_hp", body?.CurrentHp ?? 0 },
            { "enemy_has_true_buff", EnemyHasBuff(enemy, "true") },
            { "last_player_damage", _lastPlayerDamage },
            { "damage_taken_last_turn", enemy.DamageTakenThisPlayerTurn },
            { "last_player_action_type", _lastPlayerActionType },
            { "last_attack_class", _lastPlayerActionType },
            { "player_has_unlit_cell", BoardHasUnlitCell() },
        };
        enemyData.Call("set_ai_runtime_context", context);
    }

    private static bool EnemyHasBuff(EnemyBattle enemy, string buffId)
    {
        var stats = enemy?.GetStats();
        return stats != null
            && stats.HasMethod(GDScriptKeys.Stats.HasBuff)
            && stats.Call(GDScriptKeys.Stats.HasBuff, buffId).AsBool();
    }

    public int GetDistanceTo(EnemyBattle enemy) => enemy == null ? 0 : CalcDistanceTo(enemy.MapPosition);

    public GodotObject GetPlannedAction(EnemyBattle enemy) => enemy?.PlannedAction;

    /// <summary>Plans one stable action per living enemy for UI preview and later execution.</summary>
    public void PlanEnemyTurns()
    {
        if (EnemyManager == null || Player == null || rules == null) return;
        EnsureDeathLoopCanResolve();
        foreach (var enemy in EnemyManager.Enemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            var action = SelectActionForEnemy(enemy, GetDistanceTo(enemy));
            enemy.SetPlannedAction(action);
        }
        EmitSignal(SignalName.BattleStateChanged);
    }

    public void ReplanEnemyTurns() => PlanEnemyTurns();

    private int CalcDistance()
    {
        int minDist = int.MaxValue;
        foreach (var enemy in EnemyManager.Enemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            int dist = CalcDistanceTo(enemy.MapPosition);
            if (dist < minDist) minDist = dist;
        }
        return minDist == int.MaxValue ? 0 : minDist;
    }

    public void DamageEnemy(int amount) => DamageEnemy(amount, null);

    public void DamageEnemy(int amount, EnemyBattle explicitTarget)
    {
        var e = explicitTarget ?? _targetEnemy ?? ActingEnemy ?? EnemyManager.GetPrimaryEnemy();
        if (e != null)
        {
            int resolvedAmount = EffectResolver?.ResolveDamageWithBuffs(
                amount,
                Player?.GetStats(),
                e.GetStats()) ?? amount;
            int minimumHp = _trueDeathLoopActive && e.Role == "true_hand" ? 1 : 0;
            e.TakeDamage(resolvedAmount, minimumHp);
            Log($"{e.DisplayName}受到{resolvedAmount}点伤害。");
        }
        _targetEnemy = null;
        EnsureDeathLoopCanResolve();
    }
    public void DamagePlayer(int amount)
    {
        int resolvedAmount = EffectResolver?.ResolveDamageWithBuffs(
            amount,
            ActingEnemy?.GetStats(),
            Player?.GetStats()) ?? amount;
        Player?.TakeDamage(resolvedAmount);
        Log($"玩家受到{resolvedAmount}点伤害。");
    }
    public void AddPlayerShield(int amount) { Player?.AddShield(amount); Log($"玩家获得{amount}点护盾。"); }
    public void AddEnemyShield(int amount) => AddEnemyShield(amount, null);
    public void AddEnemyShield(int amount, EnemyBattle explicitTarget)
    {
        var e = explicitTarget ?? ActingEnemy ?? _targetEnemy ?? EnemyManager.GetPrimaryEnemy();
        if (e != null) { e.AddShield(amount); Log($"{e.DisplayName}获得{amount}点护盾。"); }
        _targetEnemy = null;
    }
    public void AddPlayerEnergy(int amount) { Player?.AddEnergy(amount); Log($"玩家获得{amount}点能量。"); }
    public void HealPlayer(int amount) { int old = Player?.CurrentHp ?? 0; Player?.Heal(amount); Log($"玩家恢复{(Player?.CurrentHp ?? 0) - old}点生命。"); }
    public void HealEnemy(int amount) => HealEnemy(amount, null);
    public void HealEnemy(int amount, EnemyBattle explicitTarget)
    {
        var e = explicitTarget ?? ActingEnemy ?? _targetEnemy ?? EnemyManager.GetPrimaryEnemy();
        if (e != null) { int old = e.CurrentHp; e.Heal(amount); Log($"{e.DisplayName}恢复{e.CurrentHp - old}点生命。"); }
        _targetEnemy = null;
    }

    public bool HealEnemyByRole(StringName role, int amount)
    {
        var enemy = EnemyManager?.GetAliveByRole(role);
        if (enemy == null || amount <= 0 || enemy.CurrentHp >= enemy.MaxHp) return false;
        int old = enemy.CurrentHp;
        enemy.Heal(amount);
        Log($"{enemy.DisplayName}恢复{enemy.CurrentHp - old}点生命。");
        return enemy.CurrentHp > old;
    }

    public void AddShieldToActingEnemy(int amount)
    {
        if (ActingEnemy == null || amount <= 0) return;
        ActingEnemy.AddShield(amount);
        Log($"{ActingEnemy.DisplayName}获得{amount}点护盾。");
    }

    public void PushPlayerToEdge(EnemyBattle actor, int edgeCell)
    {
        if (Player == null || rules == null) return;
        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        int cellCount = battleMap.Get(GDScriptKeys.BattleMap.CellCount).AsInt32();
        int destination = Mathf.Clamp(edgeCell, 1, cellCount);
        if (EnemyManager.IsCellOccupiedByEnemy(destination))
            destination = actor != null && actor.MapPosition < Player.MapPosition ? cellCount : 1;
        if (!EnemyManager.IsCellOccupiedByEnemy(destination))
            Player.SetMapPosition(destination);
        foreach (var enemy in EnemyManager.Enemies) enemy?.UpdateFacing(Player.MapPosition);
    }

    /// <summary>
    /// Resolves Sharkk's charge as one atomic semantic action: approach the
    /// player's adjacent cell, deal damage, then push in the original direction
    /// to the last empty legal cell. No combatant can be crossed.
    /// </summary>
    public bool ExecuteChargePush(EnemyBattle actor, int damage)
    {
        if (actor == null || !actor.IsAlive || actor.FixedPosition || Player == null || rules == null)
            return false;
        int direction = Mathf.Sign(Player.MapPosition - actor.MapPosition);
        if (direction == 0) return false;
        var map = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();

        int actorCell = actor.MapPosition;
        while (true)
        {
            int candidate = actorCell + direction;
            if (!map.Call(GDScriptKeys.BattleMap.IsValidCell, candidate).AsBool()) break;
            if (candidate == Player.MapPosition) break;
            if (EnemyManager.IsCellOccupiedByEnemy(candidate, actor)) break;
            actorCell = candidate;
        }
        actor.SetMapPosition(actorCell);
        if (Mathf.Abs(Player.MapPosition - actor.MapPosition) != 1) return false;

        DamagePlayer(Mathf.Max(0, damage));
        int playerCell = Player.MapPosition;
        while (true)
        {
            int candidate = playerCell + direction;
            if (!map.Call(GDScriptKeys.BattleMap.IsValidCell, candidate).AsBool()) break;
            if (EnemyManager.IsCellOccupiedByEnemy(candidate)) break;
            playerCell = candidate;
        }
        Player.SetMapPosition(playerCell);
        UpdateAllFacings();
        return true;
    }

    public bool MoveEnemyBehindPlayer(EnemyBattle actor)
    {
        if (actor == null || actor.FixedPosition || rules == null) return false;
        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        int preferred = Player.Facing == FacingPositive ? Player.MapPosition - 1 : Player.MapPosition + 1;
        if (!battleMap.Call(GDScriptKeys.BattleMap.IsValidCell, preferred).AsBool()
            || EnemyManager.IsCellOccupiedByEnemy(preferred, actor))
            preferred = Player.Facing == FacingPositive ? Player.MapPosition + 1 : Player.MapPosition - 1;
        if (!battleMap.Call(GDScriptKeys.BattleMap.IsValidCell, preferred).AsBool()
            || EnemyManager.IsCellOccupiedByEnemy(preferred, actor)) return false;
        actor.SetMapPosition(preferred);
        UpdateAllFacings();
        return true;
    }

    public void AddAllCardCooldown(int amount)
    {
        if (BoardManager == null || amount <= 0) return;
        foreach (var runtime in BoardManager.runtime_cards)
        {
            int current = runtime.Get(GDScriptKeys.CardRuntime.CooldownRemaining).AsInt32();
            int next = current + amount;
            int instanceId = runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32();
            runtime.Set(GDScriptKeys.CardRuntime.CooldownRemaining, next);
            runtime.Set(GDScriptKeys.CardRuntime.IsReady, false);
            BoardManager.EmitSignal(BoardManager.SignalName.CooldownChanged, instanceId, next);
        }
        BoardManager.EmitSignal(BoardManager.SignalName.BoardChanged);
        EmitSignal(SignalName.BattleStateChanged);
    }

    /// <summary>Sets every equipped card to at least the requested cooldown.</summary>
    public bool SetAllCardCooldownAtLeast(int amount)
    {
        if (BoardManager == null || amount <= 0 || BoardManager.runtime_cards.Count == 0)
            return false;
        bool changed = false;
        foreach (var runtime in BoardManager.runtime_cards)
        {
            int current = runtime.Get(GDScriptKeys.CardRuntime.CooldownRemaining).AsInt32();
            int next = Mathf.Max(current, amount);
            if (next == current && !runtime.Get(GDScriptKeys.CardRuntime.IsReady).AsBool())
                continue;
            runtime.Set(GDScriptKeys.CardRuntime.CooldownRemaining, next);
            runtime.Set(GDScriptKeys.CardRuntime.IsReady, false);
            BoardManager.EmitSignal(BoardManager.SignalName.CooldownChanged,
                runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32(), next);
            changed = true;
        }
        if (changed)
        {
            BoardManager.EmitSignal(BoardManager.SignalName.BoardChanged);
            EmitSignal(SignalName.BattleStateChanged);
        }
        return changed;
    }

    public bool DrainPlayerEnergy(int amount)
    {
        if (Player == null || amount <= 0 || Player.Energy <= 0) return false;
        int drained = Mathf.Min(Player.Energy, amount);
        bool result = Player.SpendEnergy(drained);
        if (result) Log($"False干扰：玩家失去{drained}点能量。");
        return result;
    }

    public bool HealBuffOwner(EnemyBattle actor, int amount)
    {
        if (amount <= 0) return false;
        if (actor != null)
        {
            if (!actor.IsAlive || actor.CurrentHp >= actor.MaxHp) return false;
            int old = actor.CurrentHp;
            actor.Heal(amount);
            Log($"{actor.DisplayName}的治疗包恢复{actor.CurrentHp - old}点生命。");
            return actor.CurrentHp > old;
        }
        if (Player == null || Player.CurrentHp >= Player.MaxHp) return false;
        int playerOld = Player.CurrentHp;
        Player.Heal(amount);
        Log($"部署治疗包恢复{Player.CurrentHp - playerOld}点生命。");
        return Player.CurrentHp > playerOld;
    }

    /// <summary>Moves one legal cell in the player's current forward direction.</summary>
    public bool MovePlayerForwardFreeOne()
    {
        if (Player == null || rules == null) return false;
        var map = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        int direction = Player.Facing == FacingPositive ? 1 : -1;
        int destination = Player.MapPosition + direction;
        if (!map.Call(GDScriptKeys.BattleMap.IsValidCell, destination).AsBool()
            || EnemyManager.IsCellOccupiedByEnemy(destination))
            return false;
        int old = Player.MapPosition;
        Player.SetMapPosition(destination);
        UpdateAllFacings();
        Log($"滑轮鞋：{old} → {destination}。");
        return true;
    }

    /// <summary>Deals item damage to the farthest living enemy, ties by stable spawn order.</summary>
    public bool DamageFarthestEnemy(int amount)
    {
        if (EnemyManager == null || Player == null || amount <= 0) return false;
        EnemyBattle farthest = null;
        int farthestDistance = -1;
        foreach (var enemy in EnemyManager.Enemies)
        {
            if (enemy == null || !enemy.IsAlive) continue;
            int distance = Mathf.Abs(enemy.MapPosition - Player.MapPosition);
            if (distance <= farthestDistance) continue;
            farthest = enemy;
            farthestDistance = distance;
        }
        if (farthest == null) return false;
        DamageEnemy(amount, farthest);
        return true;
    }

    public bool UseItem(int itemIndex)
    {
        if (!CanAcceptPlayerAction() || DataManager.Instance == null) return false;
        var item = DataManager.Instance.GetItem(itemIndex);
        if (item == null) return false;
        var obj = (GodotObject)item;
        var effects = obj.Get(GDScriptKeys.ItemData.Effects).As<Array>();
        var specials = obj.Get(GDScriptKeys.ItemData.SpecialEffects).As<Array>();
        int amount = obj.Get(GDScriptKeys.ItemData.SpecialAmount).AsInt32();

        bool specialMovement = ContainsItemSpecial(specials, 4);
        if (!CanExecuteItemSpecials(specials, amount))
        {
            Log("该道具当前没有可执行目标，因此没有消耗。");
            return false;
        }
        bool executed = !specialMovement && effects != null && effects.Count > 0
            && EffectResolver.ExecuteItemEffects(obj.Get(GDScriptKeys.ItemData.DisplayName).AsString(), effects, this);
        if (specials != null)
        {
            foreach (Variant special in specials)
                executed |= ExecuteItemSpecial(special.AsInt32(), amount);
        }
        if (!executed)
        {
            Log("该道具当前没有可执行目标，因此没有消耗。");
            return false;
        }

        if (obj.Get(GDScriptKeys.ItemData.ConsumeOnUse).AsBool())
            DataManager.Instance.DiscardItem(itemIndex);
        Log($"使用了{obj.Get(GDScriptKeys.ItemData.DisplayName).AsString()}。");
        PlanEnemyTurns();
        EmitSignal(SignalName.BattleStateChanged);
        return true;
    }

    public void DiscardItem(int itemIndex) => DataManager.Instance.DiscardItem(itemIndex);

    public bool IsPlayerOnEvenCell() => IsPlayerOnCellParity(0);

    public bool IsPlayerOnCellParity(int parity)
        => Player != null && Player.MapPosition % 2 == Mathf.PosMod(parity, 2);

    public bool IsPlayerTargetedByAction(GodotObject action)
    {
        if (Player == null || action == null) return false;
        int[] fixedCells = action.Get(GDScriptKeys.EnemyAction.FixedTargetCells).AsInt32Array();
        if (fixedCells.Length > 0)
            return System.Array.IndexOf(fixedCells, Player.MapPosition) >= 0;
        int parity = action.Get(GDScriptKeys.EnemyAction.PatternParity).AsInt32();
        return parity < 0 || IsPlayerOnCellParity(parity);
    }

    public static bool EnemyActionUsesFixedTargets(GodotObject action)
    {
        if (action == null) return false;
        if (action.Get(GDScriptKeys.EnemyAction.PatternParity).AsInt32() >= 0) return true;
        return action.Get(GDScriptKeys.EnemyAction.FixedTargetCells).AsInt32Array().Length > 0;
    }

    public void SetTrueDeathLoop(bool active)
    {
        if (active)
        {
            var falseHand = EnemyManager?.GetByRole("false_hand");
            if (falseHand == null || !falseHand.IsAlive)
            {
                _trueDeathLoopActive = false;
                return;
            }
        }
        _trueDeathLoopActive = active;
        Log(active ? "True手进入死亡循环：生命不会低于1。" : "True手的死亡循环已解除。");
    }

    private void EnsureDeathLoopCanResolve()
    {
        if (!_trueDeathLoopActive || EnemyManager == null) return;
        var falseHand = EnemyManager.GetByRole("false_hand");
        if (falseHand == null || !falseHand.IsAlive)
            SetTrueDeathLoop(false);
    }

    private bool ExecuteItemSpecial(int special, int amount)
    {
        // ItemData.SpecialEffect enum ordering is intentionally mirrored here.
        switch (special)
        {
            case 1: return BoardManager?.ResetAllCooldowns() ?? false;
            case 2: return BoardManager?.ClearNegativeCellBuffs() ?? false;
            case 3: return Player?.AddStrengthThisTurn(Mathf.Max(1, amount)) ?? false;
            case 4: return TryTeleportMove(Mathf.Max(1, amount));
            case 5: return BoardManager?.ResetOneCardCooldown(_lastPlayedCardInstanceId) ?? false;
            case 6:
                _smokeCancelledEnemy = GetSmokeTarget();
                if (_smokeCancelledEnemy == null) return false;
                _cancelLockedIntent = true;
                return true;
            default: return false;
        }
    }

    private static bool ContainsItemSpecial(Array specials, int expected)
    {
        if (specials == null) return false;
        foreach (Variant value in specials)
            if (value.AsInt32() == expected) return true;
        return false;
    }

    private bool CanExecuteItemSpecials(Array specials, int specialAmount)
    {
        if (specials == null || specials.Count == 0) return true;
        bool hasPositiveOperation = false;
        foreach (Variant value in specials)
        {
            switch (value.AsInt32())
            {
                case 1:
                    foreach (var runtime in BoardManager.runtime_cards)
                        if (runtime.Get(GDScriptKeys.CardRuntime.CooldownRemaining).AsInt32() > 0) hasPositiveOperation = true;
                    break;
                case 2: hasPositiveOperation |= BoardManager.HasAnyNegativeCellBuffs(); break;
                case 3: hasPositiveOperation = true; break;
                case 4: hasPositiveOperation |= CanTeleportMove(Mathf.Max(1, specialAmount)); break;
                case 5:
                    foreach (var runtime in BoardManager.runtime_cards)
                        if (runtime.Get(GDScriptKeys.CardRuntime.CooldownRemaining).AsInt32() > 0) hasPositiveOperation = true;
                    break;
                case 6: hasPositiveOperation |= HasCancelableLockedIntent(); break;
            }
        }
        return hasPositiveOperation;
    }

    private bool CanTeleportMove(int steps)
    {
        if (Player == null || rules == null || steps <= 0) return false;
        var map = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        int direction = Player.Facing == FacingPositive ? 1 : -1;
        foreach (int sign in new[] { 1, -1 })
        {
            int destination = Player.MapPosition + direction * sign * steps;
            if (map.Call(GDScriptKeys.BattleMap.IsValidCell, destination).AsBool()
                && !EnemyManager.IsCellOccupiedByEnemy(destination)) return true;
        }
        return false;
    }

    /// <summary>Free two-cell item movement which may cross, but never land on, an enemy.</summary>
    private bool TryTeleportMove(int steps)
    {
        if (Player == null || rules == null || steps <= 0) return false;
        var map = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        int facingDirection = Player.Facing == FacingPositive ? 1 : -1;
        int destination = Player.MapPosition + facingDirection * steps;
        if (!map.Call(GDScriptKeys.BattleMap.IsValidCell, destination).AsBool()
            || EnemyManager.IsCellOccupiedByEnemy(destination))
        {
            destination = Player.MapPosition - facingDirection * steps;
        }
        if (!map.Call(GDScriptKeys.BattleMap.IsValidCell, destination).AsBool()
            || EnemyManager.IsCellOccupiedByEnemy(destination)) return false;

        int old = Player.MapPosition;
        Player.SetMapPosition(destination);
        UpdateAllFacings();
        Log($"瞬移鞋垫：{old} → {destination}。");
        return old != destination;
    }

    private bool HasCancelableLockedIntent()
        => GetSmokeTarget() != null;

    private EnemyBattle GetSmokeTarget()
    {
        if (EnemyManager == null) return null;
        var tracked = GetLockedEnemyFromTV();
        if (tracked != null && tracked.IsAlive && CanSmokeCancel(tracked.PlannedAction))
            return tracked;
        foreach (var enemy in EnemyManager.Enemies)
            if (enemy != null && enemy.IsAlive && CanSmokeCancel(enemy.PlannedAction)) return enemy;
        return null;
    }

    private static bool CanSmokeCancel(GodotObject action)
    {
        if (action == null) return false;
        string id = action.Get(GDScriptKeys.EnemyAction.Id).AsString().ToLowerInvariant();
        var tags = action.Get(GDScriptKeys.EnemyAction.Tags).AsStringArray();
        bool bossLaser = id.Contains("core_") && (id.Contains("beam") || id.Contains("laser"));
        if (bossLaser) return false;
        if (tags.Contains("boss_laser") || tags.Contains("laser")) return false;

        // The MVP resources predate a locked_attack tag. Treat any real attack
        // intent (damage effect) as locked while keeping movement/defence intact.
        if (tags.Contains("locked_attack")) return true;
        var effects = action.Get(GDScriptKeys.EnemyAction.Effects).As<Array>();
        if (effects == null) return false;
        foreach (Variant entry in effects)
        {
            var effect = entry.As<GodotObject>();
            if (effect != null && effect.Call(GDScriptKeys.CombatEffect.TypeKey).AsString() == "damage")
                return true;
        }
        return false;
    }

    private void RecordPlayerCardMetadata(GodotObject data)
    {
        if (data == null) return;
        int minRange = data.Get(GDScriptKeys.CardData.MinRange).AsInt32();
        int maxRange = data.Get(GDScriptKeys.CardData.MaxRange).AsInt32();
        bool attack = data.Call(GDScriptKeys.CardData.HasDamageEffect).AsBool();
        string cardId = data.Get(GDScriptKeys.CardData.Id).AsString();
        bool knownGun = cardId is "quad_coil_gun" or "pea_gun" or "deadly_kiss" or "simple_cannon";
        bool unknownRanged = string.IsNullOrEmpty(cardId) && (minRange >= 2 || maxRange >= 4);
        _lastPlayerActionType = attack && (knownGun || unknownRanged)
            ? new StringName("ranged")
            : attack ? new StringName("melee") : new StringName("utility");
        _lastPlayerDamage = attack ? SumCardDamage(data) + (Player?.StrengthThisTurn ?? 0) : 0;
    }

    private static int SumCardDamage(GodotObject data)
    {
        int total = 0;
        var effects = data.Get(GDScriptKeys.CardData.Effects).As<Array>();
        if (effects != null)
        {
            foreach (Variant entry in effects)
            {
                var effect = entry.As<GodotObject>();
                if (effect != null && effect.Call(GDScriptKeys.CombatEffect.TypeKey).AsString() == "damage")
                    total += effect.Get(GDScriptKeys.CombatEffect.Amount).AsInt32();
            }
        }
        return total;
    }

    private bool BoardHasUnlitCell()
    {
        if (BoardManager == null) return false;
        for (int y = 0; y < BoardManager.rows; y++)
            for (int x = 0; x < BoardManager.columns; x++)
            {
                var cell = BoardManager.GetCell(new Vector2I(x, y));
                if (cell != null && !cell.Get(GDScriptKeys.CellRuntime.IsLit).AsBool()) return true;
            }
        return false;
    }

    private void UpdateAllFacings()
    {
        foreach (var enemy in EnemyManager.Enemies) enemy?.UpdateFacing(Player.MapPosition);
        var closest = EnemyManager.GetClosestAlive(Player.MapPosition);
        if (closest != null)
            Player.Facing = closest.MapPosition >= Player.MapPosition ? FacingPositive : FacingNegative;
    }

    private bool CheckBattleEnd()
    {
        if (!EnemyManager.HasAliveEnemies()) { turnTransitionLocked = true; SetPhase(Phase.BattleEnd); if (!battleEndEmitted) { battleEndEmitted = true; Log("战斗胜利。"); EmitSignal(SignalName.BattleEnded, true); } return true; }
        if (Player != null && !Player.IsAlive) { turnTransitionLocked = true; SetPhase(Phase.BattleEnd); if (!battleEndEmitted) { battleEndEmitted = true; Log("战斗失败。"); EmitSignal(SignalName.BattleEnded, false); } return true; }
        return false;
    }

    private bool CanAcceptPlayerAction() => CurrentPhase == Phase.PlayerTurn && !turnTransitionLocked && BoardManager != null && rules != null && Player != null;
    private void SetPhase(Phase newPhase) { if (CurrentPhase == newPhase) return; CurrentPhase = newPhase; EmitSignal(SignalName.PhaseChanged, (int)newPhase); }
    private void Log(string text) => EmitSignal(SignalName.LogMessage, text);

    public void TryMoveToCell(int targetCell)
{
    if (!CanAcceptPlayerAction()) { Log("当前阶段不能移动。"); return; }

    var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
    int stepCost = rules.Get(GDScriptKeys.GameRules.MoveEnergyCost).AsInt32();
    int currentPos = Player.MapPosition;

    int maxSteps = 99; // 安全上限
    for (int step = 0; step < maxSteps; step++)
    {
        if (currentPos == targetCell) break;

        int direction = Mathf.Sign(targetCell - currentPos);
        if (direction == 0) break;

        int candidate = currentPos + direction;

        // 边界检查
        if (!battleMap.Call(GDScriptKeys.BattleMap.IsValidCell, candidate).AsBool())
        { Log("路径被边界阻挡。"); return; }

        // 敌人阻挡
        if (EnemyManager.IsCellOccupiedByEnemy(candidate))
        { Log("路径被敌人阻挡。"); return; }

        // 能量检查
        if (Player.Energy < stepCost)
        { Log($"能量不足，需要{stepCost}点。"); return; }

        Player.SpendEnergy(stepCost);
        currentPos = candidate;
        Player.SetMapPosition(currentPos);
        Log($"移动：{currentPos}，消耗{stepCost}能量。");
    }

    foreach (var enemy in EnemyManager.Enemies)
        enemy?.UpdateFacing(Player.MapPosition);

    if (currentPos == targetCell)
    {
        Log($"到达 {targetCell}。");
    }
    else
    {
        Log($"移动到 {currentPos}，无法继续。");
    }

    PlanEnemyTurns();
    EmitSignal(SignalName.BattleStateChanged);
}
}
