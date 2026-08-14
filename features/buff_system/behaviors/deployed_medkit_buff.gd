extends Buff
class_name DeployedMedkitBuff

@export_group("Mounted Combat Effects")
@export var turn_start_effects: Array[PipelineCombatEffectData] = []


func _init() -> void:
	id = "deployed_medkit"
	buff_name = "部署治疗包"
	polarity = BuffPolarity.POSITIVE
	description = "下回合开始恢复等同层数的生命"


func on_turn_start(_stats: Stats, _stacks: int) -> void:
	pass


func get_effects_for_phase(phase: StringName) -> Array[PipelineCombatEffectData]:
	if phase == &"turn_start":
		return turn_start_effects
	var empty: Array[PipelineCombatEffectData] = []
	return empty


func consume_after_phase(phase: StringName) -> bool:
	return phase == &"turn_start"


func get_decay_phase() -> StringName:
	return &""


func decay_uses_stacks() -> bool:
	return false
