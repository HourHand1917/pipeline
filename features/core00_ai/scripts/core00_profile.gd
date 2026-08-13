extends Resource
class_name Core00Profile

## All balance values that are not explicit in the design image remain exposed
## here so a designer can tune them from the Inspector without editing code.

@export_group("Identity and arena")
@export var boss_id: StringName = &"core00"
@export var display_name: String = "Core-00"
@export_range(4, 99, 1) var cell_count: int = 12
@export_range(1, 99, 1) var player_start_cell: int = 6

@export_group("Phase one - two hands")
@export_range(1, 99, 1) var true_hand_start_cell: int = 1
@export_range(1, 99, 1) var false_hand_start_cell: int = 12
@export_range(1, 9999, 1) var hand_max_health: int = 21
@export_range(0, 999, 1) var phase_one_beam_damage: int = 6
@export_range(0, 999, 1) var phase_one_shield_gain: int = 4
@export_range(0, 999, 1) var phase_one_shield_cap: int = 8
@export_range(0, 999, 1) var phase_one_heal_amount: int = 5

@export_group("Phase two - humanoid")
@export_range(1, 99, 1) var body_start_cell: int = 10
@export_range(1, 9999, 1) var body_max_health: int = 50
@export_range(0, 999, 1) var teleport_reaction_damage: int = 8
@export_range(0, 999, 1) var body_shield_cap: int = 12
@export_range(1, 99, 1) var jam_every_n_rounds: int = 4
@export_range(1, 99, 1) var jammed_card_turns: int = 1
@export var phase_two_actions: Array[Core00ActionData] = []


func find_action(action_id: StringName) -> Core00ActionData:
	for action: Core00ActionData in phase_two_actions:
		if action != null and action.runtime_id() == action_id:
			return action
	return null
