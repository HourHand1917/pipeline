using Godot;
using Godot.Collections;

/// <summary>
/// Buff/item extension for EffectResolver.  The four user-supplied base files
/// stay untouched; this partial adds the data-driven operations required by
/// the approved design sheet.
/// </summary>
public partial class EffectResolver
{
    private static readonly StringName BuffsProperty = "buffs";
    private static readonly StringName BuffProperty = "buff";
    private static readonly StringName StacksProperty = "stacks";
    private static readonly StringName PendingApplicationsMethod = "drain_pending_applications";
    private static readonly StringName GetEffectsMethod = "get_effects_for_phase";
    private static readonly StringName ConsumeAfterApplyMethod = "consume_after_apply";
    private static readonly StringName ConsumeAfterPhaseMethod = "consume_after_phase";
    private static readonly StringName PipelineOperationProperty = "pipeline_operation";

    private enum PipelineOperation
    {
        Standard,
        AddDamageByBuffStacks,
        HalfDamageFloor,
        AddHalfDamageCeil,
        SetIncomingDamageToOne,
        ResetAllCardCooldowns,
        SetAllCardCooldown,
        DrainPlayerEnergy,
        HealOwnerByBuffStacks,
        MovePlayerForwardOne,
        DamageFarthestEnemy,
    }

    /// <summary>Runs ItemData.effects, including PipelineCombatEffectData.</summary>
    public bool ExecuteItemEffects(string sourceName, Array effects, BattleManager battleManager)
    {
        if (effects == null || battleManager == null) return false;
        bool any = false;
        foreach (Variant entry in effects)
        {
            var effect = entry.As<GodotObject>();
            if (effect == null) continue;
            bool executed = ExecuteConfiguredEffect(effect, battleManager, null, 0, true);
            if (!executed) continue;
            any = true;
            EmitSignal(SignalName.EffectExecuted,
                sourceName,
                effect.Call(GDScriptKeys.CombatEffect.TypeKey).AsString(),
                effect.Get(GDScriptKeys.CombatEffect.Amount).AsInt32());
        }
        ResolvePendingBuffApplications(battleManager);
        return any;
    }

    /// <summary>
    /// Resolves data-mounted Buff effects that are conditional on a patterned
    /// board hit. The normal enemy action resolver still owns damage/shield;
    /// this extension only runs the separate conditional effect array.
    /// </summary>
    public bool ExecuteEnemyPatternHitEffects(
        GodotObject action,
        BattleManager battleManager,
        EnemyBattle actor)
    {
        if (action == null || battleManager == null || actor == null)
            return false;

        if (!BattleManager.EnemyActionUsesFixedTargets(action)
            || !battleManager.IsPlayerTargetedByAction(action))
            return false;

        var effects = action.Get(GDScriptKeys.EnemyAction.PatternHitEffects).As<Array>();
        string displayName = action.Get(GDScriptKeys.EnemyAction.DisplayName).AsString();
        bool any = effects != null && effects.Count > 0
            && ExecuteConfiguredEffects(displayName, effects, battleManager, actor);

        var roleEffects = action.Get(GDScriptKeys.EnemyAction.PatternHitRoleEffects).As<Array>();
        if (roleEffects != null && roleEffects.Count > 0)
        {
            StringName role = action.Get(GDScriptKeys.EnemyAction.EffectTargetRole).AsStringName();
            EnemyBattle target = battleManager.EnemyManager?.GetAliveByRole(role);
            if (target != null)
                any |= ExecuteConfiguredEffects(displayName, roleEffects, battleManager, target);
        }
        ResolvePendingBuffApplications(battleManager);
        return any;
    }

    public bool HasDeferredEnemyRoleEffects(GodotObject action)
    {
        if (action == null) return false;
        var effects = action.Get(GDScriptKeys.EnemyAction.DeferredRoleEffects).As<Array>();
        return effects != null && effects.Count > 0;
    }

