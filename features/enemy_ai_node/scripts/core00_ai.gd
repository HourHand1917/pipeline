extends EnemyAIController
class_name Core00EnemyAI

@export_group("Core-00 phase one")
@export var left_hand_role: StringName = &"true_hand"
@export var right_hand_role: StringName = &"false_hand"

@export_group("Core-00 phase two")
@export_range(1, 30, 1) var teleport_reaction_damage: int = 8
@export_range(0.0, 1.0, 0.05) var phase_two_attack_chance: float = 0.72

## True uses 0 as its judgement state. Once step 1 starts, confirmation moves
## this through 2 and 3 without re-checking the Buff until the chain finishes.
var _true_step: int = 0
var _false_cycle: int = 1


func reset_ai() -> void:
	super.reset_ai()
	_true_step = 0
	_false_cycle = 1


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	if context.phase <= 1 or context.role in [left_hand_role, right_hand_role]:
		return _select_hand_action(context)
	return _select_body_action(context)


func _select_hand_action(context: EnemyAIContext) -> EnemyActionData:
	var is_true := context.role == left_hand_role
	var counterpart_alive := context.false_hand_hp > 0 if is_true else context.true_hand_hp > 0
	if not counterpart_alive:
		return commit_action(action_by_id(&"core_hand_passive"))
	if is_true:
		return _select_true_hand_action(context)
	return _select_false_hand_action()


func _select_true_hand_action(context: EnemyAIContext) -> EnemyActionData:
	var selected_id: StringName
	if _true_step == 0:
		selected_id = &"core_true_death_loop" if context.enemy_has_true_buff else &"core_true_send_heal"
	elif _true_step == 2:
		selected_id = &"core_true_charge"
	else:
		selected_id = &"core_true_guard_beam"
	return commit_action(action_by_id(selected_id))


func _select_false_hand_action() -> EnemyActionData:
	var ids: Array[StringName] = [
		&"core_false_charge",
		&"core_false_heal",
		&"core_false_stun",
		&"core_false_charge_complete",
		&"core_false_break_beam",
	]
	var index := clampi(_false_cycle, 1, ids.size()) - 1
	return commit_action(action_by_id(ids[index]))


func _on_action_confirmed(action: EnemyActionData, context: EnemyAIContext) -> void:
	if action == null or context.phase > 1:
		return
	if context.role == left_hand_role:
		match action.id:
			&"core_true_send_heal": _true_step = 2
			&"core_true_charge": _true_step = 3
			&"core_true_guard_beam": _true_step = 0
	elif context.role == right_hand_role:
		match action.id:
			&"core_false_charge", &"core_false_heal", &"core_false_stun", &"core_false_charge_complete", &"core_false_break_beam":
				_false_cycle = (_false_cycle % 5) + 1


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
