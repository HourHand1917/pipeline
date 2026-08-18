using Godot;
using Godot.Collections;

[GlobalClass]
public partial class PlayerBattle : Node2D
{
    [Signal] public delegate void HealthChangedEventHandler(int current, int max);
    [Signal] public delegate void ShieldChangedEventHandler(int current);
    [Signal] public delegate void EnergyChangedEventHandler(int current, int max);
    [Signal] public delegate void PositionChangedEventHandler(int newPosition);
    [Signal] public delegate void BuffAppliedEventHandler(GodotObject buff, int stacks);
    [Signal] public delegate void BuffRemovedEventHandler(string buffId);
    [Signal] public delegate void DiedEventHandler();

    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int Shield { get; private set; }
    public int Energy { get; private set; }
    public int MaxEnergy { get; private set; }
    public int MapPosition { get; private set; }
    public int StrengthThisTurn { get; private set; }
    public int Facing { get; set; } = 0;

    public string DisplayName { get; private set; } = "玩家";
    public string Glyph { get; private set; } = "旅";
    public Color Tint { get; private set; } = new Color("#f4cf61");
    public string InstanceId { get; private set; } = "player";

    private GodotObject _stats;

    public void LoadFromData(GodotObject playerData, GodotObject rules)
    {
        if (playerData == null) return;

        MaxHp = playerData.Get(GDScriptKeys.CharacterData.MaxHp).AsInt32() + (GrowthManager.Instance?.GetMaxHpBonus() ?? 0);
        CurrentHp = MaxHp;
        Shield = playerData.Get(GDScriptKeys.CharacterData.InitialShield).AsInt32();
        MaxEnergy = (rules?.Get(GDScriptKeys.GameRules.EnergyPerTurn).AsInt32() ?? 3) + (GrowthManager.Instance?.GetEnergyBonus() ?? 0);
        Energy = MaxEnergy;
        StrengthThisTurn = 0;

        DisplayName = playerData.Get(GDScriptKeys.CharacterData.DisplayName).AsString();
        Glyph = playerData.Get(GDScriptKeys.CharacterData.Glyph).AsString();
        Tint = playerData.Get(GDScriptKeys.CharacterData.Tint).AsColor();
        InstanceId = playerData.Get(GDScriptKeys.CharacterData.Id).AsString();
        _stats = GD.Load<GDScript>("res://Script/GD/refcounted/stats.gd")?.New().As<GodotObject>();

        GD.Print($"[调试] LoadFromData 完成，_stats={_stats != null}");
        if (_stats != null)
        {
            var buffs = _stats.Get("buffs").As<Array>();
            GD.Print($"[调试] 初始 buffs.Count={buffs.Count}");
        }
    }

    public void LoadForNextWave(GodotObject playerData, GodotObject rules, bool preserveVitals)
    {
        if (!preserveVitals || MaxHp <= 0)
        {
            LoadFromData(playerData, rules);
            return;
        }

        int previousHp = CurrentHp;
        int previousShield = Shield;
        MaxHp = playerData.Get(GDScriptKeys.CharacterData.MaxHp).AsInt32() + (GrowthManager.Instance?.GetMaxHpBonus() ?? 0);
        CurrentHp = Mathf.Clamp(previousHp, 0, MaxHp);
        Shield = Mathf.Max(0, previousShield);
        MaxEnergy = (rules?.Get(GDScriptKeys.GameRules.EnergyPerTurn).AsInt32() ?? MaxEnergy) + (GrowthManager.Instance?.GetEnergyBonus() ?? 0);
        Energy = MaxEnergy;
        StrengthThisTurn = 0;
        DisplayName = playerData.Get(GDScriptKeys.CharacterData.DisplayName).AsString();
        Glyph = playerData.Get(GDScriptKeys.CharacterData.Glyph).AsString();
        Tint = playerData.Get(GDScriptKeys.CharacterData.Tint).AsColor();
        InstanceId = playerData.Get(GDScriptKeys.CharacterData.Id).AsString();
        EmitHealthChanged();
    }

    public void SetStats(GodotObject stats) => _stats = stats;
    public GodotObject GetStats() => _stats;

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

    public void SetCurrentHp(int hp)
    {
        CurrentHp = Mathf.Clamp(hp, 0, MaxHp);
        EmitSignal(SignalName.HealthChanged, CurrentHp, MaxHp);
    }

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

    public void ClearTurnModifiers() => StrengthThisTurn = 0;

    public void SetMapPosition(int position)
    {
        MapPosition = position;
        EmitSignal(SignalName.PositionChanged, MapPosition);
    }

    public void ApplyBuff(GodotObject buffResource, int stacks)
    {
        GD.Print($"[调试] PlayerBattle.ApplyBuff：{buffResource.Get("buff_name")} ×{stacks}");
        GD.Print($"[调试] _stats={_stats != null}");
        if (_stats != null)
        {
            var buffs = _stats.Get("buffs").As<Array>();
            GD.Print($"[调试] 信号发射前 buffs.Count={buffs.Count}");
        }
        EmitSignal(SignalName.BuffApplied, buffResource, stacks);
    }

    public void RemoveBuff(string buffId)
    {
        EmitSignal(SignalName.BuffRemoved, buffId);
    }

    public bool IsAlive => CurrentHp > 0;
}