    /// <summary>
    /// Resolves cross-enemy effects after all enemies have acted. This lets
    /// True hand deploy a package on False hand without healing the primary
    /// enemy or consuming it during False hand's current turn.
    /// </summary>
    public bool ExecuteDeferredEnemyRoleEffects(GodotObject action, BattleManager battleManager)
    {
        if (action == null || battleManager?.EnemyManager == null) return false;
        var effects = action.Get(GDScriptKeys.EnemyAction.DeferredRoleEffects).As<Array>();
        if (effects == null || effects.Count == 0) return false;

        StringName role = action.Get(GDScriptKeys.EnemyAction.EffectTargetRole).AsStringName();
        EnemyBattle target = battleManager.EnemyManager.GetAliveByRole(role);
        if (target == null) return false;
        bool any = ExecuteConfiguredEffects(
            action.Get(GDScriptKeys.EnemyAction.DisplayName).AsString(),
            effects,
            battleManager,
            target);
        ResolvePendingBuffApplications(battleManager);
        return any;
    }

    /// <summary>Executes effects queued by Buff.on_apply through this resolver.</summary>
    public void ResolvePendingBuffApplications(BattleManager battleManager)
    {
        if (battleManager == null) return;
        ResolvePendingForStats(battleManager.Player?.GetStats(), battleManager, null);
        if (battleManager.EnemyManager == null) return;
        foreach (var enemy in battleManager.EnemyManager.Enemies)
            ResolvePendingForStats(enemy?.GetStats(), battleManager, enemy);
    }

    /// <summary>Runs one character's start/end trigger effects and lifecycle.</summary>
    public void TickActorBuffs(PlayerBattle player, BattleManager battleManager, bool atStart)
        => TickStats(player?.GetStats(), battleManager, null, atStart);

    public void TickActorBuffs(EnemyBattle enemy, BattleManager battleManager, bool atStart)
        => TickStats(enemy?.GetStats(), battleManager, enemy, atStart);

    public int ResolveDamageWithBuffs(int baseAmount, GodotObject sourceStats, GodotObject targetStats)
    {
        int amount = Mathf.Max(0, baseAmount);
        if (amount == 0) return 0;

        int outgoingAdd = SumOperationStacks(sourceStats, "outgoing_damage", PipelineOperation.AddDamageByBuffStacks);
        amount += outgoingAdd;
        amount = ApplyDamageScaleOperations(amount, sourceStats, "outgoing_damage");
        amount = ApplyDamageScaleOperations(amount, targetStats, "incoming_damage");

        if (HasOperation(targetStats, "incoming_damage", PipelineOperation.SetIncomingDamageToOne))
            amount = 1;
        return Mathf.Max(0, amount);
    }

    public bool CanLightCell(GodotObject stats, GodotObject cell)
    {
        if (stats == null || !stats.HasMethod("can_light")) return true;
        return stats.Call("can_light", cell).AsBool();
    }

    public int GetLightExtraCost(GodotObject stats, GodotObject cell)
    {
        if (stats == null || !stats.HasMethod("before_light")) return 0;
        return Mathf.Max(0, stats.Call("before_light", cell).AsInt32());
    }

    public void NotifyCellLit(GodotObject stats, GodotObject cell)
    {
        if (stats != null && stats.HasMethod("after_light"))
            stats.Call("after_light", cell);
    }

    public void ClearActorBuffs(GodotObject stats)
    {
        if (stats != null && stats.HasMethod(GDScriptKeys.Stats.ClearBuffs))
            stats.Call(GDScriptKeys.Stats.ClearBuffs);
    }

    public int ClearNegativeActorBuffs(GodotObject stats)
    {
        if (stats == null || !stats.HasMethod("clear_negative_buffs")) return 0;
        return stats.Call("clear_negative_buffs").AsInt32();
    }

