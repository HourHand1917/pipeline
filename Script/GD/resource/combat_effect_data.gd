extends Resource
class_name CombatEffectData

enum Type {
	DAMAGE,
	SHIELD,
	HEAL,
	MOVE_TOWARD_OPPONENT,
	MOVE_AWAY_FROM_OPPONENT,
	ENERGY,
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


func summary() -> String:
	var label := display_name.strip_edges()
	if label.is_empty():
		label = _default_display_name()
	match type:
		Type.MOVE_TOWARD_OPPONENT, Type.MOVE_AWAY_FROM_OPPONENT:
			return "%s %d 格" % [label, amount]
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
	return &""


func _default_display_name() -> String:
	match type:
		Type.DAMAGE: return "伤害"
		Type.SHIELD: return "护盾"
		Type.HEAL: return "回复"
		Type.MOVE_TOWARD_OPPONENT: return "逼近"
		Type.MOVE_AWAY_FROM_OPPONENT: return "远离"
		Type.ENERGY: return "能量"
	return "效果"