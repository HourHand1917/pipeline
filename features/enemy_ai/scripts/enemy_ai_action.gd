extends Resource
class_name EnemyAiAction

## One Inspector-editable enemy action.  The manager owns all runtime state
## (cooldowns, prepared dash, stun); this resource is immutable configuration.

enum ActionType {
	WAIT = 0,
	ATTACK = 1,
	ADVANCE = 2,
	RETREAT = 3,
	DEFEND_RETREAT = 4,
	PREPARE_DASH = 5,
	DASH = 6,
	DUST_RETREAT = 7,
}

@export_group("Identity")
@export var id: StringName = &"enemy_action"
@export var display_name: String = "Enemy action"
@export_multiline var intent_text: String = ""
@export var action_type: ActionType = ActionType.WAIT

@export_group("Availability")
@export_range(0, 99, 1, "or_greater") var min_range: int = 0
@export_range(0, 99, 1, "or_greater") var max_range: int = 99
@export_range(0, 99, 1, "or_greater") var cooldown_turns: int = 0
@export_range(0, 999, 1, "or_greater") var requires_damage_taken_at_least: int = 0
@export var requires_prepared_dash: bool = false

@export_group("Effects")
@export_range(0, 999, 1, "or_greater") var damage: int = 0
@export_range(0, 999, 1, "or_greater") var shield: int = 0
@export_range(0, 99, 1, "or_greater") var move_toward: int = 0
@export_range(0, 99, 1, "or_greater") var move_away: int = 0
@export_range(0, 99, 1, "or_greater") var push_player: int = 0
@export var sets_prepared_dash: bool = false
@export_range(0, 99, 1, "or_greater") var stun_self_turns: int = 0
@export var applies_dust: bool = false

@export_group("Selection")
@export var base_priority: int = 0
@export_range(0.0, 1000.0, 0.05, "or_greater") var random_weight: float = 1.0


func normalized_min_range() -> int:
	return mini(min_range, max_range)


func normalized_max_range() -> int:
	return maxi(min_range, max_range)


func is_in_range(distance: int) -> bool:
	return distance >= normalized_min_range() and distance <= normalized_max_range()


func runtime_id() -> StringName:
	if not id.is_empty():
		return id
	if not display_name.is_empty():
		return StringName(display_name)
	return StringName("action_%d" % action_type)
