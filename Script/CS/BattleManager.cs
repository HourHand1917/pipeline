using Godot;

public partial class BattleManager : Node
{
    [Signal] public delegate void BattleStateChangedEventHandler();
    [Signal] public delegate void PhaseChangedEventHandler(int newPhase);
    [Signal] public delegate void LogMessageEventHandler(string text);
    [Signal] public delegate void BattleEndedEventHandler(bool playerWon);

    public enum Phase
    {
        Build,
        PlayerLighting,
        PlayerResolving,
        EnemyTurn,
        BattleEnd
    }

    // 兼容 GDScript 的 Phase 枚举访问方式
    public static class PhaseKeys
    {
        public static string[] Keys()
        {
            return new string[] { "Build", "PlayerLighting", "PlayerResolving", "EnemyTurn", "BattleEnd" };
        }
    }

    public Phase CurrentPhase { get; private set; } = Phase.Build;
    public int RoundNumber { get; private set; } = 1;

    public int PlayerHp { get; private set; } = 12;
    public int PlayerMaxHp { get; private set; } = 12;
    public int PlayerEnergy { get; private set; } = 4;
    public int PlayerShield { get; private set; } = 0;

    public int EnemyHp { get; private set; } = 15;
    public int EnemyMaxHp { get; private set; } = 15;
    public int EnemyShield { get; private set; } = 0;

    private BoardManager boardManager;
    private EffectResolver effectResolver;

    // 兼容 GDScript 的属性名小写访问
    public Phase phase => CurrentPhase;
    public int round_number => RoundNumber;
    public int player_hp => PlayerHp;
    public int player_max_hp => PlayerMaxHp;
    public int player_energy => PlayerEnergy;
    public int player_shield => PlayerShield;
    public int enemy_hp => EnemyHp;
    public int enemy_max_hp => EnemyMaxHp;
    public int enemy_shield => EnemyShield;

    /// <summary>
    /// 初始化 BattleManager
    /// </summary>
    public void Setup(BoardManager board, EffectResolver resolver)
    {
        boardManager = board;
        effectResolver = resolver;
    }

    /// <summary>
    /// 开始战斗
    /// </summary>
    public void StartBattle()
    {
        RoundNumber = 1;

        PlayerHp = 12;
        PlayerMaxHp = 12;
        PlayerEnergy = 4;
        PlayerShield = 0;

        EnemyHp = 15;
        EnemyMaxHp = 15;
        EnemyShield = 0;

        foreach (var runtime in boardManager.runtime_cards)
        {
            runtime.Set("cooldown_remaining", 0);
            runtime.Set("is_ready", false);
        }

        boardManager.ClearAllLights();
        SetPhase(Phase.PlayerLighting);
        Log("战斗开始：玩家获得4点能量。");
        EmitSignal(SignalName.BattleStateChanged);
    }

    /// <summary>
    /// 尝试点亮格子
    /// </summary>
    public void TryLightCell(Vector2I position)
    {
        if (CurrentPhase != Phase.PlayerLighting)
        {
            Log("当前阶段不能点亮格子。");
            return;
        }

        var cell = boardManager.GetCell(position);
        if (cell == null)
            return;

        var runtime = boardManager.GetCardByCell(position);
        if (runtime == null)
        {
            Log("这个格子中没有物品。");
            return;
        }

        if ((bool)cell.Get("is_lit"))
        {
            Log("这个格子已经点亮。");
            return;
        }

        if ((int)runtime.Get("cooldown_remaining") > 0)
        {
            var data = runtime.Get("data").As<GodotObject>();
            string displayName = (string)data.Get("display_name");
            Log($"{displayName}仍在冷却：{(int)runtime.Get("cooldown_remaining")}回合。");
            return;
        }

        if (PlayerEnergy <= 0)
        {
            Log("能量不足。");
            return;
        }

        PlayerEnergy -= 1;
        boardManager.SetCellLit(position, true);
        Log($"点亮格子{position}，消耗1能量。");
        EmitSignal(SignalName.BattleStateChanged);
        return;
    }

