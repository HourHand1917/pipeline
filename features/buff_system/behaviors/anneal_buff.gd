extends Buff
class_name AnnealBuff

@export_group("Mounted Combat Effects")
@export var apply_effects: Array[PipelineCombatEffectData] = []


func _init() -> void:
	id = "anneal"
	buff_name = "退火"
	polarity = BuffPolarity.POSITIVE
	description = "施加时解除全部卡牌冷却"


func on_apply(_stats: Stats, _stacks: int) -> void:
	pass


func get_effects_for_phase(phase: StringName) -> Array[PipelineCombatEffectData]:
	if phase == &"apply":
		return apply_effects
	var empty: Array[PipelineCombatEffectData] = []
	return empty


func consume_after_apply() -> bool:
	return true


func get_decay_phase() -> StringName:
	return &""


func decay_uses_stacks() -> bool:
	return false
