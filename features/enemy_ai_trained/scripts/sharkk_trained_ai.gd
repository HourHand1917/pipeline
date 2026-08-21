extends AdaptiveEnemyAIController
class_name SharkkTrainedAI

@export_group("Sharkk response")
@export_range(1, 30, 1) var forced_dust_damage: int = 10
@export_range(1.0, 3.0, 0.05) var dash_ready_multiplier: float = 1.35
@export_range(1.0, 3.0, 0.05) var ranged_chase_multiplier: float = 1.80
@export_range(1.0, 3.0, 0.05) var mid_pressure_dust_multiplier: float = 1.80

enum ChargeState { NEUTRAL, PREPARED, RECOVERING }
var charge_state: ChargeState = ChargeState.NEUTRAL


func reset_ai() -> void:
	super.reset_ai()
	charge_state = ChargeState.NEUTRAL


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	if charge_state == ChargeState.RECOVERING:
		return commit_action(action_by_id(&"trained_sharkk_stunned"))
	if charge_state == ChargeState.PREPARED:
		var prepared_dash := _dash_action(context)
		if prepared_dash != null:
			return commit_action(prepared_dash)
		if context.distance <= 3 and can_move_away(context, 1):
			return commit_action(action_by_id(&"trained_sharkk_prepare_charge"))
		return commit_action(action_by_id(&"trained_sharkk_advance") if can_move_toward(context, 1) else null)
	var sand := action_by_id(&"trained_sharkk_sand_retreat")
	if context.damage_taken_last_turn >= forced_dust_damage and context.distance >= 2 and context.distance <= 3 and sand != null and is_ready(sand.id) and can_move_away(context, 2):
		return commit_action(sand)
	var metrics := observable_metrics(context)
	var weights := _base_weights(context)
	# Charge actions are deliberately absent while NEUTRAL.  A confirmed
	# prepare action is the only transition into PREPARED, so Sharkk can never
	# skip the visible telegraph and charge directly.
	if _prepare_is_legal(context):
		weights[&"trained_sharkk_prepare_charge"] = float(weights.get(&"trained_sharkk_prepare_charge", 0.0)) * dash_ready_multiplier
	if float(metrics.far_damage) > maxf(float(metrics.close_damage), float(metrics.mid_damage)) or float(metrics.retreat_rate) >= 0.50 or float(metrics.ranged_rate) >= 0.50:
		weights[&"trained_sharkk_advance"] = float(weights.get(&"trained_sharkk_advance", 0.0)) * ranged_chase_multiplier
		if _prepare_is_legal(context):
			weights[&"trained_sharkk_prepare_charge"] = float(weights.get(&"trained_sharkk_prepare_charge", 0.0)) * 1.50
	if float(metrics.mid_damage) > maxf(float(metrics.close_damage), float(metrics.far_damage)):
		weights[&"trained_sharkk_sand_retreat"] = float(weights.get(&"trained_sharkk_sand_retreat", 0.0)) * mid_pressure_dust_multiplier
	return commit_action(adaptive_weighted_ids(context, weights))


func _base_weights(context: EnemyAIContext) -> Dictionary:
	var weights: Dictionary = {}
	match context.distance:
		1:
			weights = {&"trained_sharkk_attack": 25.0, &"trained_sharkk_prepare_charge": 75.0 if _prepare_is_legal(context) else 0.0}
		2:
			weights = {&"trained_sharkk_attack": 25.0, &"trained_sharkk_prepare_charge": 65.0 if _prepare_is_legal(context) else 0.0, &"trained_sharkk_sand_retreat": 10.0 if can_move_away(context, 2) else 0.0}
		3:
			weights = {&"trained_sharkk_prepare_charge": 65.0 if _prepare_is_legal(context) else 0.0, &"trained_sharkk_advance": 25.0 if can_move_toward(context, 1) else 0.0, &"trained_sharkk_sand_retreat": 10.0 if can_move_away(context, 2) else 0.0}
		4:
			weights = {&"trained_sharkk_advance": 100.0 if can_move_toward(context, 1) else 0.0}
		_:
			weights = {&"trained_sharkk_advance": 100.0 if can_move_toward(context, 1) else 0.0}
	return weights


func _dash_action(context: EnemyAIContext) -> EnemyActionData:
	if not is_exact_fifth_cell_charge_legal(context):
		return null
	var action := action_by_id(StringName("trained_sharkk_charge_d%d" % context.distance))
	if action == null or not is_ready(action.id):
		return null
	return action


func _prepare_is_legal(context: EnemyAIContext) -> bool:
	if context.distance < 1 or context.distance > 3 or not can_move_away(context, 1):
		return false
	var projected := EnemyAIContext.new()
	projected.update_from_dictionary({"enemy_position": context.enemy_position - signi(context.player_position - context.enemy_position), "player_position": context.player_position, "distance": context.distance + 1})
	return is_exact_fifth_cell_charge_legal(projected)


func _on_action_confirmed(action: EnemyActionData, context: EnemyAIContext) -> void:
	confirm_observable_history(context)
	if action == null:
		return
	if action.id == &"trained_sharkk_prepare_charge":
		charge_state = ChargeState.PREPARED
	elif String(action.id).begins_with("trained_sharkk_charge_d"):
		charge_state = ChargeState.RECOVERING
		for distance in range(1, 5):
			_cooldowns[StringName("trained_sharkk_charge_d%d" % distance)] = action.cooldown_turns + 1
	elif action.id == &"trained_sharkk_stunned":
		charge_state = ChargeState.NEUTRAL
	elif charge_state == ChargeState.PREPARED:
		# If the prepared dash became illegal and Sharkk had to do something
		# else, consume the preparation.  A later charge must telegraph again.
		charge_state = ChargeState.NEUTRAL
