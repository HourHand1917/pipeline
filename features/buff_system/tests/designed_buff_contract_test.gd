extends Node

const CATALOG_PATH := "res://features/buff_system/resources/buffs/designed_buff_catalog.tres"
const BUFF_BASE_PATH := "res://Script/GD/resource/buff_data.gd"

const EXPECTED := {
	"strength": [Buff.BuffPolarity.POSITIVE, 0, true, 99],
	"temporary_strength": [Buff.BuffPolarity.POSITIVE, 1, true, 99],
	"dust": [Buff.BuffPolarity.NEGATIVE, 0, true, 99],
	"deployed_medkit": [Buff.BuffPolarity.POSITIVE, 0, true, 99],
	"disabled": [Buff.BuffPolarity.NEGATIVE, 0, true, 99],
	"anneal": [Buff.BuffPolarity.POSITIVE, 0, false, 1],
	"true": [Buff.BuffPolarity.POSITIVE, 0, false, 3],
	"false": [Buff.BuffPolarity.NEGATIVE, 0, false, 3],
	"holographic": [Buff.BuffPolarity.POSITIVE, 0, false, 1],
}

var failures: Array[String] = []
var checks := 0
var catalog: DesignedBuffCatalog


func _ready() -> void:
	catalog = load(CATALOG_PATH) as DesignedBuffCatalog
	_check(catalog != null, "九Buff Catalog必须可加载")
	if catalog != null:
		_test_catalog_and_resources()
		_test_mounted_effect_contracts()
		_test_stack_and_phase_contracts()
		_test_true_false_cancellation()

	if failures.is_empty():
		print("DESIGNED_BUFF_CONTRACT_TEST_PASS checks=%d buffs=9 effects=8 icons=9" % checks)
		get_tree().quit(0)
		return
	for failure: String in failures:
		push_error(failure)
	print("DESIGNED_BUFF_CONTRACT_TEST_FAIL checks=%d failures=%d" % [checks, failures.size()])
	get_tree().quit(1)


func _test_catalog_and_resources() -> void:
	_check(catalog.buffs.size() == 9, "Catalog必须正好包含9个Buff")
	var ids := {}
	for buff: Buff in catalog.buffs:
		_check(buff != null, "Catalog不能包含空Buff")
		if buff == null:
			continue
		_check(not ids.has(buff.id), "Buff ID重复：%s" % buff.id)
		ids[buff.id] = true
		_check(EXPECTED.has(buff.id), "出现未锁定Buff：%s" % buff.id)
		_check(not buff.buff_name.is_empty(), "%s缺少显示名" % buff.id)
		_check(not buff.description.is_empty(), "%s缺少说明" % buff.id)
		_check(buff.icon != null, "%s缺少原创SVG图标" % buff.id)
		_check(buff.has_method("get_effects_for_phase"), "%s缺少效果阶段契约" % buff.id)
		_check(buff.has_method("get_decay_phase"), "%s缺少衰减阶段契约" % buff.id)
		_check(buff.has_method("decay_uses_stacks"), "%s缺少层数衰减契约" % buff.id)
		var script := buff.get_script() as Script
		_check(script != null, "%s缺少独立脚本" % buff.id)
		if script != null:
			var base := script.get_base_script()
			_check(base != null and base.resource_path == BUFF_BASE_PATH,
				"%s必须直接extends Buff，实际基类=%s" % [buff.id, base.resource_path if base != null else "null"])
		if EXPECTED.has(buff.id):
			var expected: Array = EXPECTED[buff.id]
			_check(buff.polarity == expected[0], "%s极性错误" % buff.id)
			_check(buff.duration == expected[1], "%s duration错误" % buff.id)
			_check(buff.stackable == expected[2], "%s stackable错误" % buff.id)
			_check(buff.max_stacks == expected[3], "%s max_stacks错误" % buff.id)
	for id: String in EXPECTED:
		_check(ids.has(id), "Catalog缺少Buff：%s" % id)
		_check(catalog.get_buff(StringName(id)) != null, "无法按ID加载：%s" % id)


func _test_mounted_effect_contracts() -> void:
	_expect_ops("strength", &"outgoing_damage", [&"add_damage_by_buff_stacks"])
	_expect_ops("temporary_strength", &"outgoing_damage", [&"add_damage_by_buff_stacks"])
	_expect_ops("dust", &"turn_end", [])
	_expect_ops("deployed_medkit", &"turn_start", [&"heal_owner_by_buff_stacks"])
	_expect_ops("disabled", &"turn_start", [&"set_all_card_cooldown"])
	_expect_ops("anneal", &"apply", [&"reset_all_card_cooldowns"])
	_expect_ops("true", &"outgoing_damage", [&"half_damage_floor"])
	_expect_ops("true", &"incoming_damage", [&"half_damage_floor"])
	_expect_ops("false", &"outgoing_damage", [&"add_half_damage_ceil"])
	_expect_ops("false", &"incoming_damage", [&"add_half_damage_ceil"])
	_expect_ops("false", &"turn_start", [&"drain_player_energy"])
	_expect_ops("holographic", &"incoming_damage", [&"set_incoming_damage_to_one"])

	var anneal := catalog.get_buff(&"anneal")
	_check(bool(anneal.call("consume_after_apply")), "退火必须在apply结算后移除")
	var medkit := catalog.get_buff(&"deployed_medkit")
	_check(bool(medkit.call("consume_after_phase", &"turn_start")), "治疗包必须在turn_start结算后移除")
	_check(not bool(medkit.call("consume_after_phase", &"turn_end")), "治疗包不能在turn_end误移除")


