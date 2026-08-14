extends Node
class_name DangerAreaPredictor

## Pure prediction component.  It never calls EnemyAIController.select_action,
## so UI refreshes cannot consume RNG, cooldown, or state-machine transitions.

func predict_action(
	action: EnemyActionData,
	actor_position: int,
	actor_facing: int,
	player_position: int,
	cell_count: int,
	occupied_cells: PackedInt32Array = PackedInt32Array()
) -> Dictionary:
	var result := {
		"action_id": action.id if action != null else &"",
		"current_cells": PackedInt32Array(),
		"future_cells": PackedInt32Array(),
		"movement_destination": actor_position,
		"global_warning": "",
		"severity": 0,
		"color": Color.TRANSPARENT,
	}
	if action == null or not action.danger_profile is DangerAreaProfile:
		return result
	var profile := action.danger_profile as DangerAreaProfile
	result.severity = profile.severity
	result.color = profile.color
	var cells := PackedInt32Array()
	match profile.shape:
		DangerAreaProfile.Shape.FORWARD_RANGE:
			cells = _forward_cells(actor_position, actor_facing, profile.min_range, profile.max_range, cell_count)
		DangerAreaProfile.Shape.FIXED_PARITY:
			for cell in range(1, cell_count + 1):
				if cell % 2 == profile.parity:
					cells.append(cell)
		DangerAreaProfile.Shape.CHARGE_PUSH_PATH:
			cells = _charge_push_cells(actor_position, player_position, cell_count, occupied_cells)
			result.movement_destination = _charge_destination(actor_position, player_position, occupied_cells)
		DangerAreaProfile.Shape.DESTINATION:
			var destination := _movement_destination(
				actor_position, player_position, cell_count, occupied_cells,
				profile.movement_amount, profile.movement_toward_player
			)
			result.movement_destination = destination
			if destination != actor_position:
				cells.append(destination)
		DangerAreaProfile.Shape.GLOBAL_WARNING:
			result.global_warning = profile.global_warning
		_:
			pass
	if profile.telegraph_turn_offset > 0:
		result.future_cells = _unique_valid(cells, cell_count)
	else:
		result.current_cells = _unique_valid(cells, cell_count)
	return result


func predict_plans(
	plans: Array,
	cell_count: int,
	player_position: int,
	occupied_cells: PackedInt32Array = PackedInt32Array()
) -> Array[Dictionary]:
	var results: Array[Dictionary] = []
	for plan_variant in plans:
		if not plan_variant is Dictionary:
			continue
		var plan: Dictionary = plan_variant
		var action := plan.get("action") as EnemyActionData
		results.append(predict_action(
			action,
			int(plan.get("actor_position", 1)),
			int(plan.get("actor_facing", 0)),
			player_position,
			cell_count,
			occupied_cells
		))
	return results


func _forward_cells(actor_position: int, actor_facing: int, min_range: int, max_range: int, cell_count: int) -> PackedInt32Array:
	var cells := PackedInt32Array()
	var direction := 1 if actor_facing == 0 else -1
	for distance in range(maxi(0, min_range), maxi(min_range, max_range) + 1):
		var cell := actor_position + direction * distance
		if cell >= 1 and cell <= cell_count:
			cells.append(cell)
	return cells


func _charge_destination(actor_position: int, player_position: int, occupied_cells: PackedInt32Array) -> int:
	var direction := signi(player_position - actor_position)
	if direction == 0:
		return actor_position
	var destination := actor_position
	var candidate := actor_position + direction
	while candidate != player_position:
		if occupied_cells.has(candidate):
			break
		destination = candidate
		candidate += direction
	return destination


func _charge_push_cells(actor_position: int, player_position: int, cell_count: int, occupied_cells: PackedInt32Array) -> PackedInt32Array:
	var cells := PackedInt32Array()
	var direction := signi(player_position - actor_position)
	if direction == 0:
		return cells
	var candidate := actor_position + direction
	while candidate != player_position:
		if occupied_cells.has(candidate):
			break
		cells.append(candidate)
		candidate += direction
	cells.append(player_position)
	# The charge pushes the player in the same direction until the last legal,
	# unoccupied cell before the wall.
	candidate = player_position + direction
	while candidate >= 1 and candidate <= cell_count:
		if occupied_cells.has(candidate):
			break
		cells.append(candidate)
		candidate += direction
	return cells


func _movement_destination(
	actor_position: int,
	player_position: int,
	cell_count: int,
	occupied_cells: PackedInt32Array,
	amount: int,
	toward: bool
) -> int:
	var direction := signi(player_position - actor_position)
	if not toward:
		direction *= -1
	var destination := actor_position
	for _step in range(maxi(0, amount)):
		var candidate := destination + direction
		if candidate < 1 or candidate > cell_count or candidate == player_position or occupied_cells.has(candidate):
			break
		destination = candidate
	return destination


func _unique_valid(cells: PackedInt32Array, cell_count: int) -> PackedInt32Array:
	var unique := PackedInt32Array()
	for cell in cells:
		if cell >= 1 and cell <= cell_count and not unique.has(cell):
			unique.append(cell)
	return unique

