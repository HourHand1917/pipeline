extends RefCounted
class_name CellRuntime

var position: Vector2i
var card_instance_id: int = -1
var local_shape_index: int = -1
var is_lit: bool = false


func _init(cell_position: Vector2i) -> void:
	position = cell_position
