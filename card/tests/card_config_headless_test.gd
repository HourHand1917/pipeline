extends Node

const CATALOG_PATH := "res://card/catalog/all_cards.tres"

const EXPECTED := {
	&"finger_knuckle_striker": {"name": "指节撞针", "size": 1, "range": Vector2i(1, 1), "cooldown": 1, "category": "attack", "effects": [[0, 1, 3]]},
	&"scrap_fist": {"name": "废铁拳", "size": 2, "range": Vector2i(2, 3), "cooldown": 2, "category": "attack", "effects": [[0, 1, 5]]},
	&"hydraulic_heavy_fist": {"name": "液压重拳", "size": 2, "range": Vector2i(1, 1), "cooldown": 3, "category": "attack", "effects": [[0, 1, 9]]},
	&"rocket_punch": {"name": "火箭冲拳", "size": 2, "range": Vector2i(2, 3), "cooldown": 2, "category": "attack", "effects": [[3, 0, 1], [0, 1, 4]]},
	&"four_grip_coil_rifle": {"name": "四握式线圈铳", "size": 4, "range": Vector2i(4, 6), "cooldown": 3, "category": "attack", "effects": [[0, 1, 20]]},
	&"pea_shooter": {"name": "豆豆枪", "size": 2, "range": Vector2i(2, 4), "cooldown": 1, "category": "attack", "effects": [[0, 1, 4]]},
	&"deadly_kiss": {"name": "致命亲亲", "size": 3, "range": Vector2i(2, 3), "cooldown": 2, "category": "attack", "effects": [[0, 1, 5], [0, 1, 5]]},
	&"improvised_cannon": {"name": "简易火炮", "size": 3, "range": Vector2i(1, 3), "cooldown": 3, "category": "attack", "effects": [[4, 1, 1], [0, 1, 5]]},
	&"military_power_cell": {"name": "军用动力匣", "size": 2, "range": Vector2i(0, 0), "cooldown": 2, "category": "energy", "effects": [[5, 0, 3]]},
	&"worn_battery": {"name": "废旧电池", "size": 1, "range": Vector2i(0, 0), "cooldown": 2, "category": "energy", "effects": [[5, 0, 1]]},
	&"hemostatic_pump": {"name": "止血泵", "size": 2, "range": Vector2i(0, 0), "cooldown": 99, "category": "heal", "effects": [[2, 0, 4]]},
	&"mechanical_shoes": {"name": "机械鞋", "size": 1, "range": Vector2i(0, 0), "cooldown": 1, "category": "movement", "effects": [[3, 0, 1]]},
	&"tactical_armor": {"name": "战术护甲", "size": 1, "range": Vector2i(0, 0), "cooldown": 2, "category": "defense", "effects": [[1, 0, 4]]},
	&"armored_shield": {"name": "装甲盾", "size": 2, "range": Vector2i(0, 0), "cooldown": 2, "category": "defense", "effects": [[1, 0, 7]]},
}

var _checks := 0
var _failures: PackedStringArray = []


func _ready() -> void:
	var stress_rounds := _get_stress_rounds()
	var catalog := load(CATALOG_PATH) as PipelineCardCatalog
	_check(catalog != null, "目录资源必须能加载")
	for _round in stress_rounds:
		if catalog != null:
			_verify_catalog(catalog)
		var loaded_cards := PipelineCardCatalogLoader.load_all_cards()
		_check(loaded_cards.size() == EXPECTED.size(), "独立加载器必须返回14张卡")
		for card_id in EXPECTED:
			_check(PipelineCardCatalogLoader.get_card(card_id) != null, "加载器按ID查询失败：%s" % card_id)

	if _failures.is_empty():
		print("CARD CONFIG TEST PASS (%d rounds / %d checks)" % [stress_rounds, _checks])
		get_tree().quit(0)
	else:
		for failure in _failures:
			push_error(failure)
		print("CARD CONFIG TEST FAIL (%d failures / %d checks)" % [_failures.size(), _checks])
		get_tree().quit(1)


func _verify_catalog(catalog: PipelineCardCatalog) -> void:
	_check(catalog.cards.size() == EXPECTED.size(), "目录必须恰好包含14张卡")
	var seen: Dictionary = {}
	for card in catalog.cards:
		_check(card != null, "目录不能包含空卡牌")
		if card == null:
			continue
		_check(not seen.has(card.id), "卡牌ID必须唯一：%s" % card.id)
		seen[card.id] = true
		_check(EXPECTED.has(card.id), "出现未登记卡牌：%s" % card.id)
		if EXPECTED.has(card.id):
			_verify_card(card, EXPECTED[card.id])
	for card_id in EXPECTED:
		_check(seen.has(card_id), "缺少卡牌：%s" % card_id)


func _get_stress_rounds() -> int:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with("--stress-rounds="):
			return maxi(1, argument.trim_prefix("--stress-rounds=").to_int())
	return 1


func _verify_card(card: CardData, expected: Dictionary) -> void:
	_check(card.display_name == expected["name"], "%s 名称不符" % card.id)
	_check(card.category == expected["category"], "%s 分类不符" % card.id)
	_check(card.shape_offsets.size() == expected["size"], "%s 占格数不符" % card.id)
	for index in card.shape_offsets.size():
		_check(card.shape_offsets[index] == Vector2i(index, 0), "%s 必须使用横向连续形状" % card.id)
	var expected_range: Vector2i = expected["range"]
	_check(card.min_range == expected_range.x and card.max_range == expected_range.y, "%s 距离不符" % card.id)
	_check(card.cooldown_turns == expected["cooldown"], "%s 冷却不符" % card.id)
	var expected_effects: Array = expected["effects"]
	_check(card.effects.size() == expected_effects.size(), "%s 效果数量/顺序不符" % card.id)
	for index in mini(card.effects.size(), expected_effects.size()):
		var actual: CombatEffectData = card.effects[index]
		var spec: Array = expected_effects[index]
		_check(actual != null, "%s 效果%d为空" % [card.id, index])
		if actual != null:
			_check(actual.type == spec[0], "%s 效果%d类型或顺序不符" % [card.id, index])
			_check(actual.target == spec[1], "%s 效果%d目标不符" % [card.id, index])
			_check(actual.amount == spec[2], "%s 效果%d数值不符" % [card.id, index])


func _check(condition: bool, message: String) -> void:
	_checks += 1
	if not condition:
		_failures.append(message)
