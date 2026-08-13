extends RefCounted
class_name PipelineCardCatalog

## 新卡牌资源的唯一目录。这里只保存路径，不接管或改写现有 DataManager。
const CARD_PATHS: Array[String] = [
	"res://card/cards/knuckle_striker.tres",
	"res://card/cards/scrap_fist.tres",
	"res://card/cards/hydraulic_fist.tres",
	"res://card/cards/rocket_fist.tres",
	"res://card/cards/quad_coil_gun.tres",
	"res://card/cards/pea_gun.tres",
	"res://card/cards/deadly_kiss.tres",
	"res://card/cards/simple_cannon.tres",
	"res://card/cards/military_power_pack.tres",
	"res://card/cards/scrap_battery.tres",
	"res://card/cards/hemostatic_pump.tres",
	"res://card/cards/mechanical_shoes.tres",
	"res://card/cards/tactical_armor.tres",
	"res://card/cards/armored_shield.tres",
]


static func load_all() -> Array[CardData]:
	var cards: Array[CardData] = []
	for path: String in CARD_PATHS:
		var resource := ResourceLoader.load(path)
		if resource is CardData:
			cards.append(resource as CardData)
		else:
			push_error("卡牌目录无法读取 CardData：%s" % path)
	return cards


static func load_by_id(card_id: StringName) -> CardData:
	for card: CardData in load_all():
		if card.id == card_id:
			return card
	return null

