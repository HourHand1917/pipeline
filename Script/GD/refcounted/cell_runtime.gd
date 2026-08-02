extends RefCounted
class_name CellRuntime

var position: Vector2i
var card_instance_id: int = -1
var local_shape_index: int = -1
var is_lit: bool = false
var stats: Stats = Stats.new()        # 每个格子有独立的 Stats 容器


func _init(cell_position: Vector2i) -> void:
    position = cell_position
    stats = Stats.new()