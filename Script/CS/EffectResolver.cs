using Godot;
using Godot.Collections;

/// <summary>
/// Resolves data-only combat effects. Enemy effects always receive the explicit
/// acting enemy, so multi-enemy encounters never accidentally target slot zero.
/// </summary>
public partial class EffectResolver : Node
{
    [Signal] public delegate void EffectExecutedEventHandler(string sourceName, string effectType, int value);

    private BoardManager boardManager;

    public void SetBoardManager(BoardManager board) => boardManager = board;

    public bool ExecuteCard(GodotObject card, BattleManager battleManager)
    {
        if (card == null || battleManager == null) return false;
        var data = card.Get(GDScriptKeys.CardRuntime.Data).As<GodotObject>();
        if (data == null) return false;

        string displayName = data.Get(GDScriptKeys.CardData.DisplayName).AsString();
        var effects = data.Get(GDScriptKeys.CardData.Effects).As<Array>();
        if (effects != null && effects.Count > 0)
        {
            // Strength is one flat bonus to the card, not to every hit.
            int strength = data.Call(GDScriptKeys.CardData.HasDamageEffect).AsBool()
                ? battleManager.Player?.StrengthThisTurn ?? 0
                : 0;
            return ExecuteEffects(displayName, effects, battleManager, battleManager.GetCurrentTargetEnemy(), strength);
        }

        string type = data.Get(GDScriptKeys.CardData.EffectType).AsString();
        int value = data.Get(GDScriptKeys.CardData.EffectValue).AsInt32();
        int legacyValue = type == "damage" ? value + (battleManager.Player?.StrengthThisTurn ?? 0) : value;
        if (type == "heal" && (battleManager.Player == null || battleManager.Player.CurrentHp >= battleManager.Player.MaxHp))
            return false;
        bool executed = ExecuteLegacyEffect(type, legacyValue, battleManager);
        if (executed) EmitSignal(SignalName.EffectExecuted, displayName, type, legacyValue);
        return executed;
    }

    public bool ExecuteEnemyAction(GodotObject action, BattleManager battleManager)
        => ExecuteEnemyAction(action, battleManager, battleManager?.ActingEnemy);

    public bool ExecuteEnemyAction(GodotObject action, BattleManager battleManager, EnemyBattle actor)
    {
        if (action == null || battleManager == null || actor == null) return false;

        string id = action.Get(GDScriptKeys.EnemyAction.Id).AsString();
        string displayName = action.Get(GDScriptKeys.EnemyAction.DisplayName).AsString();
        string special = action.Get(GDScriptKeys.EnemyAction.SpecialEffect).AsString();
        StringName targetRole = action.Get(GDScriptKeys.EnemyAction.EffectTargetRole).AsStringName();
        int specialValue = action.Get(GDScriptKeys.EnemyAction.SpecialValue).AsInt32();
        bool replacesEffects = action.Get(GDScriptKeys.EnemyAction.SpecialReplacesEffects).AsBool();
        var effects = action.Get(GDScriptKeys.EnemyAction.Effects).As<Array>();

        // Core's beams have a board pattern rather than a normal range check.
        bool evenBeam = id == "core_true_guard_beam" || id == "core_false_break_beam";
        bool patternedHit = !evenBeam || battleManager.IsPlayerOnEvenCell();
        bool specialExecuted = ExecuteEnemySpecial(
            special, specialValue, targetRole, id, patternedHit, battleManager, actor);
        // Pattern miss is still a successfully resolved telegraphed action: its
        // non-damage effects (for example True hand's guard) may still execute.
        if (special == "clear_true_death_loop" && !patternedHit)
            specialExecuted = true;

        bool effectsExecuted = false;
        if (!replacesEffects && effects != null && effects.Count > 0)
        {
            effectsExecuted = ExecuteEffects(
                displayName,
                effects,
                battleManager,
                actor,
                0,
                skipMovement: special == "move_behind_player",
                skipDamage: evenBeam && !patternedHit);
        }

        return specialExecuted || effectsExecuted;
    }

    public bool ExecuteEffects(string sourceName, Array effects, BattleManager battleManager)
        => ExecuteEffects(sourceName, effects, battleManager, null, 0, false, false, false);

