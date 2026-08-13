extends Control
class_name Core00CardBattle

## Core-00 + 14-card standalone playable battle.
## Everything is additive: the existing battle, card resources and boss manager
## remain untouched.  Public debug_* methods are intentionally deterministic so
## this scene can be exercised by a headless test without clicking the UI.

const MANAGER_SCRIPT := preload("res://features/core00_ai/scripts/core00_enermy_mannager.gd")
const PROFILE_RESOURCE := preload("res://features/core00_ai/profiles/core00_profile.tres")

const TARGET_TRUE := &"true_hand"
const TARGET_FALSE := &"false_hand"
const TARGET_BODY := &"body"
const CARD_LIGHT_PER_TURN := 2
const PLAYER_MAX_HP := 50
const BOARD_CELLS := 12

@export var build_visual_ui: bool = true
@export_range(1, 999, 1) var starting_player_hp: int = PLAYER_MAX_HP
@export_range(1, BOARD_CELLS, 1) var starting_player_position: int = 6

var boss: Core00EnermyMannager
var cards: Array[CardData] = []
var card_by_id: Dictionary = {}
var card_runtime: Dictionary = {}

var player_hp: int = PLAYER_MAX_HP
var player_shield: int = 0
var player_position: int = 6
var light_points: int = CARD_LIGHT_PER_TURN
var player_turn: int = 1
var free_move_available: bool = true
var selected_card_id: StringName = &""
var selected_target_id: StringName = TARGET_TRUE
var battle_over: bool = false
var battle_result: StringName = &"playing"

var _ui_built := false
var _header_label: Label
var _intent_label: Label
var _selection_label: RichTextLabel
var _log_label: RichTextLabel
var _charge_button: Button
var _use_button: Button
var _target_buttons: Dictionary = {}
var _card_buttons: Dictionary = {}
var _log_entries: PackedStringArray = []


func _ready() -> void:
	custom_minimum_size = Vector2(1920, 1080)
	_create_boss_manager()
	_load_card_catalog()
	if build_visual_ui:
		_build_ui()
	reset_battle()
	if "--smoke-test" in OS.get_cmdline_user_args():
		call_deferred("_finish_smoke_test")
	else:
		var capture_path := _command_line_value("--capture-path=")
		if not capture_path.is_empty():
			_capture_after_frames(capture_path)
	if build_visual_ui:
		set_process_unhandled_key_input(true)


func _finish_smoke_test() -> void:
	print("CORE00_BATTLE_READY")
	get_tree().quit(0)


func _command_line_value(prefix: String) -> String:
	for argument in OS.get_cmdline_user_args():
		if argument.begins_with(prefix):
			return argument.trim_prefix(prefix)
	return ""


func _capture_after_frames(path: String) -> void:
	await get_tree().process_frame
	await get_tree().process_frame
	await get_tree().process_frame
	var image := get_viewport().get_texture().get_image()
	var error := image.save_png(path)
	print("CORE00_BATTLE_CAPTURE:%s:%s" % [path, error_string(error)])
	get_tree().quit(0 if error == OK else 2)


func _unhandled_key_input(event: InputEvent) -> void:
	if not event.pressed or event.echo or battle_over:
		return
	match event.keycode:
		KEY_A, KEY_LEFT:
			_on_move_left()
			get_viewport().set_input_as_handled()
		KEY_D, KEY_RIGHT:
			_on_move_right()
			get_viewport().set_input_as_handled()
		KEY_SPACE, KEY_ENTER:
			_on_end_turn()
			get_viewport().set_input_as_handled()


