extends Buff
class_name TrueBuff

@export_group("Mounted Combat Effects")
@export var outgoing_damage_effects: Array[PipelineCombatEffectData] = []
@export var incoming_damage_effects: Array[PipelineCombatEffectData] = []


func _init() -> void:
	id = "true"
	buff_name = "True"
	polarity = BuffPolarity.POSITIVE
	description = "造成和受到的伤害向下取整减半"


func on_apply(stats: Stats, _stacks: int) -> void:
	if stats != null and stats.has_buff("false"):
		stats.remove_buff("false")
		stats.remove_buff(id)


func get_effects_for_phase(phase: StringName) -> Array[PipelineCombatEffectData]:
	match phase:
		&"outgoing_damage":
			return outgoing_damage_effects
		&"incoming_damage":
			return incoming_damage_effects
	var empty: Array[PipelineCombatEffectData] = []
	return empty


func get_decay_phase() -> StringName:
	return &"turn_end"


func decay_uses_stacks() -> bool:
	return true
