extends Node

const CATALOG := preload("res://card/card_catalog.gd")

const EXPECTED: Dictionary = {
	&"knuckle_striker": {"name": "指节撞针", "cells": 1, "range": [1, 1], "cooldown": 1, "legacy": ["damage", 3], "effects": [[0, 1, 3]]},
	&"scrap_fist": {"name": "废铁拳", "cells": 2, "range": [2, 3], "cooldown": 2, "legacy": ["damage", 5], "effects": [[0, 1, 5]]},
	&"hydraulic_fist": {"name": "液压重拳", "cells": 2, "range": [1, 1], "cooldown": 3, "legacy": ["damage", 9], "effects": [[0, 1, 9]]},
	&"rocket_fist": {"name": "火箭冲拳", "cells": 2, "range": [2, 3], "cooldown": 2, "legacy": ["damage", 4], "effects": [[3, 0, 1], [0, 1, 4]]},
	&"quad_coil_gun": {"name": "四握式线圈铳", "cells": 4, "range": [4, 6], "cooldown": 3, "legacy": ["damage", 20], "effects": [[0, 1, 20]]},
	&"pea_gun": {"name": "豆豆枪", "cells": 2, "range": [2, 4], "cooldown": 1, "legacy": ["damage", 4], "effects": [[0, 1, 4]]},
	&"deadly_kiss": {"name": "致命亲亲", "cells": 3, "range": [2, 3], "cooldown": 2, "legacy": ["damage", 10], "effects": [[0, 1, 5], [0, 1, 5]]},
	&"simple_cannon": {"name": "简易火炮", "cells": 3, "range": [1, 3], "cooldown": 3, "legacy": ["damage", 5], "effects": [[4, 1, 1], [0, 1, 5]]},
	&"military_power_pack": {"name": "军用动力匣", "cells": 2, "range": [0, 0], "cooldown": 2, "legacy": ["energy", 3], "effects": [[5, 0, 3]]},
	&"scrap_battery": {"name": "废旧电池", "cells": 1, "range": [0, 0], "cooldown": 2, "legacy": ["energy", 1], "effects": [[5, 0, 1]]},
	&"hemostatic_pump": {"name": "止血泵", "cells": 2, "range": [0, 0], "cooldown": 99, "legacy": ["heal", 4], "effects": [[2, 0, 4]]},
	&"mechanical_shoes": {"name": "机械鞋", "cells": 1, "range": [0, 0], "cooldown": 1, "legacy": ["energy", 0], "effects": [[3, 0, 1]]},
	&"tactical_armor": {"name": "战术护甲", "cells": 1, "range": [0, 0], "cooldown": 2, "legacy": ["shield", 4], "effects": [[1, 0, 4]]},
	&"armored_shield": {"name": "装甲盾", "cells": 2, "range": [0, 0], "cooldown": 2, "legacy": ["shield", 7], "effects": [[1, 0, 7]]},
}

var checks: int = 0
var failures: int = 0


func _ready() -> void:
	_run_tests.call_deferred()


func _run_tests() -> void:
	_test_catalog_once()
	for round_index: int in range(100):
		_test_all_cards(round_index)
	if failures == 0:
		print("CARD_CONFIG_TEST PASS (%d checks, 100 rounds, 14 cards, 17 effects)" % checks)
	else:
		push_error("CARD_CONFIG_TEST FAIL (%d failures / %d checks)" % [failures, checks])
	get_tree().quit(0 if failures == 0 else 1)


func _test_catalog_once() -> void:
	_check(CATALOG.CARD_PATHS.size() == 14, "catalog 必须正好包含14张卡")
	var cards: Array[CardData] = CATALOG.load_all()
	_check(cards.size() == 14, "catalog 必须成功加载14张 CardData")
	var ids: Dictionary = {}
	var effect_paths: Dictionary = {}
	for card: CardData in cards:
		_check(not ids.has(card.id), "卡牌ID必须唯一：%s" % card.id)
		ids[card.id] = true
		for effect: CombatEffectData in card.effects:
			_check(not effect.resource_path.is_empty(), "效果必须是独立 .tres：%s" % card.id)
			effect_paths[effect.resource_path] = true
	_check(ids.size() == 14, "必须有14个唯一卡牌ID")
	_check(effect_paths.size() == 17, "必须有17个独立效果资源")
	_check(CATALOG.load_by_id(&"rocket_fist") != null, "可按ID读取火箭冲拳")
	_check(CATALOG.load_by_id(&"missing_card") == null, "不存在的ID返回null")


func _test_all_cards(round_index: int) -> void:
	var cards: Array[CardData] = CATALOG.load_all()
	_check(cards.size() == EXPECTED.size(), "第%d轮：加载数量" % round_index)
	for card: CardData in cards:
		_check(EXPECTED.has(card.id), "第%d轮：未知ID %s" % [round_index, card.id])
		if not EXPECTED.has(card.id):
			continue
		var expected: Dictionary = EXPECTED[card.id]
		_check(card.display_name == expected["name"], "%s：名称" % card.id)
		_check(card.shape_offsets.size() == expected["cells"], "%s：格数" % card.id)
		_check(card.min_range == expected["range"][0], "%s：最小距离" % card.id)
		_check(card.max_range == expected["range"][1], "%s：最大距离" % card.id)
		_check(card.cooldown_turns == expected["cooldown"], "%s：冷却" % card.id)
		_check(card.effect_type == expected["legacy"][0], "%s：兼容效果类型" % card.id)
		_check(card.effect_value == expected["legacy"][1], "%s：兼容效果数值" % card.id)
		_check(card.effects.size() == expected["effects"].size(), "%s：效果数量" % card.id)
		for effect_index: int in range(mini(card.effects.size(), expected["effects"].size())):
			var effect: CombatEffectData = card.effects[effect_index]
			var effect_expected: Array = expected["effects"][effect_index]
			_check(effect != null, "%s：效果%d不能为空" % [card.id, effect_index])
			if effect == null:
				continue
			_check(effect.type == effect_expected[0], "%s：效果%d类型/顺序" % [card.id, effect_index])
			_check(effect.target == effect_expected[1], "%s：效果%d目标" % [card.id, effect_index])
			_check(effect.amount == effect_expected[2], "%s：效果%d数值" % [card.id, effect_index])


func _check(condition: bool, message: String) -> void:
	checks += 1
	if condition:
		return
	failures += 1
	push_error("[CARD TEST] %s" % message)

