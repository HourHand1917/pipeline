extends Buff
class_name DustBuff

func _init() -> void:
    id = "dust"
    buff_name = "蒙尘"
    polarity = BuffPolarity.NEGATIVE
    description = "该格点亮消耗额外能量"


func on_before_light(cell: CellRuntime, stacks: int) -> int:
    return stacks  # 额外消耗 = 层数


func can_light(cell: CellRuntime) -> bool:
    return true  # 不影响能否点亮