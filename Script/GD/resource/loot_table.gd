extends Resource
class_name LootTable

## 战利品表：瓶盖/水龙头货币 + 卡牌 + 道具。
## 运行时会被「领取」改动，所以源要用 duplicate(true) 复制一份，不要直接改共享的 .tres。

@export_group("Currency")
@export var bottle_cap: int = 0
@export var faucet: int = 0

@export_group("Cards & Items")
@export var cards: Array[CardData] = []
@export var items: Array[ItemData] = []


func is_empty() -> bool:
	return bottle_cap <= 0 and faucet <= 0 and cards.is_empty() and items.is_empty()


func take_bottle_cap() -> int:
	var amount := bottle_cap
	bottle_cap = 0
	return amount


func take_faucet() -> int:
	var amount := faucet
	faucet = 0
	return amount


func take_card(index: int) -> CardData:
	if index < 0 or index >= cards.size():
		return null
	var card: CardData = cards[index]
	cards.remove_at(index)
	return card


func take_item(index: int) -> ItemData:
	if index < 0 or index >= items.size():
		return null
	var item: ItemData = items[index]
	items.remove_at(index)
	return item


func to_dict() -> Dictionary:
	var card_paths: Array[String] = []
	for c in cards:
		if c != null and c.resource_path != "":
			card_paths.append(c.resource_path)
	var item_paths: Array[String] = []
	for it in items:
		if it != null and it.resource_path != "":
			item_paths.append(it.resource_path)
	return {
		"bottle_cap": bottle_cap,
		"faucet": faucet,
		"cards": card_paths,
		"items": item_paths,
	}


func from_dict(data: Dictionary) -> void:
	bottle_cap = int(data.get("bottle_cap", 0))
	faucet = int(data.get("faucet", 0))
	cards.clear()
	for path in data.get("cards", []):
		if path is String and ResourceLoader.exists(path):
			var res := load(path)
			if res is CardData:
				cards.append(res)
	items.clear()
	for path in data.get("items", []):
		if path is String and ResourceLoader.exists(path):
			var res := load(path)
			if res is ItemData:
				items.append(res)
