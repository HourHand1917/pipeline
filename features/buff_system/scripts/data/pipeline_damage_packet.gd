extends RefCounted
class_name PipelineDamagePacket

var source_host: PipelineBuffHost
var target_host: PipelineBuffHost
var base_amount: int = 0
var final_amount: int = 0
var tags: PackedStringArray = []
var ignores_shield: bool = false
var cancelled: bool = false
var metadata: Dictionary = {}


func _init(
	p_source: PipelineBuffHost = null,
	p_target: PipelineBuffHost = null,
	p_amount: int = 0,
	p_tags: PackedStringArray = []
) -> void:
	source_host = p_source
	target_host = p_target
	base_amount = maxi(0, p_amount)
	final_amount = base_amount
	tags = p_tags.duplicate()