    private bool ExecuteEffects(
        string sourceName,
        Array effects,
        BattleManager battleManager,
        EnemyBattle actor,
        int firstDamageBonus = 0,
        bool skipMovement = false,
        bool skipDamage = false,
        bool requireStateChange = false)
    {
        if (effects == null) return false;
        bool any = false;
        bool bonusConsumed = false;
        foreach (Variant effect in effects)
        {
            if (effect.Obj == null) continue;
            var data = effect.As<GodotObject>();
            string type = data.Call(GDScriptKeys.CombatEffect.TypeKey).AsString();
            if (skipMovement && (type == "move_toward_opponent" || type == "move_away_from_opponent")) continue;
            if (skipDamage && type == "damage") continue;

            int bonus = type == "damage" && !bonusConsumed ? firstDamageBonus : 0;
            if (!ExecuteEffect(data, battleManager, actor, bonus, requireStateChange)) continue;
            any = true;
            if (type == "damage") bonusConsumed = true;
            EmitSignal(SignalName.EffectExecuted,
                sourceName, type, data.Get(GDScriptKeys.CombatEffect.Amount).AsInt32() + bonus);
        }
        return any;
    }

    private bool ExecuteEffect(GodotObject effect, BattleManager battleManager, EnemyBattle actor, int damageBonus, bool requireStateChange)
    {
        string type = effect.Call(GDScriptKeys.CombatEffect.TypeKey).AsString();
        int amount = effect.Get(GDScriptKeys.CombatEffect.Amount).AsInt32();
        int target = effect.Get(GDScriptKeys.CombatEffect.Target).AsInt32();
        switch (type)
        {
            case "damage":
                if (target == 0) battleManager.DamagePlayer(amount + damageBonus);
                else battleManager.DamageEnemy(amount + damageBonus, actor);
                return true;
            case "shield":
                if (target == 0) battleManager.AddPlayerShield(amount);
                else battleManager.AddEnemyShield(amount, actor);
                return true;
            case "heal":
                if (target == 0)
                {
                    if (battleManager.Player == null || battleManager.Player.CurrentHp >= battleManager.Player.MaxHp) return false;
                    battleManager.HealPlayer(amount);
                    return true;
                }
                if (actor == null || !actor.IsAlive || actor.CurrentHp >= actor.MaxHp) return false;
                battleManager.HealEnemy(amount, actor);
                return true;
            case "move_toward_opponent":
                if (target == 0) battleManager.MoveCombatantTowardOpponent(target, amount);
                else if (actor != null) battleManager.MoveEnemyToward(actor, amount);
                else battleManager.MoveCombatantTowardOpponent(target, amount);
                return true;
            case "move_away_from_opponent":
                if (target == 0) battleManager.MoveCombatantAwayFromOpponent(target, amount);
                else if (actor != null) battleManager.MoveEnemyAway(actor, amount);
                else battleManager.MoveCombatantAwayFromOpponent(target, amount);
                return true;
            case "swap_position":
                battleManager.SwapPosition();
                return true;
            case "energy":
                if (target != 0) return false;
                if (requireStateChange && amount <= 0) return false;
                battleManager.AddPlayerEnergy(amount);
                return true;
            case "apply_buff":
                return ApplyBuff(effect, battleManager, actor);
            case "remove_buff":
                return RemoveBuff(effect, target, actor);
            default:
                GD.PushWarning($"Unknown combat effect type: {type}");
                return false;
        }
    }

    private bool ExecuteEnemySpecial(
        string special,
        int value,
        StringName targetRole,
        string actionId,
        bool patternedHit,
        BattleManager battleManager,
        EnemyBattle actor)
    {
        switch (special)
        {
            case "": return false;
            case "wait": return true;
            case "push_player_to_edge": return battleManager.ExecuteChargePush(actor, value > 0 ? value : 10);
            case "move_behind_player": return battleManager.MoveEnemyBehindPlayer(actor);
            case "add_all_card_cooldown":
                battleManager.AddAllCardCooldown(Mathf.Max(1, value));
                return true;
            case "heal_specific_enemy": return battleManager.HealEnemyByRole(targetRole, value);
            case "set_death_loop":
                battleManager.SetTrueDeathLoop(true);
                return true;
            case "clear_true_death_loop":
                if (!patternedHit) return false;
                battleManager.SetTrueDeathLoop(false);
                return true;
            default:
                GD.PushWarning($"Unknown enemy special effect: {special} ({actionId})");
                return false;
        }
    }

