extends RefCounted
class_name CardRuntime

var instance_id: int
var data: CardData
var anchor_position: Vector2i
var rotation_steps: int = 0
var occupied_cells: Array[Vector2i] = []
var cooldown_remaining: int = 0
var is_ready: bool = false


func _init(
	new_instance_id: int,
	card_data: CardData,
	anchor: Vector2i,
	rotation: int,
	cells: Array[Vector2i]
) -> void:
	instance_id = new_instance_id
	data = card_data
	anchor_position = anchor
	rotation_steps = rotation
	occupied_cells = cells
