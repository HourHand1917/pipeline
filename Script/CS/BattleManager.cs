using Godot;
using Godot.Collections;

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

    [Export] private Resource _gameRules;

    public PlayerBattle Player { get; set; }
    public EnemyManager EnemyManager { get; set; }
    public BoardManager BoardManager { get; set; }
    public EffectResolver EffectResolver { get; set; }

    public Phase CurrentPhase { get; private set; } = Phase.Build;
    public int RoundNumber { get; private set; } = 0;
    public int Distance => CalcDistance();

    private GodotObject rules;
    private Dictionary<int, bool> usedCardIds = new();
    private bool turnTransitionLocked;
    private bool battleEndEmitted;

    public int PlayerHp => Player?.CurrentHp ?? 0;
    public int PlayerMaxHp => Player?.MaxHp ?? 0;
    public int PlayerEnergy => Player?.Energy ?? 0;
    public int PlayerShield => Player?.Shield ?? 0;
    public int PlayerMapPosition => Player?.MapPosition ?? 0;
    public int EnemyHp => EnemyManager?.GetPrimaryEnemy()?.CurrentHp ?? 0;
    public int EnemyMaxHp => EnemyManager?.GetPrimaryEnemy()?.MaxHp ?? 0;
    public int EnemyShield => EnemyManager?.GetPrimaryEnemy()?.Shield ?? 0;
    public int EnemyMapPosition => EnemyManager?.GetPrimaryEnemy()?.MapPosition ?? 0;

    public static bool IsInRange(int selfPos, int targetPos, int facing, int minRange, int maxRange)
    {
        if (minRange <= 0 && maxRange <= 0) return true;
        if (facing == FacingPositive) { int dist = targetPos - selfPos; return dist >= minRange && dist <= maxRange; }
        else { int dist = selfPos - targetPos; return dist >= minRange && dist <= maxRange; }
    }

    public void Setup() { }

    // ================================================================
    //  战斗开始
    // ================================================================

    public void StartBattle()
    {
        if (_gameRules == null) { Log("战斗无法开始：请先配置 GameRules 资源。"); return; }
        rules = _gameRules as GodotObject;
        if (rules == null) { Log("战斗无法开始：规则资源无效。"); return; }
        if (Player == null || EnemyManager == null || BoardManager == null) { Log("战斗无法开始：实例未注入。"); return; }

        turnTransitionLocked = false;
        battleEndEmitted = false;
        usedCardIds.Clear();
        RoundNumber = 1;

        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        var playerData = rules.Get(GDScriptKeys.GameRules.PlayerData).As<GodotObject>();
        Player.LoadFromData(playerData, rules);

        EnemyManager.SpawnAllFromConfigs(rules);
        var primaryEnemy = EnemyManager.GetPrimaryEnemy();
        int playerStartCell = battleMap.Get(GDScriptKeys.BattleMap.PlayerStartCell).AsInt32();
        int enemyStartCell = battleMap.Get(GDScriptKeys.BattleMap.EnemyStartCell).AsInt32();
        Player.SetMapPosition(playerStartCell);
        EnemyManager.SetAllPositions(enemyStartCell);
        Player.Facing = FacingPositive;
        foreach (var enemy in EnemyManager.Enemies) enemy?.UpdateFacing(Player.MapPosition);

        BoardManager.ResetAllCardStates();
        SetPhase(Phase.PlayerTurn);
        Player.ResetEnergy();
        Player.EmitHealthChanged();

        Log($"战斗开始：{battleMap.Get(GDScriptKeys.BattleMap.CellCount).AsInt32()}格地图，{Player.DisplayName}{playerStartCell}，{primaryEnemy.DisplayName}{enemyStartCell}，距离{Distance}格；{Player.DisplayName}获得{Player.Energy}点能量。");
        EmitSignal(SignalName.BattleStateChanged);
    }

    public void ReturnToBuild() { turnTransitionLocked = false; usedCardIds.Clear(); SetPhase(Phase.Build); EmitSignal(SignalName.BattleStateChanged); }

    // ================================================================
    //  玩家回合开始
    // ================================================================

    private void StartPlayerTurn()
    {
        bool preserve = rules.Get(GDScriptKeys.GameRules.PreservePartialCharge).AsBool();
        if (!preserve) BoardManager.ClearAllLights();
        BoardManager.TickCooldowns(); BoardManager.TickCellBuffs();
        Player.ResetEnergy(); usedCardIds.Clear(); SetPhase(Phase.PlayerTurn);
        Log($"第{RoundNumber}回合开始：能量重置为{Player.Energy}，冷却-1。");
    }

    // ================================================================
    //  点亮格子
    // ================================================================

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
        if (stats != null && stats.Call(GDScriptKeys.Stats.HasBuff, "disabled").AsBool()) { Log("该格子已失效，无法点亮。"); return; }

        int chargeCost = rules.Get(GDScriptKeys.GameRules.ChargeEnergyCost).AsInt32();
        int dustExtra = stats != null ? stats.Call(GDScriptKeys.Stats.GetBuffStacks, "dust").AsInt32() : 0;
        int totalCost = chargeCost + dustExtra;

        if (!Player.SpendEnergy(totalCost)) { string dustMsg = dustExtra > 0 ? $"（蒙尘额外消耗+{dustExtra}）" : ""; Log($"能量不足，点亮需要{totalCost}点能量{dustMsg}。"); return; }

        var cardData = runtime.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
        if (cardData.Get(GDScriptKeys.CardData.OncePerTurn).AsBool() && usedCardIds.ContainsKey(runtime.Get(GDScriptKeys.CardRuntime.InstanceId).AsInt32()))
        { Log($"{cardData.Get(GDScriptKeys.CardData.DisplayName)}本回合已经发动过。"); return; }

        BoardManager.SetCellLit(position, true);
        EmitSignal(SignalName.BattleStateChanged);
    }

    // ================================================================
    //  发动卡牌
    // ================================================================

    public void TryPlayCard(int instanceId)
    {
        if (!CanAcceptPlayerAction()) { Log("当前阶段不能发动装备。"); return; }

        var runtime = BoardManager.GetRuntimeCard(instanceId);
        if (runtime == null) return;
        var data = runtime.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();

        if (data.Get(GDScriptKeys.CardData.OncePerTurn).AsBool() && usedCardIds.ContainsKey(instanceId))
        { Log($"{data.Get(GDScriptKeys.CardData.DisplayName)}本回合已经发动过。"); return; }
        if (!BoardManager.CheckCardReady(runtime)) { Log($"{data.Get(GDScriptKeys.CardData.DisplayName)}尚未全部点亮。"); return; }

        if (data.Call(GDScriptKeys.CardData.HasDamageEffect).AsBool())
        {
            int minRange = data.Get(GDScriptKeys.CardData.MinRange).AsInt32();
            int maxRange = data.Get(GDScriptKeys.CardData.MaxRange).AsInt32();
            var enemy = EnemyManager.GetPrimaryEnemy();
            int enemyPos = enemy?.MapPosition ?? 0;
            if (!IsInRange(Player.MapPosition, enemyPos, Player.Facing, minRange, maxRange))
            { Log($"{data.Get(GDScriptKeys.CardData.DisplayName)}射程不足：距离{Distance}格，需要{minRange}–{maxRange}格（朝向{Player.Facing}）。"); return; }
        }

        turnTransitionLocked = true;
        if (!EffectResolver.ExecuteCard(runtime, this)) { turnTransitionLocked = false; Log($"{data.Get(GDScriptKeys.CardData.DisplayName)}效果执行失败。"); return; }

        if (data.Get(GDScriptKeys.CardData.OncePerTurn).AsBool()) usedCardIds[instanceId] = true;

        int cooldownTurns = data.Get(GDScriptKeys.CardData.CooldownTurns).AsInt32();
        runtime.Set(GDScriptKeys.CardRuntime.CooldownRemaining, cooldownTurns);
        BoardManager.ClearCardLights(runtime);
        Log($"{data.Get(GDScriptKeys.CardData.DisplayName)}发动，进入{cooldownTurns}回合冷却。");

        if (!CheckBattleEnd()) turnTransitionLocked = false;
        EmitSignal(SignalName.BattleStateChanged);
    }

    // ================================================================
    //  移动
    // ================================================================

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
        int enemyPos = EnemyManager.GetPrimaryEnemy()?.MapPosition ?? 0;
        for (int i = 1; i <= stepCount; i++)
        {
            int candidate = Player.MapPosition + direction * i;
            if (!battleMap.Call(GDScriptKeys.BattleMap.IsValidCell, candidate).AsBool()) return false;
            if (candidate == enemyPos) return false;
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
        Log($"{(action == (int)MoveAction.Forward ? "前进" : "后退")}：{oldPos} → {Player.MapPosition}，距离{Distance}格，消耗{moveCost}能量。");
        EmitSignal(SignalName.BattleStateChanged);
    }

    public void MoveCombatantTowardOpponent(int target, int amount) => MoveCombatantRelative(target, amount, true);
    public void MoveCombatantAwayFromOpponent(int target, int amount) => MoveCombatantRelative(target, amount, false);

    private void MoveCombatantRelative(int target, int amount, bool toward)
    {
        if (rules == null || amount <= 0) return;
        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        bool isPlayer = target == 0;
        int actorPos = isPlayer ? Player.MapPosition : EnemyManager.GetPrimaryEnemy()?.MapPosition ?? 0;
        int opponentPos = isPlayer ? EnemyManager.GetPrimaryEnemy()?.MapPosition ?? 0 : Player.MapPosition;
        int dir = Mathf.Sign(opponentPos - actorPos);
        if (!toward) dir *= -1;
        if (dir == 0) return;

        int oldPos = actorPos;
        for (int i = 0; i < amount; i++) { int candidate = actorPos + dir; if (!battleMap.Call(GDScriptKeys.BattleMap.IsValidCell, candidate).AsBool()) break; if (candidate == opponentPos) break; actorPos = candidate; }

        if (isPlayer) Player.SetMapPosition(actorPos); else EnemyManager.SetAllPositions(actorPos);
        foreach (var enemy in EnemyManager.Enemies) enemy?.UpdateFacing(Player.MapPosition);

        int moved = Mathf.Abs(actorPos - oldPos);
        if (moved > 0) Log($"{(isPlayer ? Player.DisplayName : "怪物")}地图上{(toward ? "接近" : "远离")}对手{moved}格：{oldPos} → {actorPos}；距离{Distance}格。");
    }

    public void SwapPosition()
    {
        var enemy = EnemyManager.GetPrimaryEnemy();
        if (enemy == null) return;
        int playerFacing = Player.Facing;
        int enemyFacing = enemy.Facing;
        int behindEnemy = enemyFacing == FacingPositive ? enemy.MapPosition - 1 : enemy.MapPosition + 1;

        var battleMap = rules.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        if (!battleMap.Call(GDScriptKeys.BattleMap.IsValidCell, behindEnemy).AsBool()) { Log("敌人背后无空间，无法绕后。"); return; }
        if (behindEnemy == enemy.MapPosition) { Log("绕后位置与敌人重叠，无法执行。"); return; }

        Player.SetMapPosition(behindEnemy);
        Player.Facing = playerFacing == FacingPositive ? FacingNegative : FacingPositive;
        enemy.Facing = enemyFacing == FacingPositive ? FacingNegative : FacingPositive;
        foreach (var e in EnemyManager.Enemies) e?.UpdateFacing(Player.MapPosition);
        Log($"绕后：移动到{behindEnemy}，双方朝向翻转。");
        EmitSignal(SignalName.BattleStateChanged);
    }

    // ================================================================
    //  回合结束
    // ================================================================

    public async void EndTurn()
    {
        if (CurrentPhase != Phase.PlayerTurn || turnTransitionLocked) return;
        turnTransitionLocked = true;
        SetPhase(Phase.EnemyTurn);

        bool preserve = rules.Get(GDScriptKeys.GameRules.PreservePartialCharge).AsBool();
        if (preserve) Log("玩家结束回合；未完成的点亮格会保留。");
        else { BoardManager.ClearAllLights(); Log("玩家结束回合；未完成的点亮格已清除。"); }
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
        var enemy = EnemyManager.GetPrimaryEnemy();
        if (enemy == null) return;
        var action = EnemyManager.GetActionForDistance(Distance);
        if (action == null) { Log("敌人没有可用行动，原地等待。"); return; }

        int minRange = action.Get(GDScriptKeys.EnemyAction.MinRange).AsInt32();
        int maxRange = action.Get(GDScriptKeys.EnemyAction.MaxRange).AsInt32();
        if (!IsInRange(enemy.MapPosition, Player.MapPosition, enemy.Facing, minRange, maxRange))
        { Log($"{enemy.DisplayName}未面向玩家（朝向{enemy.Facing}，距离{Distance}），跳过行动。"); return; }

        Log($"{enemy?.DisplayName ?? "敌人"}使用「{action.Get(GDScriptKeys.EnemyAction.DisplayName)}」。");
        EffectResolver.ExecuteEnemyAction(action, this);
    }

    // ================================================================
    //  伤害 / 回复 / 道具
    // ================================================================

    public void DamageEnemy(int amount) { var e = EnemyManager.GetPrimaryEnemy(); if (e != null) { e.TakeDamage(amount); Log($"{e.DisplayName}受到{amount}点伤害，剩余{e.CurrentHp}生命。"); } }
    public void DamagePlayer(int amount) { Player?.TakeDamage(amount); Log($"玩家受到{amount}点伤害。"); }
    public void AddPlayerShield(int amount) { Player?.AddShield(amount); Log($"玩家获得{amount}点护盾，当前{Player.Shield}。"); }
    public void AddEnemyShield(int amount) { var e = EnemyManager.GetPrimaryEnemy(); if (e != null) { e.AddShield(amount); Log($"{e.DisplayName}获得{amount}点护盾，当前{e.Shield}。"); } }
    public void AddPlayerEnergy(int amount) { Player?.AddEnergy(amount); Log($"玩家获得{amount}点能量，当前{Player.Energy}。"); }
    public void HealPlayer(int amount) { int old = Player?.CurrentHp ?? 0; Player?.Heal(amount); Log($"玩家恢复{(Player?.CurrentHp ?? 0) - old}点生命，当前{Player?.CurrentHp}。"); }
    public void HealEnemy(int amount) { var e = EnemyManager.GetPrimaryEnemy(); if (e != null) { int old = e.CurrentHp; e.Heal(amount); Log($"{e.DisplayName}恢复{e.CurrentHp - old}点生命，当前{e.CurrentHp}。"); } }

    public void UseItem(int itemIndex)
    {
        var item = DataManager.Instance.DiscardItem(itemIndex);
        if (item == null) return;
        var effects = ((GodotObject)item).Get("effects").As<Array>();
        string name = ((GodotObject)item).Get("display_name").AsString();
        Log($"使用了{name}。");
        EffectResolver.ExecuteEffects(name, effects, this);
    }

    public void DiscardItem(int itemIndex) => DataManager.Instance.DiscardItem(itemIndex);

    // ================================================================
    //  内部
    // ================================================================

    private bool CheckBattleEnd()
    {
        if (!EnemyManager.HasAliveEnemies()) { turnTransitionLocked = true; SetPhase(Phase.BattleEnd); if (!battleEndEmitted) { battleEndEmitted = true; Log("战斗胜利。"); EmitSignal(SignalName.BattleEnded, true); } return true; }
        if (Player != null && !Player.IsAlive) { turnTransitionLocked = true; SetPhase(Phase.BattleEnd); if (!battleEndEmitted) { battleEndEmitted = true; Log("战斗失败。"); EmitSignal(SignalName.BattleEnded, false); } return true; }
        return false;
    }

    private int CalcDistance()
    {
        var battleMap = rules?.Get(GDScriptKeys.GameRules.BattleMap).As<GodotObject>();
        if (battleMap == null) return 0;
        int playerPos = Player?.MapPosition ?? 0;
        int enemyPos = EnemyManager?.GetPrimaryEnemy()?.MapPosition ?? 0;
        return battleMap.Call(GDScriptKeys.BattleMap.CombatDistance, playerPos, enemyPos).AsInt32();
    }

    private bool CanAcceptPlayerAction() => CurrentPhase == Phase.PlayerTurn && !turnTransitionLocked && BoardManager != null && rules != null && Player != null;
    private void SetPhase(Phase newPhase) { if (CurrentPhase == newPhase) return; CurrentPhase = newPhase; EmitSignal(SignalName.PhaseChanged, (int)newPhase); }
    private void Log(string text) => EmitSignal(SignalName.LogMessage, text);
}