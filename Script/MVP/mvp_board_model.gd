class_name MvpBoardModel
extends RefCounted

var width: int
var height: int
var catalog: Resource
var entries: Array = []

func _init(board_width: int, board_height: int, card_catalog: Resource, source_entries: Array = []) -> void:
	width = board_width
	height = board_height
	catalog = card_catalog
	for source in source_entries:
		entries.append(source.duplicate(true))

func rotated_shape(card: Resource, rotation: int) -> Array[Vector2i]:
	var points: Array[Vector2i] = []
	if card == null:
		return points
	for source in card.shape:
		var point: Vector2i = source
		for _step in range(posmod(rotation, 4)):
			point = Vector2i(-point.y, point.x)
		points.append(point)
	if points.is_empty():
		points.append(Vector2i.ZERO)
	var min_x := points[0].x
	var min_y := points[0].y
	for point in points:
		min_x = mini(min_x, point.x)
		min_y = mini(min_y, point.y)
	for index in range(points.size()):
		points[index] -= Vector2i(min_x, min_y)
	return points

func cells_for_entry(entry: Dictionary) -> Array[Vector2i]:
	var result: Array[Vector2i] = []
	var card: Resource = catalog.call("get_card", StringName(entry.get("card_id", "")))
	if card == null:
		return result
	var anchor: Vector2i = _to_vec2i(entry.get("anchor", [0, 0]))
	for offset in rotated_shape(card, int(entry.get("rotation", 0))):
		result.append(anchor + offset)
	return result

func can_place(card_id: StringName, anchor: Vector2i, rotation: int, ignore_index: int = -1) -> bool:
	var card: Resource = catalog.call("get_card", card_id)
	if card == null:
		return false
	var occupied: Dictionary = {}
	for index in range(entries.size()):
		if index == ignore_index:
			continue
		for cell in cells_for_entry(entries[index]):
			occupied[cell] = true
	for offset in rotated_shape(card, rotation):
		var cell: Vector2i = anchor + offset
		if cell.x < 0 or cell.y < 0 or cell.x >= width or cell.y >= height:
			return false
		if occupied.has(cell):
			return false
	return true

func add_entry(card_id: StringName, anchor: Vector2i, rotation: int) -> bool:
	if not can_place(card_id, anchor, rotation):
		return false
	entries.append({
		"card_id": String(card_id),
		"anchor": [anchor.x, anchor.y],
		"rotation": posmod(rotation, 4),
	})
	return true

func entry_index_at(cell: Vector2i) -> int:
	for index in range(entries.size()):
		if cell in cells_for_entry(entries[index]):
			return index
	return -1

func remove_at(cell: Vector2i) -> bool:
	var index: int = entry_index_at(cell)
	if index < 0:
		return false
	entries.remove_at(index)
	return true

func occupied_cells() -> Dictionary:
	var result: Dictionary = {}
	for index in range(entries.size()):
		for cell in cells_for_entry(entries[index]):
			result[cell] = index
	return result

func _to_vec2i(value: Variant) -> Vector2i:
	if value is Vector2i:
		return value
	if value is Array and value.size() >= 2:
		return Vector2i(int(value[0]), int(value[1]))
	if value is Dictionary:
		return Vector2i(int(value.get("x", 0)), int(value.get("y", 0)))
	return Vector2i.ZERO
