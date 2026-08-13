extends EnemyAIController
class_name BoomEnemyAI

@export_group("Boom tuning")
@export_range(0.0, 1.0, 0.05) var attack_chance_at_range_two: float = 0.60


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	if context.distance <= 1:
		return commit_action(first_available([&"boom_attack"], context))
	if context.distance == 2:
		var close_choice := weighted_ids(context, {
			&"boom_attack": attack_chance_at_range_two,
			&"boom_advance_1": 1.0 - attack_chance_at_range_two,
		})
		return commit_action(close_choice)
	return commit_action(weighted_ids(context, {
		&"boom_advance_2": 0.70,
		&"boom_advance_1": 0.30,
	}))
