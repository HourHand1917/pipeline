extends Node

const ITEM_CATALOG := preload("res://Resource/item/mvp/item_catalog.gd")
const CARD_CATALOG := preload("res://Resource/card/card_catalog.gd")
const ITEM_DATA_SCRIPT := preload("res://Script/GD/resource/itemdata.gd")

const EXPECTED_ITEMS := {
	&"coolant": {
		"consume": true,
		"effects": [{"type": 6, "target": 0, "amount": 0, "buff": "anneal", "stacks": 1}],
	},
	&"spare_battery": {
		"consume": true,
		"effects": [{"type": 5, "target": 0, "amount": 2}],
	},
	&"spinach_powerups": {
		"consume": true,
		"effects": [{"type": 6, "target": 0, "amount": 0, "buff": "temporary_strength", "stacks": 3}],
	},
	&"gasoline": {
		"consume": true,
		"effects": [{"type": 6, "target": 0, "amount": 0, "buff": "strength", "stacks": 1}],
	},
	&"roller_shoes": {
		"consume": true,
		"effects": [
			{"type": 3, "target": 0, "amount": 1, "pipeline": 9},
			{"type": 3, "target": 0, "amount": 1, "pipeline": 9},
		],
	},
	&"grenade": {
		"consume": true,
		"effects": [{"type": 0, "target": 1, "amount": 5, "pipeline": 10}],
	},
	&"bulletproof_vest": {
		"consume": true,
		"effects": [{"type": 1, "target": 0, "amount": 7}],
	},
	&"particle_wall": {
		"consume": true,
		"effects": [{"type": 6, "target": 0, "amount": 0, "buff": "holographic", "stacks": 1}],
	},
	&"ice_cream": {"consume": false, "effects": []},
	&"medkit": {
		"consume": true,
		"effects": [{"type": 2, "target": 0, "amount": 5}],
	},
}

var failures: Array[String] = []
var checks := 0


func _ready() -> void:
	_check_card_catalog()
	_check_item_catalog()

	if failures.is_empty():
		print("MVP_CATALOG_TEST_PASS checks=%d cards=14 items=10 stock=10" % checks)
		get_tree().quit(0)
	else:
		for failure: String in failures:
			push_error(failure)
		print("MVP_CATALOG_TEST_FAIL checks=%d failures=%d" % [checks, failures.size()])
		get_tree().quit(1)


func _check_card_catalog() -> void:
	var cards := CARD_CATALOG.load_all()
	_check(cards.size() == 14, "卡牌 Catalog 必须正好14张")
	var card_ids := {}
	for card: CardData in cards:
		card_ids[card.id] = true
	_check(card_ids.size() == 14, "14张卡牌 ID 必须唯一")


func _check_item_catalog() -> void:
	var items := ITEM_CATALOG.load_all()
	_check(items.size() == 10, "道具 Catalog 必须正好10种")
	var ids := {}
	var scripts := {}
	var icons := {}
	var stock_total := 0

	for item: ItemData in items:
		_check(EXPECTED_ITEMS.has(item.id), "未知道具ID：%s" % item.id)
		if not EXPECTED_ITEMS.has(item.id):
			continue
		_check(not ids.has(item.id), "道具ID重复：%s" % item.id)
		ids[item.id] = true

		var script := item.get_script() as Script
		_check(script != null, "%s 缺少独立物品脚本" % item.id)
		if script != null:
			_check(script.get_base_script() == ITEM_DATA_SCRIPT, "%s 必须直接继承ItemData" % item.id)
			scripts[script.resource_path] = true

		_check(not item.display_name.is_empty(), "%s 缺少显示名" % item.id)
		_check(item.icon != null, "%s 缺少图标" % item.id)
		if item.icon != null:
			_check(item.icon.resource_path.ends_with(".svg"), "%s 图标必须为SVG" % item.id)
			icons[item.icon.resource_path] = true
		_check(item.shop_price == 0, "%s 策划未定价，资源价格必须保持0" % item.id)
		_check(item.mvp_stock == 1, "%s 默认库存必须为1" % item.id)
		_check(item.special_effects.is_empty(), "%s 不得绕过effects使用旧special契约" % item.id)
		stock_total += item.mvp_stock

		var expected: Dictionary = EXPECTED_ITEMS[item.id]
		_check(item.consume_on_use == expected["consume"], "%s consume_on_use错误" % item.id)
		_check_effects(item, expected["effects"])

	_check(ids.size() == 10, "10种道具ID必须唯一")
	_check(scripts.size() == 10, "每件道具必须使用不同的独立脚本")
	_check(icons.size() == 10, "每件道具必须使用不同的原创SVG图标")
	_check(stock_total == 10, "10种道具默认库存总数应为10件")
	_check(ITEM_CATALOG.load_by_id(&"grenade") != null, "应支持按ID加载手雷")
	_check(ITEM_CATALOG.load_by_id(&"missing") == null, "不存在ID必须返回null")


func _check_effects(item: ItemData, expected_effects: Array) -> void:
	_check(item.effects.size() == expected_effects.size(), "%s effect数量错误" % item.id)
	if item.effects.size() != expected_effects.size():
		return
	for index: int in item.effects.size():
		var effect: CombatEffectData = item.effects[index]
		var expected: Dictionary = expected_effects[index]
		_check(effect != null, "%s effect[%d]为空" % [item.id, index])
		if effect == null:
			continue
		_check(effect.type == expected["type"], "%s effect[%d] type错误" % [item.id, index])
		_check(effect.target == expected["target"], "%s effect[%d] target错误" % [item.id, index])
		_check(effect.amount == expected["amount"], "%s effect[%d] amount错误" % [item.id, index])
		if expected.has("pipeline"):
			_check(_pipeline_operation(effect) == expected["pipeline"], "%s effect[%d] pipeline_operation错误" % [item.id, index])
		else:
			_check(_pipeline_operation(effect) == -1, "%s 标准effect不应带pipeline_operation" % item.id)
		if expected.has("buff"):
			_check(effect.buff != null, "%s effect[%d] 缺少buff资源" % [item.id, index])
			if effect.buff != null:
				_check(effect.buff.id == expected["buff"], "%s effect[%d] buff ID错误" % [item.id, index])
			_check(effect.buff_stacks == expected["stacks"], "%s effect[%d] buff层数错误" % [item.id, index])
			_check(effect.buff_target == 0, "%s effect[%d] 必须指向PLAYER_STATS" % [item.id, index])


func _pipeline_operation(effect: CombatEffectData) -> int:
	for property: Dictionary in effect.get_property_list():
		if property.get("name", &"") == &"pipeline_operation":
			return int(effect.get("pipeline_operation"))
	return -1


func _check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)
