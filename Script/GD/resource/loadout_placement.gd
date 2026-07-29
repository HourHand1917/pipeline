extends Resource
class_name LoadoutPlacement

@export var card_id: StringName = &""
@export var anchor: Vector2i = Vector2i.ZERO
@export_range(0, 3, 1) var rotation_steps: int = 0


func to_dictionary() -> Dictionary:
	return {
		"card_id": card_id,
		"anchor": anchor,
		"rotation": posmod(rotation_steps, 4),
	}


static func from_dictionary(data: Dictionary) -> LoadoutPlacement:
	var placement := LoadoutPlacement.new()
	placement.card_id = data.get("card_id", &"")
	placement.anchor = data.get("anchor", Vector2i.ZERO)
	placement.rotation_steps = posmod(int(data.get("rotation", 0)), 4)
	return placement