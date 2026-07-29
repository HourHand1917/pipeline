extends Resource
class_name EnemyData

@export_group("Identity")
@export var id: StringName = &"enemy"
@export var display_name: String = "训练怪"
@export var subtitle: String = "TRAINING FOE"
@export var glyph: String = "怪"
@export var tint: Color = Color("#e66c62")

@export_group("Combat")
@export_range(1, 999, 1, "or_greater") var max_hp: int = 30
@export_range(0, 999, 1, "or_greater") var initial_shield: int = 0

@export_group("Actions")
@export var actions: Array[EnemyActionData] = []


func get_action_for_distance(distance: int) -> EnemyActionData:
	var selected: EnemyActionData = null
	for action in actions:
		if action == null or not action.is_available(distance):
			continue
		if selected == null or action.priority > selected.priority:
			selected = action
	return selected