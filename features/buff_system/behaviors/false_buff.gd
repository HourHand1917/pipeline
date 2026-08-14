extends Buff
class_name FalseBuff

@export_group("Mounted Combat Effects")
@export var outgoing_damage_effects: Array[PipelineCombatEffectData] = []
@export var incoming_damage_effects: Array[PipelineCombatEffectData] = []
@export var turn_start_effects: Array[PipelineCombatEffectData] = []


func _init() -> void:
	id = "false"
	buff_name = "False"
	polarity = BuffPolarity.NEGATIVE
	description = "伤害增加一半，并在回合开始失去一点能量"


func on_apply(stats: Stats, _stacks: int) -> void:
	if stats != null and stats.has_buff("true"):
		stats.remove_buff("true")
		stats.remove_buff(id)


func on_turn_start(_stats: Stats, _stacks: int) -> void:
	pass


func get_effects_for_phase(phase: StringName) -> Array[PipelineCombatEffectData]:
	match phase:
		&"outgoing_damage":
			return outgoing_damage_effects
		&"incoming_damage":
			return incoming_damage_effects
		&"turn_start":
			return turn_start_effects
	var empty: Array[PipelineCombatEffectData] = []
	return empty


func get_decay_phase() -> StringName:
	return &"turn_end"


func decay_uses_stacks() -> bool:
	return true
