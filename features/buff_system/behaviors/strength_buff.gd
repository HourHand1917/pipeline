extends Buff
class_name StrengthBuff

@export_group("Mounted Combat Effects")
@export var outgoing_damage_effects: Array[PipelineCombatEffectData] = []


func _init() -> void:
	id = "strength"
	buff_name = "力量"
	polarity = BuffPolarity.POSITIVE
	description = "每个伤害效果增加等同层数的伤害"


func on_apply(_stats: Stats, _stacks: int) -> void:
	pass


func get_effects_for_phase(phase: StringName) -> Array[PipelineCombatEffectData]:
	if phase == &"outgoing_damage":
		return outgoing_damage_effects
	var empty: Array[PipelineCombatEffectData] = []
	return empty


func get_decay_phase() -> StringName:
	return &""


func decay_uses_stacks() -> bool:
	return false
