extends Buff
class_name DisabledBuff

func _init() -> void:
    id = "disabled"
    buff_name = "失效"
    polarity = BuffPolarity.NEGATIVE
    description = "该格无法点亮"


func can_light(cell: CellRuntime) -> bool:
    return false


func on_before_light(cell: CellRuntime, stacks: int) -> int:
    return 0