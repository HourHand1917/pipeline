extends Resource
class_name EnemyActionData

@export_group("Identity")
@export var id: StringName = &""
@export var display_name: String = ""
@export var intent_text: String = ""
@export var priority: int = 0

@export_group("Availability")
@export_range(1, 99, 1, "or_greater") var min_range: int = 2
@export_range(1, 99, 1, "or_greater") var max_range: int = 99

@export_group("Effects")
@export var effects: Array[CombatEffectData] = []


func is_available(distance: int) -> bool:
	var range_min := mini(min_range, max_range)
	var range_max := maxi(min_range, max_range)
	return distance >= range_min and distance <= range_max