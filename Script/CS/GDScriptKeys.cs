using Godot;

/// <summary>
/// C# ↔ GDScript 跨语言字符串常量。
/// 所有通过 GodotObject.Get/Set/Call 访问 GDScript 属性的键名集中在此，
/// 避免运行时拼写错误导致的静默失败。
///
/// 用法示例（替代裸字符串）：
///   data.Get(GDScriptKeys.CardData.DisplayName).AsString()
///   stats.Call(GDScriptKeys.Stats.HasBuff, "dust")
/// </summary>
public static class GDScriptKeys
{
    // ================================================================
    //  CardData (card_data.gd)
    // ================================================================
    public static class CardData
    {
        public static readonly StringName Id = "id";
        public static readonly StringName DisplayName = "display_name";
        public static readonly StringName IconText = "icon_text";
        public static readonly StringName Glyph = "glyph";
        public static readonly StringName Category = "category";
        public static readonly StringName Description = "description";
        public static readonly StringName ShapeOffsets = "shape_offsets";
        public static readonly StringName MinRange = "min_range";
        public static readonly StringName MaxRange = "max_range";
        public static readonly StringName OncePerTurn = "once_per_turn";
        public static readonly StringName CooldownTurns = "cooldown_turns";
        public static readonly StringName Effects = "effects";
        public static readonly StringName EffectType = "effect_type";
        public static readonly StringName EffectValue = "effect_value";
        public static readonly StringName UpgradedVersion = "upgraded_version";
        public static readonly StringName IsUpgraded = "is_upgraded";
        public static readonly StringName Tint = "tint";

        // 方法名
        public static readonly StringName HasDamageEffect = "has_damage_effect";
        public static readonly StringName HasSwapEffect = "has_swap_effect";
        public static readonly StringName HasRangeTarget = "has_range_target";
        public static readonly StringName GetRotatedShape = "get_rotated_shape";
    }

    // ================================================================
    //  CardRuntime (card_runtime.gd)
    // ================================================================
    public static class CardRuntime
    {
        public static readonly StringName InstanceId = "instance_id";
        public static readonly StringName Data = "data";
        public static readonly StringName AnchorPosition = "anchor_position";
        public static readonly StringName RotationSteps = "rotation_steps";
        public static readonly StringName OccupiedCells = "occupied_cells";
        public static readonly StringName CooldownRemaining = "cooldown_remaining";
        public static readonly StringName IsReady = "is_ready";
    }

    // ================================================================
    //  CellRuntime (cell_runtime.gd)
    // ================================================================
    public static class CellRuntime
    {
        public static readonly StringName Position = "position";
        public static readonly StringName CardInstanceId = "card_instance_id";
        public static readonly StringName LocalShapeIndex = "local_shape_index";
        public static readonly StringName IsLit = "is_lit";
        public static readonly StringName Stats = "stats";
    }

    // ================================================================
    //  Stats (stats.gd) — 方法名
    // ================================================================
    public static class Stats
    {
        public static readonly StringName HasBuff = "has_buff";
        public static readonly StringName GetBuffStacks = "get_buff_stacks";
        public static readonly StringName AddBuff = "add_buff";
        public static readonly StringName RemoveBuff = "remove_buff";
        public static readonly StringName TickTurnStart = "tick_turn_start";
        public static readonly StringName TickTurnEnd = "tick_turn_end";
        public static readonly StringName ClearBuffs = "clear_buffs";
        public static readonly StringName Buffs = "buffs";
    }

    // ================================================================
    //  Buff (buff_data.gd)
    // ================================================================
    public static class Buff
    {
        public static readonly StringName Id = "id";
        public static readonly StringName BuffName = "buff_name";
        public static readonly StringName Polarity = "polarity";
        public static readonly StringName Description = "description";
        public static readonly StringName Duration = "duration";
        public static readonly StringName Stackable = "stackable";
        public static readonly StringName MaxStacks = "max_stacks";
        public static readonly StringName Icon = "icon";
    }

