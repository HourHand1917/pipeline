extends Buff
class_name DisabledBuff

@export_group("Mounted Combat Effects")
@export var turn_start_effects: Array[PipelineCombatEffectData] = []


func _init() -> void:
	id = "disabled"
	buff_name = "禁用"
	polarity = BuffPolarity.NEGATIVE
	description = "回合开始时令全部卡牌进入冷却。"


func can_light(_cell: CellRuntime) -> bool:
	return true


func on_before_light(_cell: CellRuntime, _stacks: int) -> int:
	return 0


func on_turn_start(_stats: Stats, _stacks: int) -> void:
	pass


func on_turn_end(_stats: Stats, _stacks: int) -> void:
	pass


func get_effects_for_phase(phase: StringName) -> Array[PipelineCombatEffectData]:
	if phase == &"turn_start":
		return turn_start_effects
	var empty: Array[PipelineCombatEffectData] = []
	return empty


func get_decay_phase() -> StringName:
	return &"turn_end"


func decay_uses_stacks() -> bool:
	return true
