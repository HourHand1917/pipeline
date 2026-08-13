extends EnemyAIController
class_name BoomEnemyAI

@export_group("Boom tuning")
@export_range(0.0, 1.0, 0.05) var attack_chance_at_range_two: float = 0.60


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	if context.distance <= 1:
		return commit_action(first_available([&"boom_attack"], context))
	if context.distance == 2 and random_chance(attack_chance_at_range_two):
		return commit_action(first_available([&"boom_attack", &"boom_advance_1"], context))
	return commit_action(first_available([&"boom_advance_2", &"boom_advance_1", &"boom_attack"], context))
