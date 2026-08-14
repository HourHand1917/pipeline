using Godot;
using Godot.Collections;

/// <summary>
/// 玩家战斗实例。
/// 加载 PlayerData 资源 → 持有运行时 stats → 通过信号通知 UI。
/// 以独立场景形式挂载到战斗场景中。
/// </summary>
[GlobalClass]
public partial class PlayerBattle : Node2D
{
    // ============ 信号（UI 层订阅） ============
    [Signal] public delegate void HealthChangedEventHandler(int current, int max);
    [Signal] public delegate void ShieldChangedEventHandler(int current);
    [Signal] public delegate void EnergyChangedEventHandler(int current, int max);
    [Signal] public delegate void PositionChangedEventHandler(int newPosition);
    [Signal] public delegate void BuffAppliedEventHandler(GodotObject buff, int stacks);
    [Signal] public delegate void BuffRemovedEventHandler(string buffId);
    [Signal] public delegate void DiedEventHandler();

    // ============ 运行时 Stats ============
    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int Shield { get; private set; }
    public int Energy { get; private set; }
    public int MaxEnergy { get; private set; }
    public int MapPosition { get; private set; }
    /// <summary>
    /// Flat bonus added once to the total damage of each attack card played in
    /// the current player turn.  It is intentionally not added per hit.
    /// </summary>
    public int StrengthThisTurn { get; private set; }
    /// <summary>朝向：0=正方向（右），1=负方向（左）</summary>
    public int Facing { get; set; } = 0;

    /// <summary>角色显示名（从 PlayerData 读取）</summary>
    public string DisplayName { get; private set; } = "玩家";
    /// <summary>显示符号（用于距离轨道标记）</summary>
    public string Glyph { get; private set; } = "旅";
    /// <summary>标识色</summary>
    public Color Tint { get; private set; } = new Color("#f4cf61");
    /// <summary>实例 ID</summary>
    public string InstanceId { get; private set; } = "player";

    // GDScript 侧的 Buff 数组（由 effectResolver 写入）
    private GodotObject _stats;

    // ================================================================
    //  从 PlayerData 资源加载
    // ================================================================

    public void LoadFromData(GodotObject playerData, GodotObject rules)
    {
        if (playerData == null) return;

        MaxHp = playerData.Get(GDScriptKeys.CharacterData.MaxHp).AsInt32();
        CurrentHp = MaxHp;
        Shield = playerData.Get(GDScriptKeys.CharacterData.InitialShield).AsInt32();
        MaxEnergy = rules?.Get(GDScriptKeys.GameRules.EnergyPerTurn).AsInt32() ?? 3;
        Energy = MaxEnergy;
        StrengthThisTurn = 0;

        DisplayName = playerData.Get(GDScriptKeys.CharacterData.DisplayName).AsString();
        Glyph = playerData.Get(GDScriptKeys.CharacterData.Glyph).AsString();
        Tint = playerData.Get(GDScriptKeys.CharacterData.Tint).AsColor();
        InstanceId = playerData.Get(GDScriptKeys.CharacterData.Id).AsString();
        _stats = GD.Load<GDScript>("res://Script/GD/refcounted/stats.gd")?.New().As<GodotObject>();
    }

    /// <summary>
    /// Reconfigures turn rules for a new wave without granting a free heal.
    /// Used only for the Core-00 hands -> body transition.
    /// </summary>
    public void LoadForNextWave(GodotObject playerData, GodotObject rules, bool preserveVitals)
    {
        if (!preserveVitals || MaxHp <= 0)
        {
            LoadFromData(playerData, rules);
            return;
        }

        int previousHp = CurrentHp;
        int previousShield = Shield;
        MaxHp = playerData.Get(GDScriptKeys.CharacterData.MaxHp).AsInt32();
        CurrentHp = Mathf.Clamp(previousHp, 0, MaxHp);
        Shield = Mathf.Max(0, previousShield);
        MaxEnergy = rules?.Get(GDScriptKeys.GameRules.EnergyPerTurn).AsInt32() ?? MaxEnergy;
        Energy = MaxEnergy;
        StrengthThisTurn = 0;
        DisplayName = playerData.Get(GDScriptKeys.CharacterData.DisplayName).AsString();
        Glyph = playerData.Get(GDScriptKeys.CharacterData.Glyph).AsString();
        Tint = playerData.Get(GDScriptKeys.CharacterData.Tint).AsColor();
        InstanceId = playerData.Get(GDScriptKeys.CharacterData.Id).AsString();
        EmitHealthChanged();
    }

    // ================================================================
    //  Stats 引用（供 EffectResolver 的 apply_buff 写入）
    // ================================================================

    public void SetStats(GodotObject stats)
    {
        _stats = stats;
    }

    public GodotObject GetStats()
    {
        return _stats;
    }

    // ================================================================
    //  伤害 / 护盾 / 回复
    // ================================================================

    /// <summary>主动触发一次 HealthChanged 信号，用于 UI 初始化</summary>
    public void EmitHealthChanged()
    {
        EmitSignal(SignalName.HealthChanged, CurrentHp, MaxHp);
        EmitSignal(SignalName.ShieldChanged, Shield);
        EmitSignal(SignalName.EnergyChanged, Energy, MaxEnergy);
    }
    public void TakeDamage(int amount)
    {
        if (amount <= 0)
        {
            // 回血
            CurrentHp = Mathf.Min(MaxHp, CurrentHp - amount);
            EmitSignal(SignalName.HealthChanged, CurrentHp, MaxHp);
            return;
        }

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

    /// <summary>从外部（DataManager）同步血量，用于探索/战斗间的继承。</summary>
    public void SetCurrentHp(int hp)
    {
        CurrentHp = Mathf.Clamp(hp, 0, MaxHp);
        EmitSignal(SignalName.HealthChanged, CurrentHp, MaxHp);
    }

    // ================================================================
    //  能量
    // ================================================================

    public bool SpendEnergy(int amount)
    {
        if (Energy < amount) return false;
        Energy -= amount;
        EmitSignal(SignalName.EnergyChanged, Energy, MaxEnergy);
        return true;
    }

    public void AddEnergy(int amount)
    {
        Energy += amount;
        EmitSignal(SignalName.EnergyChanged, Energy, MaxEnergy);
    }

    /// <summary>回合开始：能量回满</summary>
    public void ResetEnergy()
    {
        Energy = MaxEnergy;
        EmitSignal(SignalName.EnergyChanged, Energy, MaxEnergy);
    }

    public bool AddStrengthThisTurn(int amount)
    {
        if (amount <= 0) return false;
        StrengthThisTurn += amount;
        return true;
    }

    public void ClearTurnModifiers()
    {
        StrengthThisTurn = 0;
    }

    // ================================================================
    //  位置
    // ================================================================

    public void SetMapPosition(int position)
    {
        MapPosition = position;
        EmitSignal(SignalName.PositionChanged, MapPosition);
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
