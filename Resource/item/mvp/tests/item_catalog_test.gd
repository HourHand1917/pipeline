extends Node

const ITEM_CATALOG := preload("res://Resource/item/mvp/item_catalog.gd")
const CARD_CATALOG := preload("res://card/card_catalog.gd")

const EXPECTED_ITEMS := {
	&"universal_toolkit": [18, 1, [1, 2]],
	&"emergency_battery": [8, 2, []],
	&"power_sunglasses": [10, 2, [3]],
	&"teleport_insoles": [10, 2, [4]],
	&"bandage": [8, 2, []],
	&"blast_plate": [9, 2, []],
	&"cooldown_spray": [6, 2, [5]],
	&"smoke_grenade": [14, 1, [6]],
}

var failures: Array[String] = []
var checks := 0


func _ready() -> void:
	var cards := CARD_CATALOG.load_all()
	_check(cards.size() == 14, "卡牌 Catalog 必须正好14张")
	var card_ids := {}
	for card: CardData in cards:
		card_ids[card.id] = true
	_check(card_ids.size() == 14, "14张卡牌 ID 必须唯一")

	var items := ITEM_CATALOG.load_all()
	_check(items.size() == 8, "道具 Catalog 必须正好8种")
	var stock_total := 0
	for item: ItemData in items:
		_check(EXPECTED_ITEMS.has(item.id), "未知道具ID：%s" % item.id)
		if not EXPECTED_ITEMS.has(item.id):
			continue
		var expected: Array = EXPECTED_ITEMS[item.id]
		_check(item.shop_price == expected[0], "%s 售价错误" % item.id)
		_check(item.mvp_stock == expected[1], "%s 数量错误" % item.id)
		_check(Array(item.special_effects) == expected[2], "%s special契约错误" % item.id)
		_check(not item.display_name.is_empty(), "%s 缺少显示名" % item.id)
		_check(item.icon != null, "%s 缺少图标" % item.id)
		stock_total += item.mvp_stock
	_check(stock_total == 14, "8种道具的建议流程总数量应为14件")
	_check(ITEM_CATALOG.load_by_id(&"smoke_grenade") != null, "应支持按ID加载道具")
	_check(ITEM_CATALOG.load_by_id(&"missing") == null, "不存在ID必须返回null")

	if failures.is_empty():
		print("MVP_CATALOG_TEST_PASS checks=%d cards=14 items=8 stock=14" % checks)
		get_tree().quit(0)
	else:
		for failure: String in failures:
			push_error(failure)
		print("MVP_CATALOG_TEST_FAIL checks=%d failures=%d" % [checks, failures.size()])
		get_tree().quit(1)


func _check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)
