extends Resource
class_name GameRules

@export_group("Content")
@export var player_data: PlayerData
@export var battle_map: BattleMapData

@export_group("Turn Economy")
@export_range(0, 99, 1, "or_greater") var energy_per_turn: int = 3
@export_range(0, 99, 1, "or_greater") var charge_energy_cost: int = 1
@export_range(0, 99, 1, "or_greater") var move_energy_cost: int = 1
@export_range(1, 99, 1, "or_greater") var player_move_step: int = 1
@export var preserve_partial_charge_between_turns: bool = true

@export_group("Loadout Boards")
@export var board_sizes: Array[Vector2i] = [
	Vector2i(3, 2),
	Vector2i(3, 3),
	Vector2i(4, 3),
]


func is_configuration_valid() -> bool:
	if player_data == null or battle_map == null:
		return false
	if not battle_map.is_configuration_valid():
		return false
	if board_sizes.is_empty():
		return false
	for board_size in board_sizes:
		if board_size.x <= 0 or board_size.y <= 0:
			return false
	return true


func is_valid_board_size(board_size: Vector2i) -> bool:
	return board_sizes.has(board_size)