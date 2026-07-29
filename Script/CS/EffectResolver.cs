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
        private BoardManager boardManager;

    public void SetBoardManager(BoardManager board)
    {
        boardManager = board;
    }
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

            case "apply_buff":
            {
                var buffResource = effect.Get("buff").As<GodotObject>();
                int stacks = effect.Get("buff_stacks").AsInt32();
                int buffTarget = effect.Get("buff_target").AsInt32();
                var targetCell = effect.Get("buff_target_cell").AsVector2I();

                if (buffResource == null) return false;

                switch (buffTarget)
                {
                    case 0: // PLAYER_STATS
                        ApplyBuffToPlayerStats(buffResource, stacks);
                        break;
                    case 1: // ENEMY_STATS
                        ApplyBuffToEnemyStats(buffResource, stacks);
                        break;
                    case 2: // PLAYER_CELLS
                        ApplyBuffToPlayerCells(buffResource, stacks, targetCell);
                        break;
                    case 3: // ENEMY_CELLS
                        break;
                }
                return true;
            }

            case "remove_buff":
            {
                var buffObj = effect.Get("buff").As<GodotObject>();
                string buffId = "";
                if (buffObj != null)
                    buffId = buffObj.Get("id").AsString();

                if (string.IsNullOrEmpty(buffId)) return false;

                if (target == 0)
                    RemoveBuffFromPlayerCells(buffId);
                else
                    RemoveBuffFromEnemyCells(buffId);
                return true;
            }

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

private void ApplyBuffToPlayerCells(GodotObject buffResource, int stacks, Vector2I targetCell)
{
    if (targetCell.X >= 0 && targetCell.Y >= 0)
    {
        // 指定单个格子
        var cell = boardManager.GetCell(targetCell);
        if (cell == null) return;
        var stats = cell.Get("stats").As<GodotObject>();
        stats?.Call("add_buff", buffResource, stacks);
    }
    else
    {
        // 全体玩家已占格子
        foreach (var runtime in boardManager.runtime_cards)
        {
            var cells = runtime.Get("occupied_cells").As<Array<Vector2I>>();
            foreach (var pos in cells)
            {
                var cell = boardManager.GetCell(pos);
                var stats = cell?.Get("stats").As<GodotObject>();
                stats?.Call("add_buff", buffResource, stacks);
            }
        }
    }
}

        private void ApplyBuffToPlayerStats(GodotObject buffResource, int stacks)
        {
            // 玩家全局 Stats（目前还没创建，预留）
            // playerStats?.Call("add_buff", buffResource, stacks);
            GD.Print($"玩家获得 Buff：{buffResource.Get("buff_name")} ×{stacks}");
        }

        private void ApplyBuffToEnemyStats(GodotObject buffResource, int stacks)
        {
            // 敌人全局 Stats（预留）
            GD.Print($"敌人获得 Buff：{buffResource.Get("buff_name")} ×{stacks}");
        }

    private void RemoveBuffFromPlayerCells(string buffId)
    {
        foreach (var runtime in boardManager.runtime_cards)
        {
            var cells = runtime.Get("occupied_cells").As<Array<Vector2I>>();
            foreach (var pos in cells)
            {
                var cell = boardManager.GetCell(pos);
                var stats = cell?.Get("stats").As<GodotObject>();
                stats?.Call("remove_buff", buffId);
            }
        }
    }

    private void RemoveBuffFromEnemyCells(string buffId)
    {
        // 同上
    }
    }