extends Resource
class_name EnemyAiProfile

## Inspector-editable enemy AI preset.  Three corresponding .tres resources can
## share the same EnermyMannager scene and only replace this profile.

enum BrainType {
	BOOM = 0,
	ROCKY = 1,
	SHARKK = 2,
}

@export_group("Identity")
@export var brain_type: BrainType = BrainType.BOOM
@export var enemy_id: StringName = &"boom"
@export var display_name: String = "boom"

@export_group("Combat")
@export_range(1, 9999, 1, "or_greater") var max_hp: int = 7
@export_range(0, 9999, 1, "or_greater") var initial_shield: int = 0

@export_group("Track")
@export_range(1, 99, 1, "or_greater") var start_cell: int = 6
@export_range(2, 99, 1, "or_greater") var cell_count: int = 7

@export_group("Decision Tuning")
@export_range(0, 999, 1, "or_greater") var reactive_damage_threshold: int = 10
@export_range(0.0, 1.0, 0.01) var defensive_hp_ratio: float = 0.35
@export var random_seed: int = 1001

@export_group("Actions")
@export var actions: Array[EnemyAiAction] = []


func clamped_start_cell() -> int:
	return clampi(start_cell, 1, maxi(2, cell_count))


func find_action(action_id: StringName) -> EnemyAiAction:
	for action: EnemyAiAction in actions:
		if action != null and action.runtime_id() == action_id:
			return action
	return null
