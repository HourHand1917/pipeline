using Godot;
using Godot.Collections;

public partial class BattleManager : Node
{
    [Signal] public delegate void BattleStateChangedEventHandler();
    [Signal] public delegate void PhaseChangedEventHandler(int newPhase);
    [Signal] public delegate void LogMessageEventHandler(string text);
    [Signal] public delegate void BattleEndedEventHandler(bool playerWon);

    public enum Phase
    {
        Build,
        PlayerTurn,
        EnemyTurn,
        BattleEnd,
    }

    public enum MoveAction
    {
        Backward = -1,
        Forward = 1,
    }

    [Export] private Resource _gameRules;

    public Phase CurrentPhase { get; private set; } = Phase.Build;
    public int RoundNumber { get; private set; } = 0;

    public int PlayerHp { get; private set; }
    public int PlayerMaxHp { get; private set; }
    public int PlayerEnergy { get; private set; }
    public int PlayerShield { get; private set; }

    public int EnemyHp { get; private set; }
    public int EnemyMaxHp { get; private set; }
    public int EnemyShield { get; private set; }

    public int PlayerMapPosition { get; private set; }
    public int EnemyMapPosition { get; private set; }
    public int Distance { get; private set; }

    private BoardManager boardManager;
    private EffectResolver effectResolver;
    private GodotObject rules;
    private Dictionary<int, bool> usedCardIds = new();
    private bool turnTransitionLocked;
    private bool battleEndEmitted;

    // GDScript 兼容属性
    public Phase phase => CurrentPhase;
    public int round_number => RoundNumber;
    public int player_hp => PlayerHp;
    public int player_max_hp => PlayerMaxHp;
    public int player_energy => PlayerEnergy;
    public int player_shield => PlayerShield;
    public int enemy_hp => EnemyHp;
    public int enemy_max_hp => EnemyMaxHp;
    public int enemy_shield => EnemyShield;
    public int player_map_position => PlayerMapPosition;
    public int enemy_map_position => EnemyMapPosition;
    public int distance => Distance;

    // ================================================================
    //  Setup
    // ================================================================

    public void Setup(BoardManager board, EffectResolver resolver)
    {
        boardManager = board;
        effectResolver = resolver;
    }

    // ================================================================
    //  战斗开始 / 结束
    // ================================================================

    public void StartBattle()
    {
        if (_gameRules == null)
        {
            Log("战斗无法开始：请先在检查器中配置 GameRules 资源。");
            return;
        }
        rules = _gameRules as GodotObject;
        if (rules == null)
        {
            Log("战斗无法开始：规则资源无效。");
            return;
        }

        turnTransitionLocked = false;
        battleEndEmitted = false;
        usedCardIds.Clear();
        RoundNumber = 1;

        var playerData = rules.Get("player_data").As<GodotObject>();
        var enemyData = rules.Get("enemy_data").As<GodotObject>();
        var battleMap = rules.Get("battle_map").As<GodotObject>();

        if (playerData == null || enemyData == null || battleMap == null)
        {
            Log("战斗无法开始：PlayerData、EnemyData 或 BattleMapData 未配置。");
            return;
        }

        PlayerMaxHp = playerData.Get("max_hp").AsInt32();
        PlayerHp = PlayerMaxHp;
        PlayerEnergy = rules.Get("energy_per_turn").AsInt32();
        PlayerShield = playerData.Get("initial_shield").AsInt32();

        EnemyMaxHp = enemyData.Get("max_hp").AsInt32();
        EnemyHp = EnemyMaxHp;
        EnemyShield = enemyData.Get("initial_shield").AsInt32();

        PlayerMapPosition = battleMap.Get("player_start_cell").AsInt32();
        EnemyMapPosition = battleMap.Get("enemy_start_cell").AsInt32();
        UpdateDistance();

        foreach (var runtime in boardManager.runtime_cards)
        {
            runtime.Set("cooldown_remaining", 0);
            runtime.Set("is_ready", false);
        }

        boardManager.ClearAllLights();
        SetPhase(Phase.PlayerTurn);
        Log($"战斗开始：{battleMap.Get("cell_count").AsInt32()}格地图，玩家{PlayerMapPosition}，怪物{EnemyMapPosition}，距离{Distance}格；玩家获得{PlayerEnergy}点能量。");
        EmitSignal(SignalName.BattleStateChanged);
    }

    public void ReturnToBuild()
    {
        turnTransitionLocked = false;
        usedCardIds.Clear();
        SetPhase(Phase.Build);
        EmitSignal(SignalName.BattleStateChanged);
    }

    // ================================================================
    //  点亮格子
    // ================================================================

