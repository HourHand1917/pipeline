extends EnemyAIController
class_name RockyEnemyAI

@export_group("Rocky tuning")
@export_range(1, 20, 1) var heavy_hit_threshold: int = 7
@export_range(0.0, 1.0, 0.05) var midrange_attack_chance: float = 0.55
@export_range(0.0, 1.0, 0.05) var retreat_chance_after_heavy_hit: float = 0.75


func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	if context.damage_taken_last_turn >= heavy_hit_threshold:
		var evade := weighted_ids(context, {
			&"rocky_retreat": retreat_chance_after_heavy_hit,
			&"rocky_defend": 1.0 - retreat_chance_after_heavy_hit,
		})
		if evade != null:
			return commit_action(evade)
	if context.distance == 1:
		return commit_action(weighted_ids(context, {
			&"rocky_close_attack": 0.70,
			&"rocky_defend": 0.30,
		}))
	if context.distance <= 3:
		return commit_action(weighted_ids(context, {
			&"rocky_mid_attack": midrange_attack_chance,
			&"rocky_advance_1": 0.25,
			&"rocky_defend": maxf(0.05, 0.75 - midrange_attack_chance),
		}))
	return commit_action(weighted_ids(context, {
		&"rocky_advance_2": 0.70,
		&"rocky_advance_1": 0.30,
	}))