    private bool ExecuteLegacyEffect(string type, int value, BattleManager battleManager)
    {
        switch (type)
        {
            case "damage": battleManager.DamageEnemy(value); return true;
            case "shield": battleManager.AddPlayerShield(value); return true;
            case "energy": battleManager.AddPlayerEnergy(value); return true;
            case "heal": battleManager.HealPlayer(value); return true;
            default: return false;
        }
    }

   private bool ApplyBuff(GodotObject effect, BattleManager battleManager, EnemyBattle actor)
{
    var buff = effect.Get(GDScriptKeys.CombatEffect.Buff).As<GodotObject>();
    if (buff == null) return false;
    int stacks = effect.Get(GDScriptKeys.CombatEffect.BuffStacks).AsInt32();
    int target = effect.Get(GDScriptKeys.CombatEffect.BuffTarget).AsInt32();
    Vector2I cell = effect.Get(GDScriptKeys.CombatEffect.BuffTargetCell).AsVector2I();

    GD.Print($"[调试] ApplyBuff target={target}, buff={buff.Get("buff_name")}, stacks={stacks}");

    switch (target)
    {
        case 0:
        {
            var stats = battleManager.Player?.GetStats();
            GD.Print($"[调试] case 0: stats={stats != null}, hash={stats?.GetHashCode()}");
            if (stats != null)
            {
                var buffsBefore = stats.Get("buffs").As<Array>();
                GD.Print($"[调试] 施加前 buffs.Count={buffsBefore.Count}");
                stats.Call(GDScriptKeys.Stats.AddBuff, buff, stacks);
                var buffsAfter = stats.Get("buffs").As<Array>();
                GD.Print($"[调试] 施加后 buffs.Count={buffsAfter.Count}");
            }
            battleManager.Player?.ApplyBuff(buff, stacks);
            break;
        }
        case 1: actor?.GetStats()?.Call(GDScriptKeys.Stats.AddBuff, buff, stacks); break;
        case 2: ApplyBuffToPlayerCells(buff, stacks, cell); break;
        default: return false;
    }
    return true;
}

    private bool RemoveBuff(GodotObject effect, int target, EnemyBattle actor)
    {
        var buff = effect.Get(GDScriptKeys.CombatEffect.Buff).As<GodotObject>();
        string id = buff?.Get(GDScriptKeys.Buff.Id).AsString() ?? "";
        if (string.IsNullOrEmpty(id)) return false;
        if (target == 0) RemoveBuffFromPlayerCells(id);
        else actor?.GetStats()?.Call(GDScriptKeys.Stats.RemoveBuff, id);
        return true;
    }

    private void ApplyBuffToPlayerCells(GodotObject buff, int stacks, Vector2I targetCell)
    {
        if (boardManager == null) return;
        if (targetCell.X >= 0 && targetCell.Y >= 0)
        {
            boardManager.GetCell(targetCell)?.Get(GDScriptKeys.CellRuntime.Stats).As<GodotObject>()
                ?.Call(GDScriptKeys.Stats.AddBuff, buff, stacks);
            return;
        }
        foreach (var runtime in boardManager.runtime_cards)
        {
            foreach (Vector2I pos in runtime.Get(GDScriptKeys.CardRuntime.OccupiedCells).As<Array<Vector2I>>())
                boardManager.GetCell(pos)?.Get(GDScriptKeys.CellRuntime.Stats).As<GodotObject>()
                    ?.Call(GDScriptKeys.Stats.AddBuff, buff, stacks);
        }
    }

    private void RemoveBuffFromPlayerCells(string id)
    {
        if (boardManager == null) return;
        foreach (var runtime in boardManager.runtime_cards)
        {
            foreach (Vector2I pos in runtime.Get(GDScriptKeys.CardRuntime.OccupiedCells).As<Array<Vector2I>>())
                boardManager.GetCell(pos)?.Get(GDScriptKeys.CellRuntime.Stats).As<GodotObject>()
                    ?.Call(GDScriptKeys.Stats.RemoveBuff, id);
        }
    }
}