func _draw() -> void:
	if not build_visual_ui:
		return
	draw_rect(Rect2(0, 0, 1920, 1080), Color("07121a"))
	draw_rect(Rect2(0, 0, 1920, 92), Color("102836"))
	draw_rect(Rect2(18, 108, 1410, 384), Color("0b1b25"), true)
	draw_rect(Rect2(18, 510, 1410, 550), Color("0a1720"), true)
	draw_rect(Rect2(1448, 108, 454, 952), Color("10202a"), true)
	# Subtle CRT scanlines give the clean prototype a wasteland-terminal feel.
	for scan_y in range(94, 1080, 8):
		draw_line(Vector2(0, scan_y), Vector2(1920, scan_y), Color(0.1, 0.75, 0.82, 0.035), 1.0)

	var start_x := 82.0
	var end_x := 1360.0
	var arena_y := 340.0
	draw_line(Vector2(start_x, arena_y), Vector2(end_x, arena_y), Color("3d8ca2"), 8.0)
	for cell in range(1, BOARD_CELLS + 1):
		var point := _cell_point(cell, start_x, end_x, arena_y)
		var cell_color := Color("6ca8b7")
		if cell % 2 == 0:
			cell_color = Color("61d5e5")
		draw_circle(point, 17.0, cell_color)
		draw_string(ThemeDB.fallback_font, point + Vector2(-7, 46), str(cell), HORIZONTAL_ALIGNMENT_LEFT, -1, 19, Color("bcd4dc"))

	if boss == null:
		return
	var state := boss.get_state()
	_draw_actor(TARGET_TRUE, state.get("true_hand", {}), "TRUE", Color("64e0db"), start_x, end_x, arena_y)
	_draw_actor(TARGET_FALSE, state.get("false_hand", {}), "FALSE", Color("e85a7d"), start_x, end_x, arena_y)
	_draw_actor(TARGET_BODY, state.get("body", {}), "CORE-00", Color("ec9b45"), start_x, end_x, arena_y)
	var player_point := _cell_point(player_position, start_x, end_x, arena_y)
	draw_circle(player_point + Vector2(0, -54), 31.0, Color("71b7ff"))
	draw_circle(player_point + Vector2(0, -54), 35.0, Color("d9f1ff"), false, 4.0)
	draw_string(ThemeDB.fallback_font, player_point + Vector2(-38, -94), "PLAYER", HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color("d9f1ff"))


func _cell_point(cell: int, start_x: float, end_x: float, y: float) -> Vector2:
	var ratio := float(clampi(cell, 1, BOARD_CELLS) - 1) / float(BOARD_CELLS - 1)
	return Vector2(lerpf(start_x, end_x, ratio), y)


func _draw_actor(target_id: StringName, actor: Dictionary, title: String, color: Color, start_x: float, end_x: float, y: float) -> void:
	if actor.is_empty() or not bool(actor.get("active", false)):
		return
	var point := _cell_point(int(actor.get("position", 1)), start_x, end_x, y)
	var actor_rect := Rect2(point + Vector2(-44, -154), Vector2(88, 72))
	draw_rect(actor_rect, color, true)
	draw_rect(actor_rect, Color.WHITE if selected_target_id == target_id else Color("294654"), false, 5.0)
	draw_string(ThemeDB.fallback_font, point + Vector2(-42, -164), title, HORIZONTAL_ALIGNMENT_LEFT, -1, 18, Color.WHITE)
	var health := "%d/%d +%d" % [int(actor.get("hp", 0)), int(actor.get("max_hp", 0)), int(actor.get("shield", 0))]
	draw_string(ThemeDB.fallback_font, point + Vector2(-42, -104), health, HORIZONTAL_ALIGNMENT_LEFT, -1, 16, Color("07121a"))


func _create_boss_manager() -> void:
	boss = MANAGER_SCRIPT.new() as Core00EnermyMannager
	boss.name = "Core00EnermyMannager"
	boss.profile = PROFILE_RESOURCE
	boss.auto_start = false
	boss.show_debug_panel = false
	add_child(boss)
	boss.phase_changed.connect(_on_boss_phase_changed)
	boss.player_damage_requested.connect(_on_player_damage_requested)
	boss.intent_changed.connect(_on_intent_changed)
	boss.card_jam_requested.connect(_on_card_jam_requested)
	boss.boss_defeated.connect(_on_boss_defeated)


func _load_card_catalog() -> void:
	cards = PipelineCardCatalogLoader.load_all_cards()
	card_by_id.clear()
	for card in cards:
		if card != null:
			card_by_id[card.id] = card
	if not cards.is_empty():
		selected_card_id = cards[0].id


func reset_battle() -> Dictionary:
	player_hp = clampi(starting_player_hp, 1, PLAYER_MAX_HP)
	player_shield = 0
	player_position = clampi(starting_player_position, 1, BOARD_CELLS)
	light_points = CARD_LIGHT_PER_TURN
	player_turn = 1
	free_move_available = true
	battle_over = false
	battle_result = &"playing"
	card_runtime.clear()
	for card in cards:
		card_runtime[card.id] = {
			"charge": 0,
			"cooldown_remaining": 0,
			"fresh_cooldown": false,
		}
	selected_target_id = TARGET_TRUE
	if selected_card_id.is_empty() and not cards.is_empty():
		selected_card_id = cards[0].id
	_log_entries.clear()
	boss.reset_encounter(player_position, player_hp)
	# The phase-one arena reserves cells 1 and 12 for the anchored claws.
	# Mirror the manager's validated position so Inspector values can never make
	# the UI/range model disagree with the authoritative combat state.
	player_position = boss.player_position
	_append_log("战斗开始：每回合获得2点亮格；充能保留，移动免费一次。")
	_refresh_ui()
	return get_battle_snapshot()