        public void TryLightCell(Vector2I position)
    {
        if (!CanAcceptPlayerAction())
        {
            Log("当前阶段不能点亮格子。");
            return;
        }

        var cell = boardManager.GetCell(position);
        if (cell == null) return;

        var runtime = boardManager.GetCardByCell(position);
        if (runtime == null)
        {
            Log("这个格子中没有物品。");
            return;
        }

        if (cell.Get("is_lit").AsBool())
        {
            Log("这个格子已经点亮。");
            return;
        }

        if (runtime.Get("cooldown_remaining").AsInt32() > 0)
        {
            var data = runtime.Get("data").As<GodotObject>();
            Log($"{data.Get("display_name")}仍在冷却：{runtime.Get("cooldown_remaining")}回合。");
            return;
        }

        // ============ 失效检查 ============
        var stats = cell.Get("stats").As<GodotObject>();
        if (stats != null)
        {
            GD.Print($"[调试] TryLightCell stats.GetHashCode={stats.GetHashCode()}");
            GD.Print($"[调试] TryLightCell stats.buffs数量={stats.Get("buffs").As<Array>().Count}");

            if (stats.Call("has_buff", "disabled").AsBool())
            {
                Log("该格子已失效，无法点亮。");
                return;
            }
        }

        // ============ 蒙尘额外消耗 ============
        int chargeCost = rules.Get("charge_energy_cost").AsInt32();
        int dustExtra = 0;
        if (stats != null)
        {
            int st = stats.Call("get_buff_stacks", "dust").AsInt32();
            GD.Print($"[调试] TryLightCell get_buff_stacks(dust)={st}");
            dustExtra = st;
        }
        int totalCost = chargeCost + dustExtra;

        GD.Print($"[调试] TryLightCell ({position.X},{position.Y})：基础={chargeCost}, 蒙尘+{dustExtra}, 总消耗={totalCost}");

        if (PlayerEnergy < totalCost)
        {
            string dustMsg = dustExtra > 0 ? $"（蒙尘额外消耗+{dustExtra}）" : "";
            Log($"能量不足，点亮需要{totalCost}点能量{dustMsg}。");
            return;
        }

        var data2 = runtime.Get("data").As<GodotObject>();
        if (data2.Get("once_per_turn").AsBool() && usedCardIds.ContainsKey(runtime.Get("instance_id").AsInt32()))
        {
            Log($"{data2.Get("display_name")}本回合已经发动过。");
            return;
        }

        PlayerEnergy -= totalCost;
        boardManager.SetCellLit(position, true);

        string dustMsg2 = dustExtra > 0 ? $"（蒙尘额外消耗{dustExtra}）" : "";
        Log($"点亮{data2.Get("display_name")}的一格，消耗{totalCost}点能量{dustMsg2}。");
        EmitSignal(SignalName.BattleStateChanged);
    }
    // ================================================================
    //  发动卡牌
    // ================================================================

    public void TryPlayCard(int instanceId)
    {
        if (!CanAcceptPlayerAction())
        {
            Log("当前阶段不能发动装备。");
            return;
        }

        var runtime = boardManager.GetRuntimeCard(instanceId);
        if (runtime == null) return;

        var data = runtime.Get("data").As<GodotObject>();

        if (data.Get("once_per_turn").AsBool() && usedCardIds.ContainsKey(instanceId))
        {
            Log($"{data.Get("display_name")}本回合已经发动过。");
            return;
        }

        if (!boardManager.CheckCardReady(runtime))
        {
            Log($"{data.Get("display_name")}尚未全部点亮。");
            return;
        }

        if (data.Call("has_damage_effect").AsBool())
        {
            int minRange = data.Get("min_range").AsInt32();
            int maxRange = data.Get("max_range").AsInt32();
            if (Distance < minRange || Distance > maxRange)
            {
                Log($"{data.Get("display_name")}射程不足：距离{Distance}格，需要{minRange}–{maxRange}格。");
                return;
            }
        }

        turnTransitionLocked = true;
        bool executed = effectResolver.ExecuteCard(runtime, this);
        if (!executed)
        {
            turnTransitionLocked = false;
            Log($"{data.Get("display_name")}效果执行失败。");
            return;
        }

        if (data.Get("once_per_turn").AsBool())
            usedCardIds[instanceId] = true;

        int cooldownTurns = data.Get("cooldown_turns").AsInt32();
        runtime.Set("cooldown_remaining", cooldownTurns);
        boardManager.ClearCardLights(runtime);

        Log($"{data.Get("display_name")}发动，进入{cooldownTurns}回合冷却。");

        if (!CheckBattleEnd())
            turnTransitionLocked = false;

        EmitSignal(SignalName.BattleStateChanged);
    }

    // ================================================================
    //  移动
    // ================================================================

