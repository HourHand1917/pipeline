extends RefCounted
class_name PipelineItemCatalog

const ITEM_PATHS: Array[String] = [
	"res://Resource/item/mvp/universal_toolkit.tres",
	"res://Resource/item/mvp/emergency_battery.tres",
	"res://Resource/item/mvp/power_sunglasses.tres",
	"res://Resource/item/mvp/teleport_insoles.tres",
	"res://Resource/item/mvp/bandage.tres",
	"res://Resource/item/mvp/blast_plate.tres",
	"res://Resource/item/mvp/cooldown_spray.tres",
	"res://Resource/item/mvp/smoke_grenade.tres",
]


static func load_all() -> Array[ItemData]:
	var result: Array[ItemData] = []
	for path: String in ITEM_PATHS:
		var item := ResourceLoader.load(path)
		if item is ItemData:
			result.append(item as ItemData)
		else:
			push_error("道具目录无法读取 ItemData：%s" % path)
	return result


static func load_by_id(item_id: StringName) -> ItemData:
	for item: ItemData in load_all():
		if item.id == item_id:
			return item
	return null
