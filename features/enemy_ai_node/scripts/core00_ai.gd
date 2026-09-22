extends EnemyAIController
class_name Core00EnemyAI

@export_group("Core-00 phase one")
@export var left_hand_role: StringName = &"true_hand"
@export var right_hand_role: StringName = &"false_hand"

@export_group("Core-00 phase two")
@export_range(1, 30, 1) var teleport_reaction_damage: int = 8
@export_range(0.0, 1.0, 0.05) var phase_two_attack_chance: float = 0.72

## True 手的维护步骤：0 为判定状态（死循环保护 / 维护步骤1）。
## 维护步骤一旦开始，确认推进 2、3 两步时不再重新检查 True 标记，
## 直到维护步骤走完才回到判定。
var _maintenance_step: int = 0
var _false_cycle: int = 1


func reset_ai() -> void:
	super.reset_ai()
	_maintenance_step = 0
	_false_cycle = 1


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	if context.phase <= 1 or context.role in [left_hand_role, right_hand_role]:
		return _select_hand_action(context)
	return _select_body_action(context)


func _select_hand_action(context: EnemyAIContext) -> EnemyActionData:
	if context.role == left_hand_role:
		return _select_true_hand_action(context)
	# False 手永远按固定5步循环行动，不受 True 手生死影响
	return _select_false_hand_action()


func _select_true_hand_action(context: EnemyAIContext) -> EnemyActionData:
	var selected_id: StringName
	if _maintenance_step == 0:
		# 死循环保护：自身带 True 标记时维持；False 手已死时也永远困在死循环里。
		# 否则进入维护步骤：步骤1发射治疗包 → 步骤2蓄力 → 步骤3保护光束。
		# 维护步骤一旦开始（步骤2/3）不重新检查标记，走完才回到这里的判定。
		if context.enemy_has_true_buff or context.false_hand_hp <= 0:
			selected_id = &"core_true_death_loop"
		else:
			selected_id = &"core_true_send_heal"
	elif _maintenance_step == 2:
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
			&"core_true_send_heal": _maintenance_step = 2
			&"core_true_charge": _maintenance_step = 3
			&"core_true_guard_beam": _maintenance_step = 0
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
