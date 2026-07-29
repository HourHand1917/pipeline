using Godot;
using Godot.Collections;

public partial class EffectResolver : Node
{
    [Signal] public delegate void EffectExecutedEventHandler(string sourceName, string effectType, int value);

    // ================================================================
    //  卡牌效果
    // ================================================================

    /// <summary>
    /// 执行卡牌效果。兼容旧版单效果和新版 effects 数组。
    /// </summary>
    public bool ExecuteCard(GodotObject card, BattleManager battleManager)
    {
        if (card == null || battleManager == null)
        {
            GD.PushWarning("卡牌效果执行失败：缺少卡牌或战斗管理器。");
            return false;
        }

        var data = card.Get("data").As<GodotObject>();
        if (data == null)
        {
            GD.PushWarning("卡牌效果执行失败：缺少 data。");
            return false;
        }

        string displayName = data.Get("display_name").AsString();

        // 优先使用新版 effects 数组
        var effects = data.Get("effects").As<Array>();
        if (effects != null && effects.Count > 0)
        {
            return ExecuteEffects(displayName, effects, battleManager);
        }

        // 回退到旧版单效果
        string effectType = data.Get("effect_type").AsString();
        int effectValue = data.Get("effect_value").AsInt32();

        if (string.IsNullOrEmpty(effectType))
        {
            GD.PushWarning($"{displayName}没有配置任何效果。");
            return false;
        }

        bool executed = ExecuteLegacyEffect(effectType, effectValue, battleManager);
        if (executed)
            EmitSignal(SignalName.EffectExecuted, displayName, effectType, effectValue);
        return executed;
    }

    // ================================================================
    //  敌人行动效果
    // ================================================================

    /// <summary>
    /// 执行敌人行动效果
    /// </summary>
    public bool ExecuteEnemyAction(GodotObject action, BattleManager battleManager)
    {
        if (action == null || battleManager == null)
        {
            GD.PushWarning("怪物行动执行失败：缺少行动或战斗管理器。");
            return false;
        }

        var effects = action.Get("effects").As<Array>();
        if (effects == null || effects.Count == 0)
        {
            string displayName = action.Get("display_name").AsString();
            GD.PushWarning($"{displayName}没有配置任何效果。");
            return false;
        }

        return ExecuteEffects(action.Get("display_name").AsString(), effects, battleManager);
    }

    // ================================================================
    //  内部实现
    // ================================================================

    /// <summary>
    /// 遍历执行 effects 数组
    /// </summary>
    private bool ExecuteEffects(string sourceName, Array effects, BattleManager battleManager)
    {
        bool executedAny = false;
        foreach (var effect in effects)
        {
            if (effect.Obj == null) continue;
            var eff = effect.As<GodotObject>();
            if (ExecuteEffect(eff, battleManager))
            {
                executedAny = true;
                EmitSignal(SignalName.EffectExecuted,
                    sourceName,
                    eff.Call("type_key").AsString(),
                    eff.Get("amount").AsInt32());
            }
        }
        return executedAny;
    }

    /// <summary>
    /// 执行单个 CombatEffectData
    /// </summary>
    private bool ExecuteEffect(GodotObject effect, BattleManager battleManager)
    {
        // type 是枚举，用 Call("type_key") 获取字符串
        string typeKey = effect.Call("type_key").AsString();
        int amount = effect.Get("amount").AsInt32();

        // target 也是枚举，0=PLAYER, 1=ENEMY
        int target = effect.Get("target").AsInt32();

        switch (typeKey)
        {
            case "damage":
                if (target == 0) // PLAYER
                    battleManager.DamagePlayer(amount);
                else
                    battleManager.DamageEnemy(amount);
                return true;

            case "shield":
                if (target == 0)
                    battleManager.AddPlayerShield(amount);
                else
                    battleManager.AddEnemyShield(amount);
                return true;

            case "heal":
                if (target == 0)
                    battleManager.HealPlayer(amount);
                else
                    battleManager.HealEnemy(amount);
                return true;

            case "move_toward_opponent":
                battleManager.MoveCombatantTowardOpponent(target, amount);
                return true;

            case "move_away_from_opponent":
                battleManager.MoveCombatantAwayFromOpponent(target, amount);
                return true;

            case "energy":
                if (target != 0)
                {
                    GD.PushWarning("当前原型只有玩家拥有能量，敌人能量效果已忽略。");
                    return false;
                }
                battleManager.AddPlayerEnergy(amount);
                return true;

            default:
                GD.PushWarning($"未知效果类型：{typeKey}");
                return false;
        }
    }

    /// <summary>
    /// 旧版单效果执行（向后兼容）
    /// </summary>
    private bool ExecuteLegacyEffect(string effectType, int effectValue, BattleManager battleManager)
    {
        switch (effectType)
        {
            case "damage":
                battleManager.DamageEnemy(effectValue);
                return true;
            case "shield":
                battleManager.AddPlayerShield(effectValue);
                return true;
            case "energy":
                battleManager.AddPlayerEnergy(effectValue);
                return true;
            case "heal":
                battleManager.HealPlayer(effectValue);
                return true;
            default:
                GD.PushWarning($"未知效果类型：{effectType}");
                return false;
        }
    }
}