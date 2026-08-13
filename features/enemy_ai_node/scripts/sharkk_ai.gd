extends EnemyAIController
class_name SharkkEnemyAI

@export_group("Sharkk tuning")
@export_range(1, 30, 1) var adapt_damage_threshold: int = 10
@export_range(0.0, 1.0, 0.05) var attack_priority_chance: float = 0.72
@export_range(0.0, 1.0, 0.05) var charge_chance: float = 0.70
var _prepared_charge: bool = false


func reset_ai() -> void:
	super.reset_ai()
	_prepared_charge = false


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	# A 10+ hit always forces sand + retreat, as specified by the design table.
	if context.damage_taken_last_turn >= adapt_damage_threshold:
		var sand_retreat := first_available([&"sharkk_sand_retreat"], context)
		if sand_retreat != null:
			return commit_action(sand_retreat, 2)
	# Successful preparation guarantees the charge next turn if it is legal.
	if _prepared_charge:
		var prepared := first_available([&"sharkk_charge"], context)
		if prepared != null:
			return commit_action(prepared, 3)
	if context.distance <= 2 and random_chance(attack_priority_chance):
		return commit_action(first_available([&"sharkk_punch", &"sharkk_attack"], context))
	if context.distance >= 2 and context.distance <= 4 and is_ready(&"sharkk_charge") and random_chance(charge_chance):
		var prepare := first_available([&"sharkk_prepare_charge"], context)
		if prepare != null:
			return commit_action(prepare)
	if context.last_player_action_type in [&"gun", &"ranged"]:
		var anti_gun := first_available([&"sharkk_advance", &"sharkk_charge"], context)
		if anti_gun != null:
			return commit_action(anti_gun, 3 if anti_gun.id == &"sharkk_charge" else 0)
	return commit_action(first_available([&"sharkk_advance", &"sharkk_attack", &"sharkk_sand_retreat"], context))


func _on_action_confirmed(action: EnemyActionData, _context: EnemyAIContext) -> void:
	match action.id:
		&"sharkk_prepare_charge": _prepared_charge = true
		&"sharkk_charge", &"sharkk_sand_retreat": _prepared_charge = false
