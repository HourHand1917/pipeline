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
    [Signal] public delegate void HealthChangedEventHandler(int current, int max);
    [Signal] public delegate void ShieldChangedEventHandler(int current);
    [Signal] public delegate void PositionChangedEventHandler(int newPosition);
    [Signal] public delegate void BuffAppliedEventHandler(GodotObject buff, int stacks);
    [Signal] public delegate void BuffRemovedEventHandler(string buffId);
    [Signal] public delegate void IntentChangedEventHandler(GodotObject action);
    [Signal] public delegate void DiedEventHandler();

    private const string ENEMY_ATTACK_SFX = "res://tileset/music_resource/OGG/SFX/战斗反馈/小怪/COM_Enemy_Attack.ogg";
    private const string ENEMY_DEATH_SFX = "res://tileset/music_resource/OGG/SFX/战斗反馈/小怪/COM_Enemy_Death.ogg";
    private const string ENEMY_HIT_SFX = "res://tileset/music_resource/OGG/SFX/战斗反馈/小怪/COM_Enemy_Hit.ogg";

    private AudioStream _attackSfx;
    private AudioStream _deathSfx;
    private AudioStream _hitSfx;

    public int MaxHp { get; private set; }
    public int CurrentHp { get; private set; }
    public int Shield { get; private set; }
    public int MapPosition { get; private set; }
    public int Facing { get; set; } = 0;

    public string DisplayName { get; private set; } = "敌人";
    public string Glyph { get; private set; } = "怪";
    public Color Tint { get; private set; } = new Color("#e66c62");
    public string EnemyId { get; private set; } = "enemy";
    public StringName Role { get; private set; } = new StringName();
    public bool FixedPosition { get; private set; }
    public GodotObject PlannedAction { get; private set; }
    public int DamageTakenThisPlayerTurn { get; private set; }
    public EnemyAnimator Animator { get; private set; }

    private GodotObject _enemyData;
    private GodotObject _stats;

    public override void _Ready()
    {
        Animator = GetNodeOrNull<EnemyAnimator>("EnemyAnimator");

        _attackSfx = GD.Load<AudioStream>(ENEMY_ATTACK_SFX);
        _deathSfx = GD.Load<AudioStream>(ENEMY_DEATH_SFX);
        _hitSfx = GD.Load<AudioStream>(ENEMY_HIT_SFX);
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

    public void SetStats(GodotObject stats) => _stats = stats;
    public GodotObject GetStats() => _stats;
    public GodotObject GetEnemyData() => _enemyData;

    public void SetPlannedAction(GodotObject action)
    {
        if (PlannedAction == action) return;
        PlannedAction = action;
        EmitSignal(SignalName.IntentChanged, action);
    }

    public void ClearPlannedAction() => SetPlannedAction(null);

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
            PlaySfx(_deathSfx);
            Animator?.PlayDeath();
            EmitSignal(SignalName.Died);
        }
        else
        {
            PlaySfx(_hitSfx);
            Animator?.PlayHurt();
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

    public void SetMapPosition(int position)
    {
        MapPosition = position;
        EmitSignal(SignalName.PositionChanged, MapPosition);
    }

    public void UpdateFacing(int playerPos)
    {
        Facing = (playerPos - MapPosition) > 0 ? 0 : 1;
    }

    public void PlayAttackSfx() => PlaySfx(_attackSfx);

    private void PlaySfx(AudioStream sfx)
    {
        if (sfx == null) return;
        GetNodeOrNull<AudioManager>("/root/AudioManager")?.PlaySfx(sfx);
    }

    public void ApplyBuff(GodotObject buffResource, int stacks)
    {
        EmitSignal(SignalName.BuffApplied, buffResource, stacks);
    }

    public void RemoveBuff(string buffId)
    {
        EmitSignal(SignalName.BuffRemoved, buffId);
    }

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