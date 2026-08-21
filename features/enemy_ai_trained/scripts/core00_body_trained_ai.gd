extends AdaptiveEnemyAIController
class_name Core00BodyTrainedAI

@export_group("Core-00 response")
@export_range(1, 30, 1) var blink_reaction_damage: int = 8
@export_range(1, 12, 1) var sniper_initial_cooldown: int = 4
var _initial_cooldown_armed := false


func reset_ai() -> void:
	super.reset_ai()
	_initial_cooldown_armed = false


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_arm_initial_sniper_cooldown()
	_begin_decision(context)
	var metrics := observable_metrics(context)
	var weights := _base_weights(context)
	if context.damage_taken_last_turn >= blink_reaction_damage:
		weights[&"trained_core_body_teleport_guard"] = maxf(80.0, float(weights.get(&"trained_core_body_teleport_guard", 0.0)) * 2.0)
	var highest_bucket := _highest_damage_bucket(metrics)
	if highest_bucket == _distance_bucket(context.distance):
		weights[&"trained_core_body_teleport_guard"] = float(weights.get(&"trained_core_body_teleport_guard", 0.0)) * 1.60
		weights[&"trained_core_body_advance_1"] = float(weights.get(&"trained_core_body_advance_1", 0.0)) * 1.60
		weights[&"trained_core_body_advance_2"] = float(weights.get(&"trained_core_body_advance_2", 0.0)) * 1.60
		weights[&"trained_core_body_advance_3"] = float(weights.get(&"trained_core_body_advance_3", 0.0)) * 1.60
	if not is_ready(&"trained_core_body_sniper"):
		weights[&"trained_core_body_advance_1"] = float(weights.get(&"trained_core_body_advance_1", 0.0)) * 1.50
		weights[&"trained_core_body_advance_2"] = float(weights.get(&"trained_core_body_advance_2", 0.0)) * 1.50
		weights[&"trained_core_body_advance_3"] = float(weights.get(&"trained_core_body_advance_3", 0.0)) * 1.50
	return commit_action(adaptive_weighted_ids(context, weights))


func _base_weights(context: EnemyAIContext) -> Dictionary:
	match context.distance:
		1:
			return {&"trained_core_body_gunstock": 75.0, &"trained_core_body_teleport_guard": 25.0}
		2:
			return {&"trained_core_body_advance_1": 60.0 if can_move_toward(context, 1) else 0.0, &"trained_core_body_teleport_guard": 40.0}
		3, 4:
			return {&"trained_core_body_pulse": 55.0, &"trained_core_body_advance_1": 25.0 if can_move_toward(context, 1) else 0.0, &"trained_core_body_disable": 10.0 if context.player_has_unlit_cell else 0.0, &"trained_core_body_teleport_guard": 10.0}
		5:
			return {&"trained_core_body_advance_1": 45.0 if can_move_toward(context, 1) else 0.0, &"trained_core_body_advance_2": 20.0 if can_move_toward(context, 2) else 0.0, &"trained_core_body_disable": 20.0 if context.player_has_unlit_cell else 0.0, &"trained_core_body_teleport_guard": 15.0}
		6, 7, 8, 9, 10, 11, 12:
			return {&"trained_core_body_sniper": 65.0, &"trained_core_body_advance_3": 20.0 if can_move_toward(context, 3) else 0.0, &"trained_core_body_disable": 10.0 if context.player_has_unlit_cell else 0.0, &"trained_core_body_teleport_guard": 5.0}
		_:
			return {&"trained_core_body_advance_3": 70.0 if can_move_toward(context, 3) else 0.0, &"trained_core_body_teleport_guard": 20.0, &"trained_core_body_disable": 10.0 if context.player_has_unlit_cell else 0.0}


func _arm_initial_sniper_cooldown() -> void:
	if _initial_cooldown_armed:
		return
	_cooldowns[&"trained_core_body_sniper"] = sniper_initial_cooldown
	_initial_cooldown_armed = true


func _distance_bucket(distance: int) -> StringName:
	if distance <= 1: return &"close"
	if distance <= 4: return &"mid"
	return &"far"


func _highest_damage_bucket(metrics: Dictionary) -> StringName:
	var close := float(metrics.close_damage)
	var mid := float(metrics.mid_damage)
	var far := float(metrics.far_damage)
	if close >= mid and close >= far: return &"close"
	if mid >= far: return &"mid"
	return &"far"


func _on_action_confirmed(_action: EnemyActionData, context: EnemyAIContext) -> void:
	confirm_observable_history(context)
