extends Resource
class_name EnemyActionData

@export_group("Identity")
@export var id: StringName = &""
@export var display_name: String = ""
@export var intent_text: String = ""
@export var priority: int = 0

@export_group("Decision Tuning")
## Relative weight used by node-based AI.  Legacy priority selection remains
## available for old EnemyData resources.
@export_range(0.0, 999.0, 0.05, "or_greater") var base_weight: float = 1.0
## Multiplier applied when this was the immediately preceding action.
@export_range(0.0, 1.0, 0.01) var repeat_penalty: float = 0.18
## Number of following enemy turns during which this action is unavailable.
@export_range(0, 99, 1, "or_greater") var cooldown_turns: int = 0
@export var tags: PackedStringArray = PackedStringArray()

@export_group("Availability")
@export_range(1, 99, 1, "or_greater") var min_range: int = 2
@export_range(1, 99, 1, "or_greater") var max_range: int = 99

@export_group("Effects")
@export var effects: Array[CombatEffectData] = []

@export_group("Runtime Contract")
## Optional semantic hook consumed by the C# battle executor.  Ordinary
## actions leave this empty and execute their `effects` array unchanged.
@export var special_effect: StringName = &""
## `self`, `player`, or an encounter role such as `false_hand`.
@export var effect_target_role: StringName = &"self"
@export_range(0, 999, 1, "or_greater") var special_value: int = 0
## When true, the battle executor must not also replay the legacy effects.
@export var special_replaces_effects: bool = false
## Optional pure-data telegraph profile consumed by DangerAreaPredictor.
@export var danger_profile: Resource


func is_available(distance: int) -> bool:
	var range_min := mini(min_range, max_range)
	var range_max := maxi(min_range, max_range)
	return distance >= range_min and distance <= range_max
