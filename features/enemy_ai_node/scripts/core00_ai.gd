extends EnemyAIController
class_name Core00EnemyAI

@export_group("Core-00 phase one")
@export_range(1, 20, 1) var phase_one_cycle_length: int = 5
@export var left_hand_role: StringName = &"true_hand"
@export var right_hand_role: StringName = &"false_hand"

@export_group("Core-00 phase two")
@export_range(1, 30, 1) var teleport_reaction_damage: int = 8
@export_range(0.0, 1.0, 0.05) var phase_two_attack_chance: float = 0.72


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	if context.phase <= 1 or context.role in [left_hand_role, right_hand_role]:
		return _select_hand_action(context)
	return _select_body_action(context)


func _select_hand_action(context: EnemyAIContext) -> EnemyActionData:
	var step := ((context.round_number - 1) % phase_one_cycle_length) + 1
	var is_true := context.role == left_hand_role
	var selected_id: StringName
	match step:
		1:
			if is_true:
				selected_id = &"core_true_guard_beam" if context.player_is_on_even_cell() else &"core_true_guard_only"
			else:
				selected_id = &"core_false_charge"
		2:
			selected_id = &"core_true_death_loop" if is_true else &"core_false_charge_complete"
		3:
			if is_true:
				selected_id = &"core_true_death_loop"
			else:
				selected_id = &"core_false_break_beam" if context.player_is_on_even_cell() else &"core_false_break_only"
		4:
			selected_id = &"core_true_send_heal" if is_true else &"core_false_stun"
		5:
			selected_id = &"core_true_charge" if is_true else &"core_false_heal"
	# The two hands are fixed choreography: cooldown/range never changes intent.
	var action := action_by_id(selected_id)
	return commit_action(action)


func _select_body_action(context: EnemyAIContext) -> EnemyActionData:
	if context.damage_taken_last_turn >= teleport_reaction_damage:
		var blink := first_available([&"core_body_teleport_guard"], context)
		if blink != null:
			return _commit_body_action(blink)
	# Jam is a readable cadence rather than random repetition.
	if context.round_number % 4 == 0:
		var jam := first_available([&"core_body_jam"], context)
		if jam != null:
			return _commit_body_action(jam)
	if context.distance >= 6:
		return _commit_body_action(weighted_ids(context, {
			&"core_body_sniper": 0.75,
			&"core_body_advance_3": 0.25,
		}))
	if context.distance == 1:
		return _commit_body_action(weighted_ids(context, {
			&"core_body_gunstock": 0.75,
			&"core_body_teleport_guard": 0.25,
		}))
	if context.distance == 2:
		return _commit_body_action(first_available([&"core_body_reposition", &"core_body_teleport_guard"], context))
	if context.distance >= 3 and context.distance <= 4:
		return _commit_body_action(weighted_ids(context, {
			&"core_body_pulse": phase_two_attack_chance,
			&"core_body_reposition": maxf(0.10, 1.0 - phase_two_attack_chance),
		}))
	return _commit_body_action(first_available([&"core_body_advance_3", &"core_body_pulse", &"core_body_reposition"], context))


func _commit_body_action(action: EnemyActionData) -> EnemyActionData:
	if action == null:
		return null
	return commit_action(action)
