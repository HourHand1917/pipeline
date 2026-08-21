using Godot;
using Godot.Collections;

/// <summary>
/// 敌人战斗实例。
/// 加载 EnemyData 资源 → 持有运行时 stats → 通过信号通知 UI。
/// 以独立场景形式挂载到 EnemyManager 下。
/// </summary>
[GlobalClass]
public partial class EnemyBattle : Node2D
{
    // ============ 信号（UI 层订阅） ============
    [Signal] public delegate void HealthChangedEventHandler(int current, int max);
    [Signal] public delegate void ShieldChangedEventHandler(int current);
    [Signal] public delegate void PositionChangedEventHandler(int newPosition);
    [Signal] public delegate void BuffAppliedEventHandler(GodotObject buff, int stacks);
    [Signal] public delegate void BuffRemovedEventHandler(string buffId);
    [Signal] public delegate void IntentChangedEventHandler(GodotObject action);
    [Signal] public delegate void DiedEventHandler();

    // ============ 运行时 Stats ============
    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int Shield { get; private set; }
    public int MapPosition { get; private set; }
    /// <summary>朝向：0=正方向（右），1=负方向（左）</summary>
    public int Facing { get; set; } = 0;

    /// <summary>显示名（从 EnemyData 读取）</summary>
    public string DisplayName { get; private set; } = "敌人";
    /// <summary>显示符号（用于距离轨道标记）</summary>
    public string Glyph { get; private set; } = "怪";
    /// <summary>标识色</summary>
    public Color Tint { get; private set; } = new Color("#e66c62");
    /// <summary>敌人 ID</summary>
    public string EnemyId { get; private set; } = "enemy";
    /// <summary>AI role, for example rocky, true_hand, false_hand or body.</summary>
    public StringName Role { get; private set; } = new StringName();
    /// <summary>Fixed enemies participate in combat but ignore movement effects.</summary>
    public bool FixedPosition { get; private set; }
    /// <summary>The intent selected during PlayerTurn and executed unchanged in EnemyTurn.</summary>
    public GodotObject PlannedAction { get; private set; }
    public int DamageTakenThisPlayerTurn { get; private set; }
    public EnemyAnimator Animator { get; private set; }

    // GDScript 数据引用
    private GodotObject _enemyData;
    private GodotObject _stats;

    // ================================================================
    //  从 EnemyData 资源加载
    // ================================================================

    public override void _Ready()
    {
        Animator = GetNodeOrNull<EnemyAnimator>("EnemyAnimator");
    }
    public void LoadFromData(GodotObject enemyData)
    {
        if (enemyData == null) return;
        _enemyData = enemyData;

        MaxHp = enemyData.Get(GDScriptKeys.CharacterData.MaxHp).AsInt32();
        CurrentHp = MaxHp;
        Shield = enemyData.Get(GDScriptKeys.CharacterData.InitialShield).AsInt32();

        DisplayName = enemyData.Get(GDScriptKeys.CharacterData.DisplayName).AsString();
        Glyph = enemyData.Get(GDScriptKeys.CharacterData.Glyph).AsString();
        Tint = enemyData.Get(GDScriptKeys.CharacterData.Tint).AsColor();
        EnemyId = enemyData.Get(GDScriptKeys.CharacterData.Id).AsString();

        Role = HasProperty(enemyData, "role")
            ? enemyData.Get("role").AsStringName()
            : new StringName(EnemyId);
        FixedPosition = HasProperty(enemyData, "fixed_position")
            && enemyData.Get("fixed_position").AsBool();

        if (enemyData.HasMethod("clear_ai_runtime_context"))
            enemyData.Call("clear_ai_runtime_context");
        if (enemyData.HasMethod("reset_ai_provider"))
            enemyData.Call("reset_ai_provider");
        SetPlannedAction(null);
        DamageTakenThisPlayerTurn = 0;
        _stats = GD.Load<GDScript>("res://Script/GD/refcounted/stats.gd")?.New().As<GodotObject>();
    }

    // ================================================================
    //  Stats 引用（供 EffectResolver 读写）
    // ================================================================

    public void SetStats(GodotObject stats)
    {
        _stats = stats;
    }

    public GodotObject GetStats()
    {
        return _stats;
    }

    /// <summary>获取原始 EnemyData 资源（含 actions 配置）</summary>
    public GodotObject GetEnemyData()
    {
        return _enemyData;
    }

    public void SetPlannedAction(GodotObject action)
    {
        if (PlannedAction == action) return;
        PlannedAction = action;
        EmitSignal(SignalName.IntentChanged, action);
    }

    public void ClearPlannedAction() => SetPlannedAction(null);

    // ================================================================
    //  伤害 / 护盾 / 回复
    // ================================================================

    public void TakeDamage(int amount, int minimumHp = 0)
{
    if (amount <= 0 || !IsAlive) return;
    int shieldDmg = Mathf.Min(Shield, amount);
    Shield -= shieldDmg;
    int hpDmg = amount - shieldDmg;
    CurrentHp = Mathf.Max(minimumHp, CurrentHp - hpDmg);
    DamageTakenThisPlayerTurn += amount;

    EmitSignal(SignalName.ShieldChanged, Shield);
    EmitSignal(SignalName.HealthChanged, CurrentHp, MaxHp);

    if (CurrentHp <= 0)
    {
        Animator?.PlayDeath();  // ← 加这行
        EmitSignal(SignalName.Died);
    }
    else
    {
        Animator?.PlayHurt();   // ← 移到这里
    }
}

    public void ResetTurnDamage() => DamageTakenThisPlayerTurn = 0;

    public void AddShield(int amount)
    {
        Shield += amount;
        EmitSignal(SignalName.ShieldChanged, Shield);
    }

    public void Heal(int amount)
    {
        CurrentHp = Mathf.Min(MaxHp, CurrentHp + amount);
        EmitSignal(SignalName.HealthChanged, CurrentHp, MaxHp);
    }

    // ================================================================
    //  位置
    // ================================================================

    public void SetMapPosition(int position)
    {
        MapPosition = position;
        EmitSignal(SignalName.PositionChanged, MapPosition);
    }

    /// <summary>根据玩家位置自动计算朝向</summary>
    public void UpdateFacing(int playerPos)
    {
        Facing = (playerPos - MapPosition) > 0 ? 0 : 1;
    }

    // ================================================================
    //  Buff
    // ================================================================

    public void ApplyBuff(GodotObject buffResource, int stacks)
    {
        EmitSignal(SignalName.BuffApplied, buffResource, stacks);
    }

    public void RemoveBuff(string buffId)
    {
        EmitSignal(SignalName.BuffRemoved, buffId);
    }

    // ================================================================
    //  查询
    // ================================================================

    public bool IsAlive => CurrentHp > 0;

    private static bool HasProperty(GodotObject obj, StringName property)
    {
        if (obj == null) return false;
        foreach (var descriptor in obj.GetPropertyList())
            if (descriptor["name"].AsStringName() == property)
                return true;
        return false;
    }
}
