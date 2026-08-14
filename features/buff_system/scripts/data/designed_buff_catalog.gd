extends Resource
class_name DesignedBuffCatalog

## Catalog for the nine approved production Buff resources.  The concrete
## resources inherit Buff directly, so this intentionally does not depend on
## the older experimental PipelineBuffData type.

@export var buffs: Array[Buff] = []


func get_buff(buff_id: StringName) -> Buff:
	for buff: Buff in buffs:
		if buff != null and buff.id == String(buff_id):
			return buff
	return null


func contains(buff_id: StringName) -> bool:
	return get_buff(buff_id) != null


func ids() -> PackedStringArray:
	var result := PackedStringArray()
	for buff: Buff in buffs:
		if buff != null:
			result.append(buff.id)
	return result