    /// <summary>
    /// 尝试发动物品
    /// </summary>
    public void TryPlayCard(int instanceId)
    {
        if (CurrentPhase != Phase.PlayerLighting)
            return;

        var runtime = boardManager.GetRuntimeCard(instanceId);
        if (runtime == null)
            return;

        // 二次验证，不信任 UI 按钮状态
        if (!boardManager.CheckCardReady(runtime))
        {
            Log("发动条件已经变化，无法发动物品。");
            return;
        }

        SetPhase(Phase.PlayerResolving);
        effectResolver.Execute(runtime, this);

        var data = runtime.Get("data").As<GodotObject>();
        int cooldownTurns = (int)data.Get("cooldown_turns");
        string displayName = (string)data.Get("display_name");

        runtime.Set("cooldown_remaining", cooldownTurns);
        boardManager.ClearCardLights(runtime);

        Log($"{displayName}发动，进入{cooldownTurns}回合冷却。");

        if (!CheckBattleEnd())
            SetPhase(Phase.PlayerLighting);

        EmitSignal(SignalName.BattleStateChanged);
        return;
    }

    /// <summary>
    /// 结束回合
    /// </summary>
    public async void EndTurn()
    {
        if (CurrentPhase != Phase.PlayerLighting)
            return;

        //boardManager.ClearAllLights();(清空按钮充能状态的代码，我目前先注释掉，看看会不会有什么bug，有的话再重构)
        SetPhase(Phase.EnemyTurn);
        Log("玩家结束回合，保留点亮过的按钮");
        EmitSignal(SignalName.BattleStateChanged);

        await ToSignal(GetTree().CreateTimer(0.35f), "timeout");
        if (CurrentPhase == Phase.BattleEnd)
            return;

        // 假人行动：获得护盾 + 攻击
        EnemyShield += 1;
        Log("训练假人获得1点护盾。");
        DamagePlayer(1);
        EmitSignal(SignalName.BattleStateChanged);

        if (CheckBattleEnd())
            return;

        await ToSignal(GetTree().CreateTimer(0.35f), "timeout");
        StartNextRound();
    }

    /// <summary>
    /// 开始下一回合
    /// </summary>
    private void StartNextRound()
    {
        RoundNumber += 1;
        PlayerEnergy = 4;
        boardManager.TickCooldowns();

        SetPhase(Phase.PlayerLighting);
        Log($"第{RoundNumber}回合开始：能量恢复为4，所有冷却-1。");
        EmitSignal(SignalName.BattleStateChanged);
    }

    /// <summary>
    /// 对敌人造成伤害
    /// </summary>
    public void DamageEnemy(int amount)
    {
        int shieldDamage = Mathf.Min(EnemyShield, amount);
        EnemyShield -= shieldDamage;

        int hpDamage = amount - shieldDamage;
        EnemyHp = Mathf.Max(0, EnemyHp - hpDamage);

        Log($"敌人受到{hpDamage}点生命伤害，吸收{shieldDamage}点护盾伤害。");
    }

    /// <summary>
    /// 对玩家造成伤害
    /// </summary>
    public void DamagePlayer(int amount)
    {
        int shieldDamage = Mathf.Min(PlayerShield, amount);
        PlayerShield -= shieldDamage;

        int hpDamage = amount - shieldDamage;
        PlayerHp = Mathf.Max(0, PlayerHp - hpDamage);

        Log($"玩家受到{hpDamage}点生命伤害，吸收{shieldDamage}点护盾伤害。");
    }

    /// <summary>
    /// 玩家获得护盾
    /// </summary>
    public void AddPlayerShield(int amount)
    {
        PlayerShield += amount;
        Log($"玩家获得{amount}点护盾。");
    }

    /// <summary>
    /// 玩家获得能量
    /// </summary>
    public void AddPlayerEnergy(int amount)
    {
        PlayerEnergy += amount;
        Log($"玩家获得{amount}点能量。");
    }

    /// <summary>
    /// 玩家恢复生命
    /// </summary>
    public void HealPlayer(int amount)
    {
        int oldHp = PlayerHp;
        PlayerHp = Mathf.Min(PlayerMaxHp, PlayerHp + amount);
        Log($"玩家恢复{PlayerHp - oldHp}点生命。");
    }

    /// <summary>
    /// 检查战斗是否结束
    /// </summary>
    private bool CheckBattleEnd()
    {
        if (EnemyHp <= 0)
        {
            SetPhase(Phase.BattleEnd);
            Log("战斗胜利。");
            EmitSignal(SignalName.BattleEnded, true);
            return true;
        }

        if (PlayerHp <= 0)
        {
            SetPhase(Phase.BattleEnd);
            Log("战斗失败。");
            EmitSignal(SignalName.BattleEnded, false);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 设置当前阶段
    /// </summary>
    private void SetPhase(Phase newPhase)
    {
        CurrentPhase = newPhase;
        EmitSignal(SignalName.PhaseChanged, (int)newPhase);
    }

    /// <summary>
    /// 记录日志
    /// </summary>
    private void Log(string text)
    {
        EmitSignal(SignalName.LogMessage, text);
    }
}