func get_battle_snapshot() -> Dictionary:
	return {
		"player_hp": player_hp,
		"player_max_hp": PLAYER_MAX_HP,
		"player_shield": player_shield,
		"player_position": player_position,
		"light_points": light_points,
		"player_turn": player_turn,
		"free_move_available": free_move_available,
		"selected_card_id": selected_card_id,
		"selected_target_id": selected_target_id,
		"battle_over": battle_over,
		"battle_result": battle_result,
		"cards": card_runtime.duplicate(true),
		"boss": boss.get_state() if boss != null else {},
	}


func get_card_state(card_id: StringName) -> Dictionary:
	if not card_runtime.has(card_id):
		return {}
	return (card_runtime[card_id] as Dictionary).duplicate(true)


func get_card_data(card_id: StringName) -> CardData:
	return card_by_id.get(card_id) as CardData


func get_active_target_ids() -> Array[StringName]:
	var result: Array[StringName] = []
	if boss == null:
		return result
	if boss.phase == Core00EnermyMannager.Phase.HANDS:
		if _target_is_active(TARGET_TRUE):
			result.append(TARGET_TRUE)
		if _target_is_active(TARGET_FALSE):
			result.append(TARGET_FALSE)
	elif boss.phase == Core00EnermyMannager.Phase.BODY and _target_is_active(TARGET_BODY):
		result.append(TARGET_BODY)
	return result


func get_target_distance(target_id: StringName = selected_target_id) -> int:
	var actor := _get_target_actor(target_id)
	if actor.is_empty() or not bool(actor.get("active", false)):
		return -1
	return absi(player_position - int(actor.get("position", player_position)))


func select_card(card_id: StringName) -> bool:
	if not card_by_id.has(card_id):
		return false
	selected_card_id = card_id
	_refresh_ui()
	return true


func select_target(target_id: StringName) -> bool:
	if not _target_is_active(target_id):
		return false
	selected_target_id = target_id
	_refresh_ui()
	return true


func charge_selected_card() -> Dictionary:
	return charge_card(selected_card_id, 1)


func charge_card(card_id: StringName, requested_points: int = 1) -> Dictionary:
	var result := {"ok": false, "card_id": card_id, "added": 0, "reason": ""}
	if battle_over:
		result["reason"] = "战斗已经结束"
		return result
	var card := get_card_data(card_id)
	if card == null:
		result["reason"] = "找不到卡牌"
		return result
	var runtime := card_runtime[card_id] as Dictionary
	if int(runtime.get("cooldown_remaining", 0)) > 0:
		result["reason"] = "卡牌仍在冷却"
		return result
	var capacity := maxi(1, card.shape_offsets.size())
	var missing := capacity - int(runtime.get("charge", 0))
	if missing <= 0:
		result["reason"] = "卡牌已经全部点亮"
		return result
	var added := mini(maxi(0, requested_points), mini(missing, light_points))
	if added <= 0:
		result["reason"] = "本回合没有剩余亮格点"
		return result
	runtime["charge"] = int(runtime.get("charge", 0)) + added
	light_points -= added
	result["ok"] = true
	result["added"] = added
	result["charge"] = runtime["charge"]
	result["capacity"] = capacity
	_append_log("点亮《%s》 %d格（%d/%d）。" % [card.display_name, added, int(runtime["charge"]), capacity])
	_refresh_ui()
	return result


func use_selected_card() -> Dictionary:
	return use_card(selected_card_id, selected_target_id)


