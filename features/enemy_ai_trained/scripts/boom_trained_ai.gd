extends AdaptiveEnemyAIController
class_name BoomTrainedAI

func select_action(context: EnemyAIContext) -> EnemyActionData:
	_begin_decision(context)
	if context.distance <= 1:
		return commit_action(action_by_id(&"trained_boom_attack_1"))
	if context.distance == 2:
		return commit_action(action_by_id(&"trained_boom_attack_2"))
	if context.distance >= 4 and can_move_toward(context, 2):
		return commit_action(adaptive_weighted_ids(context, {
			&"trained_boom_advance_2": 80.0,
			&"trained_boom_advance_1": 20.0,
		}))
	return commit_action(action_by_id(&"trained_boom_advance_1") if can_move_toward(context, 1) else null)


func _on_action_confirmed(_action: EnemyActionData, context: EnemyAIContext) -> void:
	confirm_observable_history(context)
