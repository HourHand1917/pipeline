extends Resource
class_name CardData

@export var id: StringName
@export var display_name: String = ""
@export var icon_text: String = "?"
@export var category: String = ""
@export var description: String = ""

# Vector2i.x = 列，Vector2i.y = 行
@export var shape_offsets: Array[Vector2i] = [Vector2i.ZERO]

@export_enum("damage", "shield", "energy", "heal")
var effect_type: String = "damage"

@export var effect_value: int = 1
@export var cooldown_turns: int = 1


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
