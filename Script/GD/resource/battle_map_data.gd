extends Resource
class_name BattleMapData

@export_group("Track")
@export_range(2, 99, 1, "or_greater") var cell_count: int = 7
@export_range(1, 99, 1, "or_greater") var player_start_cell: int = 2
@export_range(1, 99, 1, "or_greater") var enemy_start_cell: int = 6

@export_group("Movement And Distance")
@export_enum("向左:-1", "向右:1") var player_forward_direction: int = 1
@export var include_occupied_cells_in_distance: bool = true


func is_configuration_valid() -> bool:
	return (
		cell_count >= 2
		and is_valid_cell(player_start_cell)
		and is_valid_cell(enemy_start_cell)
		and player_start_cell != enemy_start_cell
		and player_forward_direction in [-1, 1]
	)


func is_valid_cell(cell: int) -> bool:
	return cell >= 1 and cell <= cell_count


func combat_distance(player_cell: int, enemy_cell: int) -> int:
	var occupied_cell_offset := 1 if include_occupied_cells_in_distance else 0
	return absi(enemy_cell - player_cell) + occupied_cell_offset