    // ================================================================
    //  CombatEffectData (combat_effect_data.gd)
    // ================================================================
    public static class CombatEffect
    {
        public static readonly StringName DisplayName = "display_name";
        public static readonly StringName Type = "type";
        public static readonly StringName Target = "target";
        public static readonly StringName Amount = "amount";
        public static readonly StringName Buff = "buff";
        public static readonly StringName BuffStacks = "buff_stacks";
        public static readonly StringName BuffTarget = "buff_target";
        public static readonly StringName BuffTargetCell = "buff_target_cell";

        // 方法名
        public static readonly StringName TypeKey = "type_key";
    }

    // ================================================================
    //  GameRules (game_rules.gd)
    // ================================================================
    public static class GameRules
    {
        public static readonly StringName PlayerData = "player_data";
        public static readonly StringName EnemyData = "enemy_data";
        public static readonly StringName BattleMap = "battle_map";
        public static readonly StringName EnergyPerTurn = "energy_per_turn";
        public static readonly StringName ChargeEnergyCost = "charge_energy_cost";
        public static readonly StringName MoveEnergyCost = "move_energy_cost";
        public static readonly StringName PlayerMoveStep = "player_move_step";
        public static readonly StringName PreservePartialCharge = "preserve_partial_charge_between_turns";
        public static readonly StringName BoardSizes = "board_sizes";
    }

    // ================================================================
    //  PlayerData / EnemyData (player_data.gd, enemy_data.gd)
    // ================================================================
    public static class CharacterData
    {
        public static readonly StringName Id = "id";
        public static readonly StringName DisplayName = "display_name";
        public static readonly StringName Subtitle = "subtitle";
        public static readonly StringName Glyph = "glyph";
        public static readonly StringName Tint = "tint";
        public static readonly StringName MaxHp = "max_hp";
        public static readonly StringName InitialShield = "initial_shield";
        public static readonly StringName Actions = "actions"; // enemy only
    }

    // ================================================================
    public static class EnemyData
    {
        public static readonly StringName GetActionForDistance = "get_action_for_distance";
    }
    // ================================================================

    public static class EnemyAction
    {
        public static readonly StringName Id = "id";
        public static readonly StringName DisplayName = "display_name";
        public static readonly StringName MinRange = "min_range";
        public static readonly StringName MaxRange = "max_range";
        public static readonly StringName Effects = "effects";
        public static readonly StringName Priority = "priority";
        public static readonly StringName Tags = "tags";
        public static readonly StringName SpecialEffect = "special_effect";
        public static readonly StringName EffectTargetRole = "effect_target_role";
        public static readonly StringName SpecialValue = "special_value";
        public static readonly StringName SpecialReplacesEffects = "special_replaces_effects";

        // 方法名
        public static readonly StringName IsAvailable = "is_available";
        public static readonly StringName GetActionForDistance = "get_action_for_distance";
    }

    // ================================================================
    //  ItemData (itemdata.gd)
    // ================================================================
    public static class ItemData
    {
        public static readonly StringName Id = "id";
        public static readonly StringName DisplayName = "display_name";
        public static readonly StringName Effects = "effects";
        public static readonly StringName SpecialEffects = "special_effects";
        public static readonly StringName SpecialAmount = "special_amount";
        public static readonly StringName ConsumeOnUse = "consume_on_use";
    }

    // ================================================================
    //  BattleMapData (battle_map_data.gd)
    // ================================================================
    public static class BattleMap
    {
        public static readonly StringName CellCount = "cell_count";
        public static readonly StringName PlayerStartCell = "player_start_cell";
        public static readonly StringName EnemyStartCell = "enemy_start_cell";
        public static readonly StringName PlayerForwardDirection = "player_forward_direction";
        public static readonly StringName IncludeOccupiedCellsInDistance = "include_occupied_cells_in_distance";

        // 方法名
        public static readonly StringName IsValidCell = "is_valid_cell";
        public static readonly StringName CombatDistance = "combat_distance";
    }
}
