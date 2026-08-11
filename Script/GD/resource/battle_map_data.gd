extends Resource
class_name BattleMapData

@export_group("Track")
@export_range(2, 99, 1, "or_greater") var cell_count: int = 7
@export_range(1, 99, 1, "or_greater") var player_start_cell: int = 2

@export_group("Enemies")
@export var enemy_data_list: Array[EnemyData] = []
@export var enemy_start_cells: Array[int] = []

@export_group("Movement And Distance")
@export_enum("向左:-1", "向右:1") var player_forward_direction: int = 1
@export var include_occupied_cells_in_distance: bool = true


func is_configuration_valid() -> bool:
	if cell_count < 2:
		return false
	if not is_valid_cell(player_start_cell):
		return false
	if enemy_data_list.is_empty():
		return false
	if enemy_start_cells.size() != enemy_data_list.size():
		return false
	for cell in enemy_start_cells:
		if not is_valid_cell(cell):
			return false
		if cell == player_start_cell:
			return false
	if player_forward_direction not in [-1, 1]:
		return false
	return true


func is_valid_cell(cell: int) -> bool:
	return cell >= 1 and cell <= cell_count


func combat_distance(player_cell: int, enemy_cell: int) -> int:
	var occupied_cell_offset := 1 if include_occupied_cells_in_distance else 0
	return absi(enemy_cell - player_cell) + occupied_cell_offset