extends Resource
class_name PipelineBuffCatalog

@export var buffs: Array[PipelineBuffData] = []


func get_buff(buff_id: StringName) -> PipelineBuffData:
	for buff in buffs:
		if buff != null and buff.id == String(buff_id):
			return buff
	return null


func contains(buff_id: StringName) -> bool:
	return get_buff(buff_id) != null


func ids() -> PackedStringArray:
	var result := PackedStringArray()
	for buff in buffs:
		if buff != null:
			result.append(buff.id)
	return result