    public bool CanPlayerMove(int action)
    {
        if (CurrentPhase != Phase.PlayerTurn || turnTransitionLocked || rules == null)
            return false;
        if (action != (int)MoveAction.Backward && action != (int)MoveAction.Forward)
            return false;

        int moveCost = rules.Get("move_energy_cost").AsInt32();
        if (PlayerEnergy < moveCost) return false;

        var battleMap = rules.Get("battle_map").As<GodotObject>();
        int direction = battleMap.Get("player_forward_direction").AsInt32();
        if (action == (int)MoveAction.Backward) direction *= -1;

        int stepCount = rules.Get("player_move_step").AsInt32();
        for (int i = 1; i <= stepCount; i++)
        {
            int candidate = PlayerMapPosition + direction * i;
            if (!battleMap.Call("is_valid_cell", candidate).AsBool()) return false;
            if (candidate == EnemyMapPosition) return false;
        }
        return true;
    }

    public void TryMove(int action)
    {
        if (!CanAcceptPlayerAction())
        {
            Log("当前阶段不能移动。");
            return;
        }
        if (!CanPlayerMove(action))
        {
            Log("该方向已到边界或被阻挡。");
            return;
        }

        int moveCost = rules.Get("move_energy_cost").AsInt32();
        var battleMap = rules.Get("battle_map").As<GodotObject>();
        int direction = battleMap.Get("player_forward_direction").AsInt32();
        if (action == (int)MoveAction.Backward) direction *= -1;

        int oldPos = PlayerMapPosition;
        int stepCount = rules.Get("player_move_step").AsInt32();
        PlayerMapPosition += direction * stepCount;
        PlayerEnergy -= moveCost;
        UpdateDistance();

        Log($"{(action == (int)MoveAction.Forward ? "前进" : "后退")}：{oldPos} → {PlayerMapPosition}，距离{Distance}格，消耗{moveCost}能量。");
        EmitSignal(SignalName.BattleStateChanged);
    }

    public void MoveCombatantTowardOpponent(int target, int amount)
    {
        MoveCombatantRelative(target, amount, true);
    }

    public void MoveCombatantAwayFromOpponent(int target, int amount)
    {
        MoveCombatantRelative(target, amount, false);
    }

    private void MoveCombatantRelative(int target, int amount, bool toward)
    {
        if (rules == null || amount <= 0) return;

        var battleMap = rules.Get("battle_map").As<GodotObject>();
        int actorPos = target == 0 ? PlayerMapPosition : EnemyMapPosition;
        int opponentPos = target == 0 ? EnemyMapPosition : PlayerMapPosition;
        int dir = Mathf.Sign(opponentPos - actorPos);
        if (!toward) dir *= -1;
        if (dir == 0) return;

        int oldPos = actorPos;
        for (int i = 0; i < amount; i++)
        {
            int candidate = actorPos + dir;
            if (!battleMap.Call("is_valid_cell", candidate).AsBool()) break;
            if (candidate == opponentPos) break;
            actorPos = candidate;
        }

        if (target == 0) PlayerMapPosition = actorPos;
        else EnemyMapPosition = actorPos;
        UpdateDistance();

        int moved = Mathf.Abs(actorPos - oldPos);
        if (moved > 0)
        {
            string name = target == 0 ? "玩家" : "怪物";
            Log($"{name}地图上{(toward ? "接近" : "远离")}对手{moved}格：{oldPos} → {actorPos}；距离{Distance}格。");
        }
    }

    // ================================================================
    //  回合结束
    // ================================================================

    public async void EndTurn()
    {
        if (CurrentPhase != Phase.PlayerTurn || turnTransitionLocked) return;

        turnTransitionLocked = true;
        SetPhase(Phase.EnemyTurn);

        bool preserve = rules.Get("preserve_partial_charge_between_turns").AsBool();
        if (preserve)
            Log("玩家结束回合；未完成的点亮格会保留。");
        else
        {
            boardManager.ClearAllLights();
            Log("玩家结束回合；未完成的点亮格已清除。");
        }
        EmitSignal(SignalName.BattleStateChanged);

        await ToSignal(GetTree().CreateTimer(0.35f), "timeout");
        if (CurrentPhase == Phase.BattleEnd) { turnTransitionLocked = false; return; }

        ResolveEnemyTurn();
        EmitSignal(SignalName.BattleStateChanged);
        if (CheckBattleEnd()) { turnTransitionLocked = false; return; }

        await ToSignal(GetTree().CreateTimer(0.35f), "timeout");
        StartNextRound();
        turnTransitionLocked = false;
    }

private void ResolveEnemyTurn()
{
    var enemyData = rules.Get("enemy_data").As<GodotObject>();
    if (enemyData == null)
    {
        Log("调试：enemyData 为 null");
        return;
    }

    var actions = enemyData.Get("actions").As<Array>();
    Log($"调试：敌人共 {actions.Count} 个行动，当前距离 {Distance}");

    for (int i = 0; i < actions.Count; i++)
    {
        var act = actions[i].As<GodotObject>();
        if (act == null)
        {
            Log($"调试：行动[{i}] 为 null");
            continue;
        }
        int minR = act.Get("min_range").AsInt32();
        int maxR = act.Get("max_range").AsInt32();
        bool available = act.Call("is_available", Distance).AsBool();
        Log($"调试：行动[{i}] {act.Get("display_name")} min={minR} max={maxR} available={available}");
    }

    var action = enemyData.Call("get_action_for_distance", Distance).As<GodotObject>();
    if (action == null)
    {
        Log("敌人没有可用行动，原地等待。");
        return;
    }
    Log($"敌人使用「{action.Get("display_name")}」。");
    effectResolver.ExecuteEnemyAction(action, this);
}