func use_card(card_id: StringName, target_id: StringName = selected_target_id) -> Dictionary:
	var result := {"ok": false, "card_id": card_id, "target_id": target_id, "reason": "", "effects": []}
	if battle_over:
		result["reason"] = "战斗已经结束"
		return result
	var card := get_card_data(card_id)
	if card == null:
		result["reason"] = "找不到卡牌"
		return result
	var runtime := card_runtime[card_id] as Dictionary
	if int(runtime.get("cooldown_remaining", 0)) > 0:
		result["reason"] = "卡牌仍在冷却"
		return result
	var capacity := maxi(1, card.shape_offsets.size())
	if int(runtime.get("charge", 0)) < capacity:
		result["reason"] = "卡牌尚未全部点亮"
		return result
	if card.has_damage_effect():
		if not _target_is_active(target_id):
			result["reason"] = "请选择仍存活的目标"
			return result
		var distance := get_target_distance(target_id)
		if distance < card.min_range or distance > card.max_range:
			result["reason"] = "目标距离%d，不在射程%d-%d" % [distance, card.min_range, card.max_range]
			return result

	runtime["charge"] = 0
	runtime["cooldown_remaining"] = maxi(0, card.cooldown_turns)
	runtime["fresh_cooldown"] = card.cooldown_turns > 0
	var effects_result: Array = []
	if card.effects.is_empty():
		effects_result.append(_execute_legacy_effect(card, target_id))
	else:
		for effect in card.effects:
			if effect != null:
				effects_result.append(_execute_effect(effect, target_id))
	result["ok"] = true
	result["effects"] = effects_result
	result["cooldown_started"] = card.cooldown_turns
	_append_log("使用《%s》：%s" % [card.display_name, card.effect_summary()])
	_sanitize_target()
	_refresh_ui()
	return result


func move_player_adjacent(direction: int) -> Dictionary:
	var result := {"ok": false, "from": player_position, "to": player_position, "reason": ""}
	if battle_over:
		result["reason"] = "战斗已经结束"
		return result
	if not free_move_available:
		result["reason"] = "本回合的免费移动已经使用"
		return result
	var step := signi(direction)
	if step == 0:
		result["reason"] = "移动方向无效"
		return result
	var destination := player_position + step
	if destination < 1 or destination > BOARD_CELLS:
		result["reason"] = "已经到达战场边缘"
		return result
	if _cell_has_active_enemy(destination):
		result["reason"] = "不能移动到敌人所在格"
		return result
	player_position = destination
	free_move_available = false
	boss.set_debug_snapshot({"player_position": player_position, "player_hp": player_hp})
	result["ok"] = true
	result["to"] = player_position
	_append_log("玩家免费移动到第%d格。" % player_position)
	_refresh_ui()
	return result


func end_player_turn() -> Dictionary:
	if battle_over:
		return {"ok": false, "reason": "战斗已经结束"}
	_tick_card_cooldowns_after_full_turn()
	_append_log("-- Core-00 行动 --")
	var enemy_result := boss.advance_enemy_turn()
	if not battle_over and player_hp > 0:
		player_turn += 1
		light_points = CARD_LIGHT_PER_TURN
		free_move_available = true
		_apply_pending_jam()
		_append_log("-- 玩家回合%d：获得2点亮格 --" % player_turn)
	_refresh_ui()
	return {"ok": true, "enemy_result": enemy_result, "snapshot": get_battle_snapshot()}


func _execute_effect(effect: CombatEffectData, target_id: StringName) -> Dictionary:
	var outcome := {
		"type": effect.type,
		"type_key": effect.type_key(),
		"target": effect.target,
		"amount": effect.amount,
		"applied": 0,
	}
	match effect.type:
		CombatEffectData.Type.DAMAGE:
			if effect.target == CombatEffectData.Target.PLAYER:
				outcome["applied"] = _apply_direct_player_damage(effect.amount)
			else:
				outcome["applied"] = boss.take_damage(target_id, effect.amount)
		CombatEffectData.Type.SHIELD:
			if effect.target == CombatEffectData.Target.PLAYER:
				player_shield += effect.amount
				outcome["applied"] = effect.amount
			else:
				outcome["applied"] = _add_enemy_shield(target_id, effect.amount)
		CombatEffectData.Type.HEAL:
			if effect.target == CombatEffectData.Target.PLAYER:
				var before := player_hp
				player_hp = mini(PLAYER_MAX_HP, player_hp + effect.amount)
				boss.player_hp = player_hp
				outcome["applied"] = player_hp - before
			else:
				outcome["applied"] = _heal_enemy(target_id, effect.amount)
		CombatEffectData.Type.MOVE_TOWARD_OPPONENT:
			outcome["applied"] = _move_effect(effect.target, target_id, effect.amount, true)
		CombatEffectData.Type.MOVE_AWAY_FROM_OPPONENT:
			outcome["applied"] = _move_effect(effect.target, target_id, effect.amount, false)
		CombatEffectData.Type.ENERGY:
			if effect.target == CombatEffectData.Target.PLAYER:
				light_points += effect.amount
				outcome["applied"] = effect.amount
	return outcome


