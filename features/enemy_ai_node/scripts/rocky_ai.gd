extends EnemyAIController
class_name RockyEnemyAI

@export_group("Rocky tuning")
@export_range(1, 20, 1) var heavy_hit_threshold: int = 7
@export_range(0.0, 1.0, 0.05) var midrange_attack_chance: float = 0.55
@export_range(0.0, 1.0, 0.05) var retreat_chance_after_heavy_hit: float = 0.75


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	if context.damage_taken_last_turn >= heavy_hit_threshold:
		if random_chance(retreat_chance_after_heavy_hit):
			var evade := first_available([&"rocky_retreat", &"rocky_defend"], context)
			if evade != null:
				return commit_action(evade, 2 if evade.id == &"rocky_defend" else 3)
	if context.distance == 1:
		var close_attack := first_available([&"rocky_close_attack", &"rocky_defend", &"rocky_mid_attack"], context)
		var close_cooldown := 0
		if close_attack != null and close_attack.id in [&"rocky_close_attack", &"rocky_defend"]:
			close_cooldown = 2
		return commit_action(close_attack, close_cooldown)
	if context.distance <= 3 and random_chance(midrange_attack_chance):
		return commit_action(first_available([&"rocky_mid_attack", &"rocky_advance_1"], context))
	return commit_action(first_available([&"rocky_advance_2", &"rocky_advance_1", &"rocky_mid_attack"], context))
