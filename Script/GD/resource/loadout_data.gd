extends Resource
class_name LoadoutData

@export var board_size: Vector2i = Vector2i(3, 2)
@export var placements: Array[LoadoutPlacement] = []


func to_placement_dictionaries() -> Array[Dictionary]:
	var result: Array[Dictionary] = []
	for placement in placements:
		if placement != null:
			result.append(placement.to_dictionary())
	return result


func duplicate_loadout() -> LoadoutData:
	var result := LoadoutData.new()
	result.board_size = board_size
	for placement in placements:
		if placement == null: continue
		var copy := LoadoutPlacement.new()
		copy.card_id = placement.card_id
		copy.anchor = placement.anchor
		copy.rotation_steps = placement.rotation_steps
		result.placements.append(copy)
	return result