func _execute_legacy_effect(card: CardData, target_id: StringName) -> Dictionary:
	match card.effect_type:
		"damage":
			return {"type_key": &"damage", "applied": boss.take_damage(target_id, card.effect_value)}
		"shield":
			player_shield += card.effect_value
			return {"type_key": &"shield", "applied": card.effect_value}
		"heal":
			var before := player_hp
			player_hp = mini(PLAYER_MAX_HP, player_hp + card.effect_value)
			boss.player_hp = player_hp
			return {"type_key": &"heal", "applied": player_hp - before}
		"energy":
			light_points += card.effect_value
			return {"type_key": &"energy", "applied": card.effect_value}
	return {"type_key": &"unknown", "applied": 0}


func _move_effect(target: int, target_id: StringName, amount: int, toward: bool) -> int:
	var steps := maxi(0, amount)
	if steps <= 0:
		return 0
	if target == CombatEffectData.Target.PLAYER:
		var actor := _get_target_actor(target_id)
		if actor.is_empty():
			return 0
		var enemy_position := int(actor.get("position", player_position))
		var direction := signi(enemy_position - player_position)
		if not toward:
			direction *= -1
		var old := player_position
		for _step in range(steps):
			var next := player_position + direction
			if next < 1 or next > BOARD_CELLS or _cell_has_active_enemy(next):
				break
			player_position = next
		boss.set_debug_snapshot({"player_position": player_position, "player_hp": player_hp})
		return absi(player_position - old)

	var enemy := _get_target_actor(target_id)
	if enemy.is_empty() or not bool(enemy.get("active", false)):
		return 0
	# Core-00's phase-one claws are bolted to the two arena ends.  Push effects
	# still deal their configured damage, but cannot displace either claw.
	if boss.phase == Core00EnermyMannager.Phase.HANDS \
		and target_id in [TARGET_TRUE, TARGET_FALSE]:
		return 0
	var old_enemy := int(enemy.get("position", player_position))
	var enemy_direction := signi(player_position - old_enemy)
	if not toward:
		enemy_direction *= -1
	for _step in range(steps):
		var next_enemy := int(enemy.get("position", old_enemy)) + enemy_direction
		if next_enemy < 1 or next_enemy > BOARD_CELLS or next_enemy == player_position:
			break
		enemy["position"] = next_enemy
	return absi(int(enemy.get("position", old_enemy)) - old_enemy)


func _apply_direct_player_damage(amount: int) -> int:
	var remaining := maxi(0, amount)
	var absorbed := mini(player_shield, remaining)
	player_shield -= absorbed
	remaining -= absorbed
	var before := player_hp
	player_hp = maxi(0, player_hp - remaining)
	boss.player_hp = player_hp
	if player_hp <= 0:
		_finish_battle(&"defeat")
	return before - player_hp + absorbed


func _add_enemy_shield(target_id: StringName, amount: int) -> int:
	var actor := _get_target_actor(target_id)
	if actor.is_empty() or not bool(actor.get("active", false)):
		return 0
	actor["shield"] = int(actor.get("shield", 0)) + maxi(0, amount)
	return maxi(0, amount)


func _heal_enemy(target_id: StringName, amount: int) -> int:
	var actor := _get_target_actor(target_id)
	if actor.is_empty() or not bool(actor.get("active", false)):
		return 0
	var before := int(actor.get("hp", 0))
	actor["hp"] = mini(int(actor.get("max_hp", before)), before + maxi(0, amount))
	return int(actor["hp"]) - before


func _tick_card_cooldowns_after_full_turn() -> void:
	for card_id: Variant in card_runtime.keys():
		var runtime := card_runtime[card_id] as Dictionary
		if bool(runtime.get("fresh_cooldown", false)):
			runtime["fresh_cooldown"] = false
		elif int(runtime.get("cooldown_remaining", 0)) > 0:
			runtime["cooldown_remaining"] = int(runtime["cooldown_remaining"]) - 1


func _apply_pending_jam() -> int:
	var runtime_cards: Array = []
	var ordered_ids: Array = []
	for card in cards:
		runtime_cards.append(card_runtime[card.id])
		ordered_ids.append(card.id)
	var turns := boss.begin_player_turn(runtime_cards)
	for index in range(ordered_ids.size()):
		var runtime := runtime_cards[index] as Dictionary
		runtime["fresh_cooldown"] = false
		card_runtime[ordered_ids[index]] = runtime
	if turns > 0:
		_append_log("Core-00 干扰：全部卡牌进入%d回合冷却；已有更长冷却不缩短。" % turns)
	return turns