    private void StartNextRound()
    {
        RoundNumber++;
        PlayerEnergy = rules.Get("energy_per_turn").AsInt32();
        usedCardIds.Clear();
        boardManager.TickCooldowns();
        boardManager.TickCellBuffs();  // ← 新增
        SetPhase(Phase.PlayerTurn);
        Log($"第{RoundNumber}回合开始：能量重置为{PlayerEnergy}，冷却-1。");
        EmitSignal(SignalName.BattleStateChanged);
    }

    // ================================================================
    //  伤害 / 回复
    // ================================================================

    public void DamageEnemy(int amount)
    {
        int shieldDmg = Mathf.Min(EnemyShield, amount);
        EnemyShield -= shieldDmg;
        int hpDmg = amount - shieldDmg;
        EnemyHp = Mathf.Max(0, EnemyHp - hpDmg);
        Log($"敌人护盾吸收{shieldDmg}点，受到{hpDmg}点伤害，剩余{EnemyHp}生命。");
    }

    public void DamagePlayer(int amount)
    {
        int shieldDmg = Mathf.Min(PlayerShield, amount);
        PlayerShield -= shieldDmg;
        int hpDmg = amount - shieldDmg;
        PlayerHp = Mathf.Max(0, PlayerHp - hpDmg);
        Log($"玩家护盾吸收{shieldDmg}点，受到{hpDmg}点伤害。");
    }

    public void AddPlayerShield(int amount)
    {
        PlayerShield += amount;
        Log($"玩家获得{amount}点护盾，当前{PlayerShield}。");
    }

    public void AddEnemyShield(int amount)
    {
        EnemyShield += amount;
        Log($"敌人获得{amount}点护盾，当前{EnemyShield}。");
    }

    public void AddPlayerEnergy(int amount)
    {
        PlayerEnergy += amount;
        Log($"玩家获得{amount}点能量，当前{PlayerEnergy}。");
    }

    public void HealPlayer(int amount)
    {
        int old = PlayerHp;
        PlayerHp = Mathf.Min(PlayerMaxHp, PlayerHp + amount);
        Log($"玩家恢复{PlayerHp - old}点生命，当前{PlayerHp}。");
    }

    public void HealEnemy(int amount)
    {
        int old = EnemyHp;
        EnemyHp = Mathf.Min(EnemyMaxHp, EnemyHp + amount);
        Log($"敌人恢复{EnemyHp - old}点生命，当前{EnemyHp}。");
    }

    // ================================================================
    //  内部
    // ================================================================

    private bool CheckBattleEnd()
    {
        if (EnemyHp <= 0)
        {
            turnTransitionLocked = true;
            SetPhase(Phase.BattleEnd);
            if (!battleEndEmitted) { battleEndEmitted = true; Log("战斗胜利。"); EmitSignal(SignalName.BattleEnded, true); }
            return true;
        }
        if (PlayerHp <= 0)
        {
            turnTransitionLocked = true;
            SetPhase(Phase.BattleEnd);
            if (!battleEndEmitted) { battleEndEmitted = true; Log("战斗失败。"); EmitSignal(SignalName.BattleEnded, false); }
            return true;
        }
        return false;
    }

    private void UpdateDistance()
    {
        var battleMap = rules?.Get("battle_map").As<GodotObject>();
        if (battleMap == null) { Distance = 0; return; }
        Distance = battleMap.Call("combat_distance", PlayerMapPosition, EnemyMapPosition).AsInt32();
    }

    private bool CanAcceptPlayerAction()
    {
        return CurrentPhase == Phase.PlayerTurn && !turnTransitionLocked && boardManager != null && rules != null;
    }

    private void SetPhase(Phase newPhase)
    {
        if (CurrentPhase == newPhase) return;
        CurrentPhase = newPhase;
        EmitSignal(SignalName.PhaseChanged, (int)newPhase);
    }

    private void Log(string text)
    {
        EmitSignal(SignalName.LogMessage, text);
    }
}