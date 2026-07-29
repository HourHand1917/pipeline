extends Resource
class_name CardData

enum Category {
	ATTACK,
	DEFENSE,
	HEAL,
}

@export_group("Identity")
@export var id: StringName
@export var display_name: String = ""
@export var icon_text: String = "?"
@export var glyph: String = "?"
@export var category: String = ""
@export_multiline var description: String = ""

@export_group("Board Shape")
@export var shape_offsets: Array[Vector2i] = [Vector2i.ZERO]

@export_group("Activation")
@export_range(0, 99, 1, "or_greater") var min_range: int = 0
@export_range(0, 99, 1, "or_greater") var max_range: int = 0
@export var once_per_turn: bool = true

@export_group("Effects (old)")
@export_enum("damage", "shield", "energy", "heal")
var effect_type: String = "damage"
@export var effect_value: int = 1
@export var cooldown_turns: int = 1

@export_group("Effects (new)")
@export var effects: Array[CombatEffectData] = []

@export_group("Presentation")
@export var tint: Color = Color.WHITE


func get_rotated_shape(rotation_steps: int) -> Array[Vector2i]:
	var result: Array[Vector2i] = []
	for offset in shape_offsets:
		result.append(offset)

	for _step in range(posmod(rotation_steps, 4)):
		var rotated: Array[Vector2i] = []
		for point in result:
			rotated.append(Vector2i(-point.y, point.x))

		var min_x := 999999
		var min_y := 999999
		for point in rotated:
			min_x = mini(min_x, point.x)
			min_y = mini(min_y, point.y)

		result.clear()
		for point in rotated:
			result.append(Vector2i(point.x - min_x, point.y - min_y))

	return result


func is_attack() -> bool:
	return category == "attack"


func has_damage_effect() -> bool:
	for effect in effects:
		if effect != null and effect.type == CombatEffectData.Type.DAMAGE:
			return true
	if effect_type == "damage":
		return true
	return false


func range_text() -> String:
	if not has_damage_effect():
		return "目标：自身"
	var range_min := mini(min_range, max_range)
	var range_max := maxi(min_range, max_range)
	if range_min == range_max:
		return "射程 %d" % range_min
	return "射程 %d–%d" % [range_min, range_max]


func effect_summary() -> String:
	if not effects.is_empty():
		var summaries: PackedStringArray = []
		for effect in effects:
			if effect != null:
				summaries.append(effect.summary())
		return " / ".join(summaries)
	return "%s %d" % [_effect_display_name(), effect_value]


func _effect_display_name() -> String:
	match effect_type:
		"damage": return "伤害"
		"shield": return "护盾"
		"energy": return "能量"
		"heal": return "回复"
	return "效果"