func _get_target_actor(target_id: StringName) -> Dictionary:
	if boss == null:
		return {}
	match target_id:
		TARGET_TRUE:
			return boss.true_hand
		TARGET_FALSE:
			return boss.false_hand
		TARGET_BODY:
			return boss.body
	return {}


func _target_is_active(target_id: StringName) -> bool:
	var actor := _get_target_actor(target_id)
	return not actor.is_empty() and bool(actor.get("active", false)) and int(actor.get("hp", 0)) > 0


func _cell_has_active_enemy(cell: int) -> bool:
	for target_id in get_active_target_ids():
		var actor := _get_target_actor(target_id)
		if int(actor.get("position", -1)) == cell:
			return true
	return false


func _sanitize_target() -> void:
	if _target_is_active(selected_target_id):
		return
	var candidates := get_active_target_ids()
	if not candidates.is_empty():
		selected_target_id = candidates[0]


func _on_boss_phase_changed(_phase: int) -> void:
	_sanitize_target()
	if boss != null and boss.phase == Core00EnermyMannager.Phase.BODY:
		selected_target_id = TARGET_BODY
		_append_log("Core-00 第二阶段启动：灵活远程本体，50生命。")
	_refresh_ui()


func _on_player_damage_requested(amount: int, source_id: StringName) -> void:
	var absorbed := mini(player_shield, maxi(0, amount))
	player_shield -= absorbed
	var hp_damage := maxi(0, amount - absorbed)
	player_hp = maxi(0, player_hp - hp_damage)
	# The standalone manager already subtracts from its debug HP before emitting.
	# This battle owns shield resolution, so synchronize the authoritative value.
	boss.player_hp = player_hp
	_append_log("%s造成%d伤害（护盾吸收%d，生命-%d）。" % [String(source_id), amount, absorbed, hp_damage])
	if player_hp <= 0:
		_finish_battle(&"defeat")
	_refresh_ui()


func _on_intent_changed(_intent: Dictionary) -> void:
	_refresh_ui()


func _on_card_jam_requested(turns: int) -> void:
	_append_log("警告：Core-00 准备让全部卡牌冷却%d回合。" % turns)


func _on_boss_defeated() -> void:
	_finish_battle(&"victory")


func _finish_battle(result: StringName) -> void:
	if battle_over:
		return
	battle_over = true
	battle_result = result
	_append_log("胜利：Core-00 已停止运行。" if result == &"victory" else "失败：玩家失去战斗能力。")
	_refresh_ui()


func _append_log(message: String) -> void:
	_log_entries.append(message)
	while _log_entries.size() > 18:
		_log_entries.remove_at(0)
	if _log_label != null:
		_log_label.text = "\n".join(_log_entries)
		_log_label.scroll_to_line(maxi(0, _log_entries.size() - 1))


# ---------------------------------------------------------------------------
# Debug/test API
# ---------------------------------------------------------------------------

func debug_reset(new_player_position: int = 6, new_player_hp: int = PLAYER_MAX_HP) -> Dictionary:
	starting_player_position = clampi(new_player_position, 1, BOARD_CELLS)
	starting_player_hp = clampi(new_player_hp, 1, PLAYER_MAX_HP)
	return reset_battle()


func debug_set_light_points(value: int) -> void:
	light_points = maxi(0, value)
	_refresh_ui()


func debug_set_card_charge(card_id: StringName, value: int) -> bool:
	var card := get_card_data(card_id)
	if card == null:
		return false
	var runtime := card_runtime[card_id] as Dictionary
	runtime["charge"] = clampi(value, 0, maxi(1, card.shape_offsets.size()))
	_refresh_ui()
	return true


func debug_set_player_position(value: int) -> bool:
	var next := clampi(value, 1, BOARD_CELLS)
	if _cell_has_active_enemy(next):
		return false
	player_position = next
	boss.set_debug_snapshot({"player_position": player_position, "player_hp": player_hp})
	_refresh_ui()
	return true


func debug_damage_target(target_id: StringName, amount: int) -> int:
	var received := boss.take_damage(target_id, amount)
	_sanitize_target()
	_refresh_ui()
	return received


func debug_force_phase_two() -> Dictionary:
	var state := boss.force_phase_two()
	selected_target_id = TARGET_BODY
	_refresh_ui()
	return state


func debug_queue_jam(turns: int = 1) -> void:
	boss.pending_card_jam_turns = maxi(boss.pending_card_jam_turns, maxi(0, turns))


