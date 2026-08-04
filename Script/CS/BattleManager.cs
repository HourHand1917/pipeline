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
        Player.EmitHealthChanged();

        Log($"战斗开始：{battleMap.Get(GDScriptKeys.BattleMap.CellCount).AsInt32()}格地图，{Player.DisplayName}{playerStartCell}，{primaryEnemy.DisplayName}{enemyStartCell}，距离{Distance}格；{Player.DisplayName}获得{Player.Energy}点能量。");
        EmitSignal(SignalName.BattleStateChanged);
    }

    public void ReturnToBuild() { turnTransitionLocked = false; usedCardIds.Clear(); SetPhase(Phase.Build); EmitSignal(SignalName.BattleStateChanged); }

    private void StartPlayerTurn()
    {
        if (!rules.Get(GDScriptKeys.GameRules.PreservePartialCharge).AsBool()) BoardManager.ClearAllLights();
        BoardManager.TickCooldowns(); BoardManager.TickCellBuffs();
        Player.ResetEnergy(); usedCardIds.Clear(); SetPhase(Phase.PlayerTurn);
        Log($"第{RoundNumber}回合开始：能量重置为{Player.Energy}，冷却-1。");
    }

    public void TryLightCell(Vector2I position) { /* 保持不变 */ }
    public void TryPlayCard(int instanceId) { /* 保持不变 */ }
    public bool CanPlayerMove(int action) { /* 保持不变 */ return false; }
    public void TryMove(int action) { /* 保持不变 */ }
    public void MoveCombatantTowardOpponent(int target, int amount) => MoveCombatantRelative(target, amount, true);
    public void MoveCombatantAwayFromOpponent(int target, int amount) => MoveCombatantRelative(target, amount, false);
    private void MoveCombatantRelative(int target, int amount, bool toward) { /* 保持不变 */ }
    public void SwapPosition() { /* 保持不变 */ }

    public async void EndTurn() { /* 保持不变 */ }
    private void ResolveEnemyTurn() { /* 保持不变 */ }

    public void DamageEnemy(int amount) { var e = EnemyManager.GetPrimaryEnemy(); if (e != null) { e.TakeDamage(amount); Log($"{e.DisplayName}受到{amount}点伤害。"); } }
    public void DamagePlayer(int amount) { Player?.TakeDamage(amount); Log($"玩家受到{amount}点伤害。"); }
    public void AddPlayerShield(int amount) { Player?.AddShield(amount); Log($"玩家获得{amount}点护盾。"); }
    public void AddEnemyShield(int amount) { var e = EnemyManager.GetPrimaryEnemy(); if (e != null) { e.AddShield(amount); } }
    public void AddPlayerEnergy(int amount) { Player?.AddEnergy(amount); }
    public void HealPlayer(int amount) { Player?.Heal(amount); }
    public void HealEnemy(int amount) { var e = EnemyManager.GetPrimaryEnemy(); if (e != null) e.Heal(amount); }

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

    private bool CheckBattleEnd() { /* 保持不变 */ return false; }
    private int CalcDistance() { /* 保持不变 */ return 0; }
    private bool CanAcceptPlayerAction() => CurrentPhase == Phase.PlayerTurn && !turnTransitionLocked && BoardManager != null && rules != null && Player != null;
    private void SetPhase(Phase newPhase) { if (CurrentPhase == newPhase) return; CurrentPhase = newPhase; EmitSignal(SignalName.PhaseChanged, (int)newPhase); }
    private void Log(string text) => EmitSignal(SignalName.LogMessage, text);
}