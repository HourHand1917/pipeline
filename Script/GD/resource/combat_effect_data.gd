extends Resource
class_name CombatEffectData

enum Type {
	DAMAGE,
	SHIELD,
	HEAL,
	MOVE_TOWARD_OPPONENT,
	MOVE_AWAY_FROM_OPPONENT,
	ENERGY,
	APPLY_BUFF,          # 新增：施加 Buff
	REMOVE_BUFF,         # 新增：移除 Buff
}

enum Target {
	PLAYER,
	ENEMY,
}

@export_group("Display")
@export var display_name: String = ""
@export_multiline var description: String = ""

@export_group("Effect")
@export var type: Type = Type.DAMAGE
@export var target: Target = Target.ENEMY
@export_range(0, 999, 1, "or_greater") var amount: int = 1

# ============ Buff 专用字段 ============
enum BuffTarget {
    PLAYER_STATS,    # 玩家状态
    ENEMY_STATS,     # 敌人状态
    PLAYER_CELLS,    # 玩家格子
    ENEMY_CELLS,     # 敌人格子（预留）
}

@export_group("Buff")
@export var buff: Buff
@export var buff_stacks: int = 1
@export var buff_target: BuffTarget = BuffTarget.PLAYER_CELLS
@export var buff_target_cell: Vector2i = Vector2i(-1, -1) 

func summary() -> String:
	var label := display_name.strip_edges()
	if label.is_empty():
		label = _default_display_name()
	match type:
		Type.MOVE_TOWARD_OPPONENT, Type.MOVE_AWAY_FROM_OPPONENT:
			return "%s %d 格" % [label, amount]
		Type.APPLY_BUFF:
			if buff != null:
				return "施加 %s ×%d" % [buff.buff_name, buff_stacks]
			return "施加 Buff"
		Type.REMOVE_BUFF:
			return "移除 Buff"
		_:
			return "%s %d" % [label, amount]


func type_key() -> StringName:
	match type:
		Type.DAMAGE: return &"damage"
		Type.SHIELD: return &"shield"
		Type.HEAL: return &"heal"
		Type.MOVE_TOWARD_OPPONENT: return &"move_toward_opponent"
		Type.MOVE_AWAY_FROM_OPPONENT: return &"move_away_from_opponent"
		Type.ENERGY: return &"energy"
		Type.APPLY_BUFF: return &"apply_buff"
		Type.REMOVE_BUFF: return &"remove_buff"
	return &""


func _default_display_name() -> String:
	match type:
		Type.DAMAGE: return "伤害"
		Type.SHIELD: return "护盾"
		Type.HEAL: return "回复"
		Type.MOVE_TOWARD_OPPONENT: return "逼近"
		Type.MOVE_AWAY_FROM_OPPONENT: return "远离"
		Type.ENERGY: return "能量"
		Type.APPLY_BUFF: return "施加 Buff"
		Type.REMOVE_BUFF: return "移除 Buff"
	return "效果"