func debug_apply_pending_jam() -> int:
	var turns := _apply_pending_jam()
	_refresh_ui()
	return turns


# ---------------------------------------------------------------------------
# UI
# ---------------------------------------------------------------------------

func _build_ui() -> void:
	_ui_built = true
	_header_label = _make_label("CORE-00 / GRID-LIGHT BATTLE", Vector2(28, 17), Vector2(1360, 58), 32)
	_header_label.add_theme_color_override("font_color", Color("9feaf2"))
	_intent_label = _make_label("", Vector2(28, 112), Vector2(1380, 64), 22)
	_intent_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART

	var cards_title := _make_label("14张卡牌 · 点击选择", Vector2(36, 518), Vector2(500, 40), 23)
	cards_title.add_theme_color_override("font_color", Color("9feaf2"))
	var grid := GridContainer.new()
	grid.position = Vector2(30, 560)
	grid.size = Vector2(1384, 480)
	grid.columns = 7
	grid.add_theme_constant_override("h_separation", 8)
	grid.add_theme_constant_override("v_separation", 10)
	add_child(grid)
	for card in cards:
		var button := Button.new()
		button.custom_minimum_size = Vector2(190, 224)
		button.text_overrun_behavior = TextServer.OVERRUN_TRIM_ELLIPSIS
		button.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		button.add_theme_font_size_override("font_size", 18)
		button.add_theme_color_override("font_color", Color("c8d6dc"))
		button.add_theme_color_override("font_hover_color", Color.WHITE)
		button.pressed.connect(_on_card_pressed.bind(card.id))
		button.tooltip_text = card.description
		grid.add_child(button)
		_card_buttons[card.id] = button

	var side_title := _make_label("操作台", Vector2(1472, 122), Vector2(380, 42), 28)
	side_title.add_theme_color_override("font_color", Color("f4c97b"))
	_make_label("目标", Vector2(1472, 178), Vector2(120, 32), 19)
	var target_row := HBoxContainer.new()
	target_row.position = Vector2(1472, 212)
	target_row.size = Vector2(402, 44)
	target_row.add_theme_constant_override("separation", 8)
	add_child(target_row)
	for pair in [[TARGET_TRUE, "TRUE"], [TARGET_FALSE, "FALSE"], [TARGET_BODY, "本体"]]:
		var target_button := Button.new()
		target_button.toggle_mode = true
		target_button.custom_minimum_size = Vector2(126, 44)
		target_button.text = pair[1]
		target_button.pressed.connect(_on_target_pressed.bind(pair[0]))
		target_row.add_child(target_button)
		_target_buttons[pair[0]] = target_button

	_selection_label = RichTextLabel.new()
	_selection_label.position = Vector2(1472, 274)
	_selection_label.size = Vector2(402, 174)
	_selection_label.bbcode_enabled = true
	_selection_label.fit_content = false
	_selection_label.add_theme_font_size_override("normal_font_size", 19)
	add_child(_selection_label)

	_charge_button = _make_button("点亮1格", Vector2(1472, 458), Vector2(194, 56), _on_charge_pressed)
	_use_button = _make_button("使用卡牌", Vector2(1680, 458), Vector2(194, 56), _on_use_pressed)
	_make_button("← 免费移动", Vector2(1472, 528), Vector2(194, 52), _on_move_left)
	_make_button("免费移动 →", Vector2(1680, 528), Vector2(194, 52), _on_move_right)
	_make_button("结束玩家回合", Vector2(1472, 594), Vector2(402, 58), _on_end_turn)
	_make_button("重新开始", Vector2(1472, 666), Vector2(402, 46), _on_reset_pressed)
	_make_label("快捷键：A/D 或 ←/→ 移动　Space/Enter 结束回合", Vector2(1472, 716), Vector2(410, 26), 14)

	_make_label("战斗记录", Vector2(1472, 748), Vector2(220, 36), 22)
	_log_label = RichTextLabel.new()
	_log_label.position = Vector2(1472, 790)
	_log_label.size = Vector2(402, 242)
	_log_label.bbcode_enabled = false
	_log_label.scroll_active = true
	_log_label.add_theme_font_size_override("normal_font_size", 16)
	add_child(_log_label)


func _make_label(text_value: String, at: Vector2, dimensions: Vector2, font_size: int) -> Label:
	var label := Label.new()
	label.text = text_value
	label.position = at
	label.size = dimensions
	label.add_theme_font_size_override("font_size", font_size)
	add_child(label)
	return label


