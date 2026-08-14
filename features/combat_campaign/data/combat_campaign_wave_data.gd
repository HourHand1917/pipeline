extends Resource
class_name CombatCampaignWaveData

@export_group("Identity")
@export_range(1, 99, 1, "or_greater") var battle_number: int = 1
@export_range(1, 99, 1, "or_greater") var wave_number: int = 1
@export var id: StringName = &"battle_1_wave_1"
@export var display_name: String = "Battle 1"

@export_group("Battle")
@export var game_rules: GameRules
@export var show_build_before: bool = true
@export var preserve_player_state: bool = false
@export var preserve_board_runtime_state: bool = false


func is_configuration_valid() -> bool:
	if battle_number <= 0 or wave_number <= 0:
		return false
	if id.is_empty() or game_rules == null:
		return false
	return game_rules.is_configuration_valid()

