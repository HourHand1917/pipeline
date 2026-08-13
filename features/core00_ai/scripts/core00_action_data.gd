extends Resource
class_name Core00ActionData

## Core-00 phase-two action data.  These resources are read-only templates;
## runtime cooldown state lives in Core00EnermyMannager, so multiple boss
## instances can safely share the same .tres files.

enum ActionKind {
	ATTACK,
	MOVE,
	TELEPORT_DEFEND,
	JAM_CARDS,
	WAIT,
}

@export_group("Identity")
@export var action_id: StringName
@export var display_name: String = "Action"
@export_multiline var intent_text: String = ""
@export var action_kind: ActionKind = ActionKind.WAIT

@export_group("Range and damage")
@export_range(0, 99, 1) var min_range: int = 0
@export_range(0, 99, 1) var max_range: int = 99
@export_range(0, 999, 1) var damage_per_hit: int = 0
@export_range(1, 99, 1) var hit_count: int = 1

@export_group("Movement and defense")
@export_range(0, 99, 1) var move_min: int = 0
@export_range(0, 99, 1) var move_max: int = 0
@export_range(0, 999, 1) var shield: int = 0

@export_group("Reuse")
@export_range(0, 99, 1) var cooldown_turns: int = 0


func runtime_id() -> StringName:
	return action_id if not action_id.is_empty() else StringName(resource_path.get_file().get_basename())


func is_in_range(distance: int) -> bool:
	return distance >= min_range and distance <= max_range


func total_damage() -> int:
	return maxi(0, damage_per_hit) * maxi(1, hit_count)
