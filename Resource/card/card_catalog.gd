extends RefCounted
class_name PipelineCardCatalog

## 生产卡牌的唯一字典。
##
## 这里只登记已经完成数值、效果和升级关系的 14 组卡牌，不修改
## DataManager 的持有数量。旧的 battery/revolver 等原型卡仍保留原路径，
## 但它们的升级资源尚未完成，因此不会混入这个生产字典。

const BASE_CARD_PATHS: Array[String] = [
	"res://Resource/card/cards/knuckle_striker.tres",
	"res://Resource/card/cards/scrap_fist.tres",
	"res://Resource/card/cards/hydraulic_fist.tres",
	"res://Resource/card/cards/rocket_fist.tres",
	"res://Resource/card/cards/quad_coil_gun.tres",
	"res://Resource/card/cards/pea_gun.tres",
	"res://Resource/card/cards/deadly_kiss.tres",
	"res://Resource/card/cards/simple_cannon.tres",
	"res://Resource/card/cards/military_power_pack.tres",
	"res://Resource/card/cards/scrap_battery.tres",
	"res://Resource/card/cards/hemostatic_pump.tres",
	"res://Resource/card/cards/mechanical_shoes.tres",
	"res://Resource/card/cards/tactical_armor.tres",
	"res://Resource/card/cards/armored_shield.tres",
]

const UPGRADED_CARD_PATHS: Array[String] = [
	"res://Resource/card/upgrades/knuckle_striker_up.tres",
	"res://Resource/card/upgrades/scrap_fist_up.tres",
	"res://Resource/card/upgrades/hydraulic_fist_up.tres",
	"res://Resource/card/upgrades/rocket_fist_up.tres",
	"res://Resource/card/upgrades/quad_coil_gun_up.tres",
	"res://Resource/card/upgrades/pea_gun_up.tres",
	"res://Resource/card/upgrades/deadly_kiss_up.tres",
	"res://Resource/card/upgrades/simple_cannon_up.tres",
	"res://Resource/card/upgrades/military_power_pack_up.tres",
	"res://Resource/card/upgrades/scrap_battery_up.tres",
	"res://Resource/card/upgrades/hemostatic_pump_up.tres",
	"res://Resource/card/upgrades/mechanical_shoes_up.tres",
	"res://Resource/card/upgrades/tactical_armor_up.tres",
	"res://Resource/card/upgrades/armored_shield_up.tres",
]

# 兼容旧调用者：CARD_PATHS 始终表示可获得的基础卡。
const CARD_PATHS: Array[String] = BASE_CARD_PATHS


static func load_all() -> Array[CardData]:
	return _load_paths(BASE_CARD_PATHS)


static func load_upgrades() -> Array[CardData]:
	return _load_paths(UPGRADED_CARD_PATHS)


static func load_all_versions() -> Array[CardData]:
	var cards := load_all()
	cards.append_array(load_upgrades())
	return cards


static func load_by_id(card_id: StringName) -> CardData:
	for card: CardData in load_all_versions():
		if card.id == card_id:
			return card
	return null


static func load_upgrade_for(base_id: StringName) -> CardData:
	var base := load_by_id(base_id)
	if base == null or base.is_upgraded or base.upgraded_version == null:
		return null
	return base.upgraded_version as CardData


static func _load_paths(paths: Array[String]) -> Array[CardData]:
	var cards: Array[CardData] = []
	for path: String in paths:
		var resource := ResourceLoader.load(path)
		if resource is CardData:
			cards.append(resource as CardData)
		else:
			push_error("卡牌字典无法读取 CardData：%s" % path)
	return cards
