extends TrueBuff
class_name CoreTrueLoopBuff

## Core-00's internal True flag is removed by False's destruction beam rather
## than by ordinary turn decay. It keeps the same damage modifier contract and
## the same `true` id, so FalseBuff's existing mutual-cancellation rule applies.


func _init() -> void:
	super._init()
	description = "Core-00死循环标记；仅由False破坏光束移除。"


func get_decay_phase() -> StringName:
	return &""


func decay_uses_stacks() -> bool:
	return false
