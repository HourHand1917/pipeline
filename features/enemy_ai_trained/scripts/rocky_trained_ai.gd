extends AdaptiveEnemyAIController
class_name RockyTrainedAI

@export_group("Rocky response")
@export_range(1, 20, 1) var heavy_hit_threshold: int = 7
@export_range(1.0, 3.0, 0.05) var defence_after_burst: float = 1.80
@export_range(1.0, 3.0, 0.05) var retreat_after_burst: float = 1.50


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	var metrics := observable_metrics(context)
	var heavy_pressure := context.damage_taken_last_turn >= heavy_hit_threshold or float(metrics.burst) >= 6.0
	var weights: Dictionary
	match context.distance:
		1:
			weights = {&"trained_rocky_close_attack": 60.0, &"trained_rocky_mid_attack": 25.0, &"trained_rocky_defend": 15.0 if can_move_away(context, 1) else 0.0}
		2:
			weights = {&"trained_rocky_mid_attack": 35.0, &"trained_rocky_advance_1": 35.0 if can_move_toward(context, 1) else 0.0, &"trained_rocky_defend": 15.0 if can_move_away(context, 1) else 0.0, &"trained_rocky_retreat": 15.0 if can_move_away(context, 2) else 0.0}
		3:
			weights = {&"trained_rocky_mid_attack": 40.0, &"trained_rocky_advance_2": 30.0 if can_move_toward(context, 2) else 0.0, &"trained_rocky_advance_1": 10.0 if can_move_toward(context, 1) else 0.0, &"trained_rocky_defend": 10.0 if can_move_away(context, 1) else 0.0, &"trained_rocky_retreat": 10.0 if can_move_away(context, 2) else 0.0}
		_:
			weights = {&"trained_rocky_advance_2": 55.0 if can_move_toward(context, 2) else 0.0, &"trained_rocky_advance_1": 25.0 if can_move_toward(context, 1) else 0.0, &"trained_rocky_defend": 10.0 if can_move_away(context, 1) else 0.0, &"trained_rocky_retreat": 10.0 if can_move_away(context, 2) else 0.0}
	if heavy_pressure:
		weights[&"trained_rocky_defend"] = float(weights.get(&"trained_rocky_defend", 0.0)) * defence_after_burst
		weights[&"trained_rocky_retreat"] = float(weights.get(&"trained_rocky_retreat", 0.0)) * retreat_after_burst
	if float(metrics.close_damage) > maxf(float(metrics.mid_damage), float(metrics.far_damage)):
		weights[&"trained_rocky_defend"] = float(weights.get(&"trained_rocky_defend", 0.0)) * 1.35
		weights[&"trained_rocky_retreat"] = float(weights.get(&"trained_rocky_retreat", 0.0)) * 1.35
	elif float(metrics.far_damage) > float(metrics.close_damage):
		weights[&"trained_rocky_advance_1"] = float(weights.get(&"trained_rocky_advance_1", 0.0)) * 1.50
		weights[&"trained_rocky_advance_2"] = float(weights.get(&"trained_rocky_advance_2", 0.0)) * 1.50
	return commit_action(adaptive_weighted_ids(context, weights))


func _on_action_confirmed(_action: EnemyActionData, context: EnemyAIContext) -> void:
	confirm_observable_history(context)
