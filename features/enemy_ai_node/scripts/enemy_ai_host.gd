extends Node
class_name EnemyAIHost

signal intent_selected(action: EnemyActionData, context: EnemyAIContext)

## Both fields are drag-and-drop slots in the Inspector.  `ai_controller` may
## be an instanced child scene; `enemy_data` may be one of the adapter .tres.
@export_group("Plug-in configuration")
@export var enemy_data: EnemyDataNodeAdapter
@export var ai_controller: EnemyAIController

var context := EnemyAIContext.new()


func _ready() -> void:
	if ai_controller == null:
		for child in get_children():
			if child is EnemyAIController:
				ai_controller = child
				break


func configure(data: EnemyDataNodeAdapter, controller: EnemyAIController = null) -> void:
	enemy_data = data
	if controller != null:
		ai_controller = controller


func update_context(values: Dictionary) -> void:
	context.update_from_dictionary(values)
	if enemy_data != null:
		enemy_data.set_ai_runtime_context(values)


func choose_intent(values: Dictionary = {}) -> EnemyActionData:
	update_context(values)
	var provider := ai_controller
	if provider == null and enemy_data != null:
		provider = enemy_data.get_ai_provider() as EnemyAIController
	if provider == null:
		return null
	var action := provider.select_action(context)
	intent_selected.emit(action, context)
	return action

