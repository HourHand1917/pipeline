extends RefCounted
class_name BuffInstance

var buff: Buff
var stacks: int
var remaining_duration: int

func _init(p_buff: Buff, p_stacks: int = 1) -> void:
    buff = p_buff
    stacks = min(p_stacks, p_buff.max_stacks)
    remaining_duration = p_buff.duration

func add_stacks(amount: int) -> void:
    if buff != null and buff.stackable:
        stacks = min(stacks + amount, buff.max_stacks)

func tick_duration() -> bool:
    # 返回 true 表示已过期
    if remaining_duration <= 0:
        return false
    remaining_duration -= 1
    return remaining_duration <= 0