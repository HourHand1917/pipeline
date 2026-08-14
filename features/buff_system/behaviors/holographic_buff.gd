extends Buff
class_name HolographicBuff

@export_group("Mounted Combat Effects")
@export var incoming_damage_effects: Array[PipelineCombatEffectData] = []


func _init() -> void:
	id = "holographic"
	buff_name = "全息化"
	polarity = BuffPolarity.POSITIVE
	description = "受到的每次伤害固定结算为一点"


func on_turn_start(_stats: Stats, _stacks: int) -> void:
	pass


func get_effects_for_phase(phase: StringName) -> Array[PipelineCombatEffectData]:
	if phase == &"incoming_damage":
		return incoming_damage_effects
	var empty: Array[PipelineCombatEffectData] = []
	return empty


func get_decay_phase() -> StringName:
	return &"turn_start"


func decay_uses_stacks() -> bool:
	return true