    private void ResolvePendingForStats(
        GodotObject stats,
        BattleManager battleManager,
        EnemyBattle actor)
    {
        if (stats == null || !stats.HasMethod(PendingApplicationsMethod)) return;
        var pending = stats.Call(PendingApplicationsMethod).As<Array>();
        if (pending == null) return;
        foreach (Variant entry in pending)
        {
            var application = entry.AsGodotDictionary();
            var buff = application.TryGetValue("buff", out Variant buffValue)
                ? buffValue.As<GodotObject>()
                : null;
            int stacks = application.TryGetValue("stacks", out Variant stackValue)
                ? stackValue.AsInt32()
                : 0;
            if (buff == null || stacks <= 0) continue;

            // Mutual-cancellation Buffs may remove themselves during on_apply.
            string id = buff.Get(GDScriptKeys.Buff.Id).AsString();
            if (!stats.Call(GDScriptKeys.Stats.HasBuff, id).AsBool()) continue;
            ExecuteBuffEffects(buff, "apply", stacks, battleManager, actor);
            if (buff.HasMethod(ConsumeAfterApplyMethod)
                && buff.Call(ConsumeAfterApplyMethod).AsBool())
                stats.Call(GDScriptKeys.Stats.RemoveBuff, id);
        }
    }

    private void TickStats(
        GodotObject stats,
        BattleManager battleManager,
        EnemyBattle actor,
        bool atStart)
    {
        if (stats == null || battleManager == null) return;
        string phase = atStart ? "turn_start" : "turn_end";
        foreach (Variant entry in GetBuffInstances(stats))
        {
            var instance = entry.As<GodotObject>();
            var buff = instance?.Get(BuffProperty).As<GodotObject>();
            if (buff == null) continue;
            int stacks = instance.Get(StacksProperty).AsInt32();
            ExecuteBuffEffects(buff, phase, stacks, battleManager, actor);
            if (buff.HasMethod(ConsumeAfterPhaseMethod)
                && buff.Call(ConsumeAfterPhaseMethod, new StringName(phase)).AsBool())
                stats.Call(GDScriptKeys.Stats.RemoveBuff, buff.Get(GDScriptKeys.Buff.Id).AsString());
        }

        stats.Call(atStart ? GDScriptKeys.Stats.TickTurnStart : GDScriptKeys.Stats.TickTurnEnd);
        ResolvePendingForStats(stats, battleManager, actor);
    }

    private bool ExecuteBuffEffects(
        GodotObject buff,
        string phase,
        int stacks,
        BattleManager battleManager,
        EnemyBattle actor)
    {
        if (buff == null || !buff.HasMethod(GetEffectsMethod)) return false;
        var effects = buff.Call(GetEffectsMethod, new StringName(phase)).As<Array>();
        if (effects == null) return false;
        bool any = false;
        foreach (Variant entry in effects)
        {
            var effect = entry.As<GodotObject>();
            if (effect == null) continue;
            any |= ExecuteConfiguredEffect(effect, battleManager, actor, stacks, false);
        }
        return any;
    }

    private bool ExecuteConfiguredEffect(
        GodotObject effect,
        BattleManager battleManager,
        EnemyBattle actor,
        int sourceBuffStacks,
        bool requireStateChange)
    {
        PipelineOperation operation = GetPipelineOperation(effect);
        int amount = effect.Get(GDScriptKeys.CombatEffect.Amount).AsInt32();
        switch (operation)
        {
            case PipelineOperation.Standard:
                return ExecuteEffect(effect, battleManager, actor, 0, requireStateChange);
            case PipelineOperation.ResetAllCardCooldowns:
                return boardManager?.ResetAllCooldowns() ?? false;
            case PipelineOperation.SetAllCardCooldown:
                return battleManager.SetAllCardCooldownAtLeast(Mathf.Max(1, amount));
            case PipelineOperation.DrainPlayerEnergy:
                return battleManager.DrainPlayerEnergy(Mathf.Max(1, amount));
            case PipelineOperation.HealOwnerByBuffStacks:
                return battleManager.HealBuffOwner(actor, Mathf.Max(0, sourceBuffStacks));
            case PipelineOperation.MovePlayerForwardOne:
                return battleManager.MovePlayerForwardFreeOne();
            case PipelineOperation.DamageFarthestEnemy:
                return battleManager.DamageFarthestEnemy(Mathf.Max(0, amount));
            // Modifier-only operations are evaluated inside ResolveDamageWithBuffs.
            case PipelineOperation.AddDamageByBuffStacks:
            case PipelineOperation.HalfDamageFloor:
            case PipelineOperation.AddHalfDamageCeil:
            case PipelineOperation.SetIncomingDamageToOne:
                return true;
            default:
                return false;
        }
    }

