extends EnemyAIController
class_name SharkkEnemyAI

@export_group("Sharkk tuning")
@export_range(1, 30, 1) var adapt_damage_threshold: int = 10
@export_range(0.0, 1.0, 0.05) var attack_priority_chance: float = 0.72
@export_range(0.0, 1.0, 0.05) var charge_chance: float = 0.70
@export_range(0, 9, 1) var ranged_pressure_cap: int = 5

enum ChargeState { NEUTRAL, PREPARED, RECOVERING }
var charge_state: ChargeState = ChargeState.NEUTRAL
var ranged_pressure: int = 0


func reset_ai() -> void:
	super.reset_ai()
	charge_state = ChargeState.NEUTRAL
	ranged_pressure = 0


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	# State-machine actions are guarantees, not weighted suggestions.
	if charge_state == ChargeState.RECOVERING:
		return commit_action(first_available([&"sharkk_stunned"], context))
	if charge_state == ChargeState.PREPARED:
		var prepared := first_available([&"sharkk_charge"], context)
		if prepared != null:
			return commit_action(prepared)
		# Keep the prepared state while closing into charge range.
		return commit_action(first_available([&"sharkk_advance"], context))
	# A 10+ hit always forces sand + retreat, then queues a charge.
	if context.damage_taken_last_turn >= adapt_damage_threshold:
		var sand_retreat := first_available([&"sharkk_sand_retreat"], context)
		if sand_retreat != null:
			return commit_action(sand_retreat)
	# Any hit at middle range creates the readable retreat -> charge rhythm.
	if context.distance >= 2 and context.distance <= 4 and context.damage_taken_last_turn > 0:
		var reactive_prepare := first_available([&"sharkk_prepare_charge"], context)
		if reactive_prepare != null:
			return commit_action(reactive_prepare)
	var pressure := _projected_ranged_pressure(context)
	if context.distance == 1:
		return commit_action(weighted_ids(context, {
			&"sharkk_punch": maxf(0.10, attack_priority_chance),
			&"sharkk_sand_retreat": 0.10,
		}))
	if context.distance >= 2 and context.distance <= 4:
		return commit_action(weighted_ids(context, {
			&"sharkk_prepare_charge": charge_chance + float(pressure) * 0.12,
			&"sharkk_attack": 0.25,
			&"sharkk_advance": 0.15 + float(pressure) * 0.08,
		}))
	# At long range Sharkk never idles: ranged pressure makes closing even more
	# deterministic and it keeps a queued charge state intact.
	return commit_action(weighted_ids(context, {
		&"sharkk_advance": 0.80 + float(pressure) * 0.15,
	}))


func _on_action_confirmed(action: EnemyActionData, _context: EnemyAIContext) -> void:
	match action.id:
		&"sharkk_prepare_charge", &"sharkk_sand_retreat": charge_state = ChargeState.PREPARED
		&"sharkk_charge": charge_state = ChargeState.RECOVERING
		&"sharkk_stunned": charge_state = ChargeState.NEUTRAL
	if _context.last_player_action_type in [&"gun", &"ranged"]:
		ranged_pressure = mini(ranged_pressure_cap, ranged_pressure + 2)
		if _context.distance >= 3 and _context.damage_taken_last_turn > 0:
			ranged_pressure = mini(ranged_pressure_cap, ranged_pressure + 1)
	else:
		ranged_pressure = maxi(0, ranged_pressure - 1)


func _projected_ranged_pressure(context: EnemyAIContext) -> int:
	var pressure := ranged_pressure
	if context.last_player_action_type in [&"gun", &"ranged"]:
		pressure += 2
		if context.distance >= 3 and context.damage_taken_last_turn > 0:
			pressure += 1
	return clampi(pressure, 0, ranged_pressure_cap)
