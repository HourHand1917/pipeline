extends Resource
class_name PipelineCardCatalog

## 独立卡牌目录。这个类型不依赖 DataManager，方便场景按需加载。
@export var cards: Array[CardData] = []


func get_card(card_id: StringName) -> CardData:
	for card in cards:
		if card != null and card.id == card_id:
			return card
	return null


func get_ids() -> Array[StringName]:
	var result: Array[StringName] = []
	for card in cards:
		if card != null:
			result.append(card.id)
	return result