func _make_button(text_value: String, at: Vector2, dimensions: Vector2, callback: Callable) -> Button:
	var button := Button.new()
	button.text = text_value
	button.position = at
	button.size = dimensions
	button.add_theme_font_size_override("font_size", 19)
	button.pressed.connect(callback)
	add_child(button)
	return button


func _refresh_ui() -> void:
	queue_redraw()
	if not _ui_built:
		return
	var phase_text := "双手机制" if boss.phase == Core00EnermyMannager.Phase.HANDS else "灵活本体"
	if boss.phase == Core00EnermyMannager.Phase.DEFEATED:
		phase_text = "已击败"
	_header_label.text = "CORE-00  |  回合 %d  |  HP %d/%d  护盾 %d  |  亮格 %d  |  位置 %d  |  %s" % [
		player_turn, player_hp, PLAYER_MAX_HP, player_shield, light_points, player_position, phase_text,
	]
	var intent := boss.current_intent
	_intent_label.text = "敌人意图：%s" % String(intent.get("intent_text", "等待"))
	_sanitize_target()
	for target_id: Variant in _target_buttons.keys():
		var button := _target_buttons[target_id] as Button
		button.disabled = not _target_is_active(target_id) or battle_over
		button.button_pressed = selected_target_id == target_id

	for card in cards:
		var button := _card_buttons.get(card.id) as Button
		if button == null:
			continue
		var runtime := card_runtime.get(card.id, {}) as Dictionary
		var capacity := maxi(1, card.shape_offsets.size())
		var charge := int(runtime.get("charge", 0))
		var cooldown := int(runtime.get("cooldown_remaining", 0))
		var charge_bar := "■".repeat(charge) + "□".repeat(capacity - charge)
		button.text = "%s\n%s  CD:%d\n%s\n%s" % [card.display_name, charge_bar, cooldown, card.range_text(), card.effect_summary()]
		button.disabled = battle_over
		button.modulate = Color.WHITE if selected_card_id == card.id else Color(0.72, 0.78, 0.82, 1)

	var selected := get_card_data(selected_card_id)
	if selected != null:
		var state := card_runtime[selected.id] as Dictionary
		var distance := get_target_distance(selected_target_id)
		_selection_label.text = "[font_size=24][color=#f4c97b]%s[/color][/font_size]\n%s\n充能 %d/%d　冷却 %d\n当前目标 %s　距离 %s" % [
			selected.display_name,
			selected.description,
			int(state.get("charge", 0)),
			maxi(1, selected.shape_offsets.size()),
			int(state.get("cooldown_remaining", 0)),
			_target_display_name(selected_target_id),
			str(distance) if distance >= 0 else "-",
		]
		_charge_button.disabled = battle_over or light_points <= 0 or int(state.get("cooldown_remaining", 0)) > 0 or int(state.get("charge", 0)) >= maxi(1, selected.shape_offsets.size())
		_use_button.disabled = battle_over or int(state.get("cooldown_remaining", 0)) > 0 or int(state.get("charge", 0)) < maxi(1, selected.shape_offsets.size())
	if _log_label != null:
		_log_label.text = "\n".join(_log_entries)


func _target_display_name(target_id: StringName) -> String:
	match target_id:
		TARGET_TRUE: return "True手"
		TARGET_FALSE: return "False手"
		TARGET_BODY: return "Core-00本体"
	return "无"


func _on_card_pressed(card_id: StringName) -> void:
	select_card(card_id)


func _on_target_pressed(target_id: StringName) -> void:
	select_target(target_id)


func _on_charge_pressed() -> void:
	var result := charge_selected_card()
	if not bool(result.get("ok", false)):
		_append_log("无法点亮：%s。" % String(result.get("reason", "未知原因")))


func _on_use_pressed() -> void:
	var result := use_selected_card()
	if not bool(result.get("ok", false)):
		_append_log("无法使用：%s。" % String(result.get("reason", "未知原因")))


func _on_move_left() -> void:
	var result := move_player_adjacent(-1)
	if not bool(result.get("ok", false)):
		_append_log("无法移动：%s。" % String(result.get("reason", "未知原因")))


func _on_move_right() -> void:
	var result := move_player_adjacent(1)
	if not bool(result.get("ok", false)):
		_append_log("无法移动：%s。" % String(result.get("reason", "未知原因")))


func _on_end_turn() -> void:
	end_player_turn()


func _on_reset_pressed() -> void:
	reset_battle()
