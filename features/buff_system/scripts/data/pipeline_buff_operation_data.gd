extends Resource
class_name PipelineBuffOperationData

enum Type {
	CLEANSE_ALL_NEGATIVE,
	RESET_ALL_CARD_COOLDOWNS,
}

@export var id: StringName
@export var display_name: String = ""
@export_multiline var description: String = ""
@export var type: Type = Type.CLEANSE_ALL_NEGATIVE
@export var include_board_cells: bool = true


func execute(runtime: PipelineBuffRuntime, context: PipelineBuffContext) -> bool:
	if runtime == null or context == null:
		return false
	match type:
		Type.CLEANSE_ALL_NEGATIVE:
			runtime.cleanse_all_negative(
				context.host,
				context.board_manager if include_board_cells else null
			)
			return true
		Type.RESET_ALL_CARD_COOLDOWNS:
			return runtime.reset_all_card_cooldowns(
				context.board_manager,
				context.host
			) > 0
	return false