func _test_stack_and_phase_contracts() -> void:
	var dust := catalog.get_buff(&"dust")
	var cell := CellRuntime.new(Vector2i.ZERO)
	cell.stats.add_buff(dust, 3)
	_check(cell.stats.before_light(cell) == 3, "蒙尘3层必须增加3点点亮费用")
	_check(cell.stats.can_light(cell), "蒙尘不能直接禁止点亮")
	cell.stats.tick_turn_end()
	_check(cell.stats.get_buff_stacks("dust") == 2, "蒙尘回合结束应减少1层")

	var disabled := catalog.get_buff(&"disabled")
	var disabled_stats := Stats.new()
	disabled_stats.add_buff(disabled, 1)
	_check(not disabled_stats.can_light(cell), "禁用仍须兼容旧格子禁止点亮契约")
	disabled_stats.tick_turn_end()
	_check(not disabled_stats.has_buff("disabled"), "禁用1层应在回合结束移除")

	var temporary := catalog.get_buff(&"temporary_strength")
	var temporary_stats := Stats.new()
	temporary_stats.add_buff(temporary, 3)
	temporary_stats.tick_turn_end()
	_check(not temporary_stats.has_buff("temporary_strength"), "临时力量应在回合结束整体移除")

	var true_buff := catalog.get_buff(&"true")
	var true_stats := Stats.new()
	true_stats.add_buff(true_buff, 3)
	true_stats.tick_turn_end()
	_check(true_stats.get_buff_stacks("true") == 2, "True第一次end tick后应剩2层")
	true_stats.tick_turn_end()
	true_stats.tick_turn_end()
	_check(not true_stats.has_buff("true"), "True必须在第3次end tick移除")

	var false_buff := catalog.get_buff(&"false")
	var false_stats := Stats.new()
	false_stats.add_buff(false_buff, 3)
	false_stats.tick_turn_end()
	_check(false_stats.get_buff_stacks("false") == 2, "False第一次end tick后应剩2层")
	false_stats.tick_turn_end()
	false_stats.tick_turn_end()
	_check(not false_stats.has_buff("false"), "False必须在第3次end tick移除")

	var holographic := catalog.get_buff(&"holographic")
	var holographic_stats := Stats.new()
	holographic_stats.add_buff(holographic, 1)
	holographic_stats.tick_turn_start()
	_check(not holographic_stats.has_buff("holographic"), "全息化应在下次turn_start移除")

	var strength := catalog.get_buff(&"strength")
	var strength_stats := Stats.new()
	strength_stats.add_buff(strength, 2)
	strength_stats.tick_turn_start()
	strength_stats.tick_turn_end()
	_check(strength_stats.get_buff_stacks("strength") == 2, "力量不能随回合衰减")


func _test_true_false_cancellation() -> void:
	var true_buff := catalog.get_buff(&"true")
	var false_buff := catalog.get_buff(&"false")

	var stats_a := Stats.new()
	stats_a.add_buff(false_buff, 3)
	stats_a.add_buff(true_buff, 3)
	_check(not stats_a.has_buff("true") and not stats_a.has_buff("false"),
		"已有False时施加True必须中和二者")

	var stats_b := Stats.new()
	stats_b.add_buff(true_buff, 3)
	stats_b.add_buff(false_buff, 3)
	_check(not stats_b.has_buff("true") and not stats_b.has_buff("false"),
		"已有True时施加False必须中和二者")


func _expect_ops(buff_id: String, phase: StringName, expected: Array[StringName]) -> void:
	var buff := catalog.get_buff(StringName(buff_id))
	_check(buff != null, "无法检查不存在的Buff：%s" % buff_id)
	if buff == null:
		return
	var effects: Array = buff.call("get_effects_for_phase", phase)
	_check(effects.size() == expected.size(), "%s/%s效果数量错误" % [buff_id, phase])
	var actual: Array[StringName] = []
	for effect in effects:
		_check(effect is PipelineCombatEffectData, "%s/%s必须挂PipelineCombatEffectData" % [buff_id, phase])
		if effect is PipelineCombatEffectData:
			actual.append((effect as PipelineCombatEffectData).pipeline_operation_key())
	_check(actual == expected, "%s/%s操作错误：%s" % [buff_id, phase, actual])


func _check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)