    private bool ExecuteConfiguredEffects(
        string sourceName,
        Array effects,
        BattleManager battleManager,
        EnemyBattle actor)
    {
        bool any = false;
        foreach (Variant entry in effects)
        {
            var effect = entry.As<GodotObject>();
            if (effect == null || !ExecuteConfiguredEffect(effect, battleManager, actor, 0, false))
                continue;
            any = true;
            EmitSignal(SignalName.EffectExecuted,
                sourceName,
                effect.Call(GDScriptKeys.CombatEffect.TypeKey).AsString(),
                effect.Get(GDScriptKeys.CombatEffect.Amount).AsInt32());
        }
        return any;
    }

    private static int ApplyDamageScaleOperations(int amount, GodotObject stats, string phase)
    {
        if (HasOperation(stats, phase, PipelineOperation.HalfDamageFloor))
            amount = Mathf.FloorToInt(amount / 2.0f);
        if (HasOperation(stats, phase, PipelineOperation.AddHalfDamageCeil))
            amount += Mathf.CeilToInt(amount / 2.0f);
        return amount;
    }

    private static int SumOperationStacks(
        GodotObject stats,
        string phase,
        PipelineOperation operation)
    {
        int result = 0;
        foreach (Variant entry in GetBuffInstances(stats))
        {
            var instance = entry.As<GodotObject>();
            var buff = instance?.Get(BuffProperty).As<GodotObject>();
            if (buff == null || !BuffHasOperation(buff, phase, operation)) continue;
            result += Mathf.Max(0, instance.Get(StacksProperty).AsInt32());
        }
        return result;
    }

    private static bool HasOperation(
        GodotObject stats,
        string phase,
        PipelineOperation operation)
    {
        foreach (Variant entry in GetBuffInstances(stats))
        {
            var instance = entry.As<GodotObject>();
            var buff = instance?.Get(BuffProperty).As<GodotObject>();
            if (buff != null && BuffHasOperation(buff, phase, operation)) return true;
        }
        return false;
    }

    private static bool BuffHasOperation(
        GodotObject buff,
        string phase,
        PipelineOperation operation)
    {
        if (buff == null || !buff.HasMethod(GetEffectsMethod)) return false;
        var effects = buff.Call(GetEffectsMethod, new StringName(phase)).As<Array>();
        if (effects == null) return false;
        foreach (Variant entry in effects)
        {
            var effect = entry.As<GodotObject>();
            if (GetPipelineOperation(effect) == operation) return true;
        }
        return false;
    }

    private static Array GetBuffInstances(GodotObject stats)
        => stats?.Get(BuffsProperty).As<Array>() ?? new Array();

    private static PipelineOperation GetPipelineOperation(GodotObject effect)
    {
        if (effect == null || !HasProperty(effect, PipelineOperationProperty))
            return PipelineOperation.Standard;
        return (PipelineOperation)effect.Get(PipelineOperationProperty).AsInt32();
    }

    private static bool HasProperty(GodotObject obj, StringName property)
    {
        if (obj == null) return false;
        foreach (var descriptor in obj.GetPropertyList())
            if (descriptor["name"].AsStringName() == property)
                return true;
        return false;
    }
}
