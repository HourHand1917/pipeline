using Godot;
using Godot.Collections;

public partial class EffectResolver : Node
{
    [Signal] public delegate void EffectExecutedEventHandler(string sourceName, string effectType, int value);

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

        var effects = data.Get("effects").As<Array>();
        if (effects != null && effects.Count > 0)
        {
            return ExecuteEffects(displayName, effects, battleManager);
        }

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

    public bool ExecuteEffects(string sourceName, Array effects, BattleManager battleManager)
    {
        bool executedAny = false;
        foreach (var effect in effects)
        {
            if (effect.Obj == null) continue;
            var eff = effect.As<GodotObject>();
            string typeKey = eff.Call("type_key").AsString();

            if (ExecuteEffect(eff, battleManager))
            {
                executedAny = true;
                EmitSignal(SignalName.EffectExecuted,
                    sourceName,
                    typeKey,
                    eff.Get("amount").AsInt32());
            }
        }
        return executedAny;
    }

    private bool ExecuteEffect(GodotObject effect, BattleManager battleManager)
    {
        string typeKey = effect.Call("type_key").AsString();
        int amount = effect.Get("amount").AsInt32();
        int target = effect.Get("target").AsInt32();

        switch (typeKey)
        {
            case "damage":
                if (target == 0)
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

            case "swap_position":
                battleManager.SwapPosition();
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
                if (buffResource == null) return false;

                int stacks = effect.Get("buff_stacks").AsInt32();
                int buffTarget = effect.Get("buff_target").AsInt32();
                var targetCell = effect.Get("buff_target_cell").AsVector2I();

                switch (buffTarget)
                {
                    case 0: ApplyBuffToPlayerStats(buffResource, stacks); break;
                    case 1: ApplyBuffToEnemyStats(buffResource, stacks); break;
                    case 2: ApplyBuffToPlayerCells(buffResource, stacks, targetCell); break;
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

    private bool ExecuteLegacyEffect(string effectType, int effectValue, BattleManager battleManager)
    {
        switch (effectType)
        {
            case "damage": battleManager.DamageEnemy(effectValue); return true;
            case "shield": battleManager.AddPlayerShield(effectValue); return true;
            case "energy": battleManager.AddPlayerEnergy(effectValue); return true;
            case "heal": battleManager.HealPlayer(effectValue); return true;
            default: GD.PushWarning($"未知效果类型：{effectType}"); return false;
        }
    }

    private void ApplyBuffToPlayerCells(GodotObject buffResource, int stacks, Vector2I targetCell)
    {
        if (targetCell.X >= 0 && targetCell.Y >= 0)
        {
            var cell = boardManager.GetCell(targetCell);
            if (cell == null) return;
            var stats = cell.Get("stats").As<GodotObject>();
            stats?.Call("add_buff", buffResource, stacks);
        }
        else
        {
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

    private void ApplyBuffToPlayerStats(GodotObject buffResource, int stacks) { }
    private void ApplyBuffToEnemyStats(GodotObject buffResource, int stacks) { }

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

    private void RemoveBuffFromEnemyCells(string buffId) { }
}