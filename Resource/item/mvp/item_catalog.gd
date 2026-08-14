extends RefCounted
class_name PipelineItemCatalog

const ITEM_PATHS: Array[String] = [
	"res://Resource/item/mvp/coolant.tres",
	"res://Resource/item/mvp/spare_battery.tres",
	"res://Resource/item/mvp/spinach_powerups.tres",
	"res://Resource/item/mvp/gasoline.tres",
	"res://Resource/item/mvp/roller_shoes.tres",
	"res://Resource/item/mvp/grenade.tres",
	"res://Resource/item/mvp/bulletproof_vest.tres",
	"res://Resource/item/mvp/particle_wall.tres",
	"res://Resource/item/mvp/ice_cream.tres",
	"res://Resource/item/mvp/medkit.tres",
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
