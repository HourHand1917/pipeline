extends Buff
class_name DustBuff


func _init() -> void:
	id = "dust"
	buff_name = "蒙尘"
	polarity = BuffPolarity.NEGATIVE
	description = "该格点亮消耗额外能量"


func on_before_light(cell: CellRuntime, stacks: int) -> int:
	return stacks


func can_light(cell: CellRuntime) -> bool:
	return true


func on_turn_end(_stats: Stats, _stacks: int) -> void:
	# Stats owns the actual one-stack decay.  This hook documents the exact
	# settlement phase required by the design sheet.
	pass


func get_effects_for_phase(_phase: StringName) -> Array[PipelineCombatEffectData]:
	var empty: Array[PipelineCombatEffectData] = []
	return empty


func get_decay_phase() -> StringName:
	return &"turn_end"


func decay_uses_stacks() -> bool:
	return true
