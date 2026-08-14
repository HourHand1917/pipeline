extends CombatEffectData
class_name PipelineCombatEffectData

## Extra data-only operations required by the approved Buff and item sheet.
## It remains a CombatEffectData, so the same EffectResolver pipeline handles
## cards, enemy actions, Buff triggers, and items.

enum PipelineOperation {
	STANDARD,
	ADD_DAMAGE_BY_BUFF_STACKS,
	HALF_DAMAGE_FLOOR,
	ADD_HALF_DAMAGE_CEIL,
	SET_INCOMING_DAMAGE_TO_ONE,
	RESET_ALL_CARD_COOLDOWNS,
	SET_ALL_CARD_COOLDOWN,
	DRAIN_PLAYER_ENERGY,
	HEAL_OWNER_BY_BUFF_STACKS,
	MOVE_PLAYER_FORWARD_ONE,
	DAMAGE_FARTHEST_ENEMY,
}

@export_group("Pipeline Operation")
@export var pipeline_operation: PipelineOperation = PipelineOperation.STANDARD


func pipeline_operation_key() -> StringName:
	return StringName(PipelineOperation.keys()[pipeline_operation].to_lower())
