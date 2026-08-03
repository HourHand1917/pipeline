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

    // GDScript 数据引用
    private GodotObject _enemyData;
    private GodotObject _stats;

    // ================================================================
    //  从 EnemyData 资源加载
    // ================================================================

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

    // ================================================================
    //  伤害 / 护盾 / 回复
    // ================================================================

    public void TakeDamage(int amount)
    {
        int shieldDmg = Mathf.Min(Shield, amount);
        Shield -= shieldDmg;
        int hpDmg = amount - shieldDmg;
        CurrentHp = Mathf.Max(0, CurrentHp - hpDmg);

        EmitSignal(SignalName.ShieldChanged, Shield);
        EmitSignal(SignalName.HealthChanged, CurrentHp, MaxHp);

        if (CurrentHp <= 0)
            EmitSignal(SignalName.Died);
    }

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
        Facing = (MapPosition - playerPos) > 0 ? 0 : 1;
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
}
