extends Node
class_name Core00EnermyMannager

## Standalone two-phase controller for Core-00.
##
## This deliberately does not patch or inherit the project's existing
## single-enemy manager.  Consumers can connect to the request signals below,
## or use the public manual/debug API in isolation.

signal phase_changed(phase: int)
signal hand_health_changed(hand_id: StringName, current: int, maximum: int, shield: int)
signal body_health_changed(current: int, maximum: int, shield: int)
signal intent_changed(intent: Dictionary)
signal intent_executed(result: Dictionary)
signal player_damage_requested(amount: int, source_id: StringName)
signal player_logic_state_changed(state: StringName)
signal card_jam_requested(turns: int)
signal boss_defeated()

enum Phase {
	HANDS = 1,
	BODY = 2,
	DEFEATED = 3,
}

const HAND_TRUE := &"true_hand"
const HAND_FALSE := &"false_hand"
const LOGIC_NONE := &"none"
const LOGIC_TRUE := &"true"
const LOGIC_FALSE := &"false"

@export_group("Configuration")
@export var profile: Core00Profile
@export var show_debug_panel: bool = true
@export var auto_start: bool = true

@export_group("Standalone Inspector Debug")
@export_range(1, 99, 1) var debug_player_position: int = 6
@export_range(1, 9999, 1) var debug_player_hp: int = 100

var phase: Phase = Phase.HANDS
var cycle_step: int = 1
var round_number: int = 1
var phase_two_turn: int = 0
var player_position: int = 6
var player_hp: int = 100
var player_logic_state: StringName = LOGIC_NONE
var true_hand: Dictionary = {}
var false_hand: Dictionary = {}
var body: Dictionary = {}
var cooldowns: Dictionary = {}
var pending_card_jam_turns: int = 0
var damage_received_this_player_turn: int = 0
var true_death_loop_active: bool = false
var false_heal_pending: bool = false
var current_intent: Dictionary = {}
var locked_intent: Dictionary = {}
var _defeat_emitted: bool = false


func _ready() -> void:
	if auto_start:
		reset_encounter(debug_player_position, debug_player_hp)
	_update_debug_panel()


func reset_encounter(new_player_position: int = 6, new_player_hp: int = 100) -> Dictionary:
	_ensure_profile()
	phase = Phase.HANDS
	cycle_step = 1
	round_number = 1
	phase_two_turn = 0
	player_position = _clamp_cell(new_player_position)
	player_hp = maxi(0, new_player_hp)
	player_logic_state = LOGIC_NONE
	true_hand = _make_actor(profile.hand_max_health, profile.true_hand_start_cell)
	false_hand = _make_actor(profile.hand_max_health, profile.false_hand_start_cell)
	body = _make_actor(profile.body_max_health, profile.body_start_cell)
	body["active"] = false
	_ensure_distinct_positions()
	cooldowns = {}
	pending_card_jam_turns = 0
	damage_received_this_player_turn = 0
	true_death_loop_active = false
	false_heal_pending = false
	current_intent = {}
	locked_intent = {}
	_defeat_emitted = false
	refresh_intent_preview()
	emit_signal("phase_changed", phase)
	_emit_all_health()
	return get_state()


func get_state() -> Dictionary:
	return {
		"phase": phase,
		"cycle_step": cycle_step,
		"round_number": round_number,
		"phase_two_turn": phase_two_turn,
		"cell_count": _cell_count(),
		"player_position": player_position,
		"player_hp": player_hp,
		"player_logic_state": player_logic_state,
		"true_hand": true_hand.duplicate(true),
		"false_hand": false_hand.duplicate(true),
		"body": body.duplicate(true),
		"distance": get_body_distance(),
		"cooldowns": cooldowns.duplicate(true),
		"pending_card_jam_turns": pending_card_jam_turns,
		"damage_received_this_player_turn": damage_received_this_player_turn,
		"true_death_loop_active": true_death_loop_active,
		"false_heal_pending": false_heal_pending,
		"current_intent": current_intent.duplicate(true),
		"locked_intent": locked_intent.duplicate(true),
	}


func set_debug_snapshot(snapshot: Dictionary) -> Dictionary:
	if snapshot.has("player_position"):
		player_position = _clamp_cell(int(snapshot["player_position"]))
	if snapshot.has("player_hp"):
		player_hp = maxi(0, int(snapshot["player_hp"]))
	if snapshot.has("cycle_step"):
		cycle_step = clampi(int(snapshot["cycle_step"]), 1, 5)
	if snapshot.has("round_number"):
		round_number = maxi(1, int(snapshot["round_number"]))
		if phase == Phase.BODY and not snapshot.has("phase_two_turn"):
			phase_two_turn = round_number
	if snapshot.has("phase_two_turn"):
		phase_two_turn = maxi(0, int(snapshot["phase_two_turn"]))
	if snapshot.has("damage_received_this_player_turn"):
		damage_received_this_player_turn = maxi(0, int(snapshot["damage_received_this_player_turn"]))
	if snapshot.has("cooldowns") and snapshot["cooldowns"] is Dictionary:
		cooldowns = (snapshot["cooldowns"] as Dictionary).duplicate(true)
	if snapshot.has("body_position"):
		body["position"] = _clamp_actor_cell(int(snapshot["body_position"]))
	if snapshot.has("body_hp"):
		body["hp"] = clampi(int(snapshot["body_hp"]), 0, int(body.get("max_hp", 1)))
	if snapshot.has("true_hp"):
		true_hand["hp"] = clampi(int(snapshot["true_hp"]), 0, int(true_hand.get("max_hp", 1)))
	if snapshot.has("false_hp"):
		false_hand["hp"] = clampi(int(snapshot["false_hp"]), 0, int(false_hand.get("max_hp", 1)))
	current_intent = {}
	locked_intent = {}
	_handle_deaths()
	_ensure_distinct_positions()
	refresh_intent_preview()
	_emit_all_health()
	return get_state()


func force_phase_two() -> Dictionary:
	true_hand["hp"] = 0
	true_hand["shield"] = 0
	true_hand["active"] = false
	false_hand["hp"] = 0
	false_hand["shield"] = 0
	false_hand["active"] = false
	emit_signal("hand_health_changed", HAND_TRUE, 0, int(true_hand.get("max_hp", profile.hand_max_health)), 0)
	emit_signal("hand_health_changed", HAND_FALSE, 0, int(false_hand.get("max_hp", profile.hand_max_health)), 0)
	_enter_phase_two()
	return get_state()


func get_body_distance() -> int:
	if body.is_empty():
		return 0
	return absi(int(body.get("position", 1)) - player_position)


func get_cooldown(action_id: StringName) -> int:
	return maxi(0, int(cooldowns.get(action_id, cooldowns.get(String(action_id), 0))))


func notify_player_action(snapshot: Dictionary = {}) -> Dictionary:
	## Optional integration point.  Call this after a player action; damage should
	## still be applied through take_damage so shields and the death loop work.
	set_debug_snapshot(snapshot)
	return refresh_intent_preview()


func take_damage(target_id: StringName, amount: int) -> int:
	if amount <= 0 or phase == Phase.DEFEATED:
		return 0
	var actor := _actor_for(target_id)
	if actor.is_empty() or not bool(actor.get("active", false)) or int(actor.get("hp", 0)) <= 0:
		return 0
	var old_total := int(actor.get("hp", 0)) + int(actor.get("shield", 0))
	var remaining := amount
	var shield_damage := mini(int(actor.get("shield", 0)), remaining)
	actor["shield"] = int(actor.get("shield", 0)) - shield_damage
	remaining -= shield_damage
	if remaining > 0:
		var next_hp := maxi(0, int(actor.get("hp", 0)) - remaining)
		if target_id == HAND_TRUE and true_death_loop_active:
			next_hp = maxi(1, next_hp)
		actor["hp"] = next_hp
	var received := old_total - int(actor.get("hp", 0)) - int(actor.get("shield", 0))
	if target_id == &"body":
		damage_received_this_player_turn += received
		emit_signal("body_health_changed", int(actor["hp"]), int(actor["max_hp"]), int(actor["shield"]))
	else:
		emit_signal("hand_health_changed", target_id, int(actor["hp"]), int(actor["max_hp"]), int(actor["shield"]))
	current_intent = {}
	locked_intent = {}
	_handle_deaths()
	if phase != Phase.DEFEATED and current_intent.is_empty():
		refresh_intent_preview()
	_update_debug_panel()
	return received


func refresh_intent_preview() -> Dictionary:
	if profile == null:
		current_intent = _wait_intent("Missing Core00Profile")
	elif phase == Phase.HANDS:
		current_intent = _phase_one_intent()
	elif phase == Phase.BODY:
		current_intent = _phase_two_intent()
	else:
		current_intent = _wait_intent("Core-00 defeated")
	emit_signal("intent_changed", current_intent.duplicate(true))
	_update_debug_panel()
	return current_intent.duplicate(true)


func lock_intent() -> Dictionary:
	if current_intent.is_empty():
		refresh_intent_preview()
	locked_intent = current_intent.duplicate(true)
	return locked_intent.duplicate(true)


func execute_locked_intent() -> Dictionary:
	if locked_intent.is_empty():
		lock_intent()
	var result := locked_intent.duplicate(true)
	if phase == Phase.HANDS:
		_execute_phase_one_step(result)
	elif phase == Phase.BODY:
		_execute_phase_two_action(result)
	result["executed"] = true
	locked_intent = {}
	current_intent = {}
	emit_signal("intent_executed", result.duplicate(true))
	_update_debug_panel()
	return result


func advance_enemy_turn() -> Dictionary:
	## Complete production turn entry point. It locks the final preview, executes
	## exactly once, advances the appropriate phase clock, clears the damage
	## reaction window, and prepares the next preview.
	if current_intent.is_empty():
		refresh_intent_preview()
	lock_intent()
	var result := execute_locked_intent()
	if phase != Phase.DEFEATED:
		round_number += 1
		if phase == Phase.BODY:
			phase_two_turn += 1
		damage_received_this_player_turn = 0
		refresh_intent_preview()
	result["state_after"] = get_state()
	return result


func advance_enemy_turn_for_test() -> Dictionary:
	## Backward-compatible alias retained for the existing headless suite.
	return advance_enemy_turn()


func begin_player_turn(runtime_cards: Array = []) -> int:
	## Applies a queued Core-00 jam without depending on the existing board code.
	## Dictionaries and objects with a cooldown_remaining property are supported.
	var turns := pending_card_jam_turns
	if turns <= 0:
		return 0
	for index in range(runtime_cards.size()):
		var card: Variant = runtime_cards[index]
		if card is Dictionary:
			var card_dict := card as Dictionary
			card_dict["cooldown_remaining"] = maxi(int(card_dict.get("cooldown_remaining", 0)), turns)
			runtime_cards[index] = card_dict
		elif card is Object and _has_property(card as Object, &"cooldown_remaining"):
			var card_object := card as Object
			card_object.set("cooldown_remaining", maxi(int(card_object.get("cooldown_remaining")), turns))
			if _has_property(card_object, &"is_ready"):
				card_object.set("is_ready", false)
	pending_card_jam_turns = 0
	_update_debug_panel()
	return turns


func debug_advance_turn() -> void:
	advance_enemy_turn_for_test()


func debug_damage_true() -> void:
	take_damage(HAND_TRUE, 5)
	refresh_intent_preview()


func debug_damage_false() -> void:
	take_damage(HAND_FALSE, 5)
	refresh_intent_preview()


func debug_damage_body() -> void:
	take_damage(&"body", 5)
	refresh_intent_preview()


func debug_force_phase_two() -> void:
	force_phase_two()


func _phase_one_intent() -> Dictionary:
	var even_cell := player_position % 2 == 0
	var true_id: StringName
	var true_text: String
	var false_id: StringName
	var false_text: String
	match cycle_step:
		1:
			true_id = &"protect_beam"
			true_text = "保护光束：扫描偶数格%s" % ("，命中玩家" if even_cell else "，未命中")
			false_id = &"charge"
			false_text = "蓄力并获得格挡"
		2:
			true_id = &"enter_death_loop"
			true_text = "进入死循环并获得格挡"
			false_id = &"charge_complete"
			false_text = "蓄力完成"
		3:
			true_id = &"death_loop"
			true_text = "维持死循环并获得格挡"
			false_id = &"break_beam"
			false_text = "破坏光束：扫描偶数格%s" % ("，命中并解除死循环" if even_cell else "，未命中")
		4:
			true_id = &"send_medkit"
			true_text = "给 False 发送治疗包"
			false_id = &"stunned"
			false_text = "晕眩"
		_:
			true_id = &"charge"
			true_text = "蓄力"
			false_id = &"heal"
			false_text = "使用治疗包恢复血量"
	if not _hand_is_alive(HAND_TRUE):
		true_id = &"destroyed"
		true_text = "已摧毁，无行动"
	if not _hand_is_alive(HAND_FALSE):
		false_id = &"destroyed"
		false_text = "已摧毁，无行动"
	return {
		"phase": Phase.HANDS,
		"id": StringName("core00_cycle_%d" % cycle_step),
		"cycle_step": cycle_step,
		"display_name": "双手循环 %d/5" % cycle_step,
		"intent_text": "True: %s | False: %s" % [true_text, false_text],
		"true_action": {"id": true_id, "text": true_text},
		"false_action": {"id": false_id, "text": false_text},
		"player_on_even_cell": even_cell,
	}


func _execute_phase_one_step(result: Dictionary) -> void:
	var even_cell := player_position % 2 == 0
	match cycle_step:
		1:
			if _hand_is_alive(HAND_TRUE) and even_cell:
				_damage_player(profile.phase_one_beam_damage, &"protect_beam")
				_set_player_logic_state(LOGIC_TRUE)
			if _hand_is_alive(HAND_FALSE):
				_add_hand_shield(HAND_FALSE, profile.phase_one_shield_gain)
		2:
			if _hand_is_alive(HAND_TRUE):
				true_death_loop_active = true
				_add_hand_shield(HAND_TRUE, profile.phase_one_shield_gain)
		3:
			if _hand_is_alive(HAND_TRUE):
				_add_hand_shield(HAND_TRUE, profile.phase_one_shield_gain)
			if _hand_is_alive(HAND_FALSE) and even_cell:
				_damage_player(profile.phase_one_beam_damage, &"break_beam")
				_set_player_logic_state(LOGIC_FALSE)
				true_death_loop_active = false
		4:
			if _hand_is_alive(HAND_TRUE) and _hand_is_alive(HAND_FALSE):
				false_heal_pending = true
		5:
			if _hand_is_alive(HAND_FALSE) and false_heal_pending:
				_heal_actor(false_hand, profile.phase_one_heal_amount)
				false_heal_pending = false
	result["player_hp_after"] = player_hp
	result["player_logic_state_after"] = player_logic_state
	result["true_death_loop_after"] = true_death_loop_active
	cycle_step = 1 if cycle_step >= 5 else cycle_step + 1
	_handle_deaths()


func _phase_two_intent() -> Dictionary:
	var distance := get_body_distance()
	var chosen: Core00ActionData
	var reaction := damage_received_this_player_turn >= profile.teleport_reaction_damage
	var teleport := profile.find_action(&"core00_teleport_defend")
	var jam := profile.find_action(&"core00_jam_cards")
	if reaction and _action_ready(teleport):
		chosen = teleport
	elif phase_two_turn > 0 \
		and phase_two_turn % maxi(1, profile.jam_every_n_rounds) == 0 \
		and _action_ready(jam):
		chosen = jam
	else:
		var preferred: Array[StringName] = []
		if distance == 1:
			preferred = [&"core00_gunstock"]
		elif distance >= 3 and distance <= 4:
			preferred = [&"core00_pulse"]
		elif distance >= 6:
			preferred = [&"core00_sniper"]
		for action_id: StringName in preferred:
			var candidate := profile.find_action(action_id)
			if _action_ready(candidate) and candidate.is_in_range(distance):
				chosen = candidate
				break
		if chosen == null:
			chosen = profile.find_action(&"core00_advance")
	if chosen == null:
		return _wait_intent("No configured phase-two action")
	var body_from := int(body.get("position", 1))
	var body_to := body_from
	if chosen.action_kind == Core00ActionData.ActionKind.MOVE:
		body_to = _project_smart_advance(body_from, chosen.move_min, chosen.move_max)
	elif chosen.action_kind == Core00ActionData.ActionKind.TELEPORT_DEFEND:
		body_to = _cell_behind_player(body_from)
	return {
		"phase": Phase.BODY,
		"id": chosen.runtime_id(),
		"display_name": chosen.display_name,
		"intent_text": chosen.intent_text,
		"action": chosen,
		"action_kind": chosen.action_kind,
		"distance": distance,
		"damage_per_hit": chosen.damage_per_hit,
		"hit_count": chosen.hit_count,
		"damage": chosen.total_damage(),
		"body_from": body_from,
		"body_to": body_to,
		"cooldown_turns": chosen.cooldown_turns,
		"is_reaction": reaction and chosen == teleport,
	}


func _execute_phase_two_action(result: Dictionary) -> void:
	var action := result.get("action") as Core00ActionData
	if action == null:
		_tick_existing_cooldowns()
		return
	if action.action_kind == Core00ActionData.ActionKind.ATTACK:
		for hit_index in range(maxi(1, action.hit_count)):
			_damage_player(action.damage_per_hit, action.runtime_id())
	elif action.action_kind == Core00ActionData.ActionKind.MOVE:
		body["position"] = int(result.get("body_to", body.get("position", 1)))
	elif action.action_kind == Core00ActionData.ActionKind.TELEPORT_DEFEND:
		body["position"] = int(result.get("body_to", body.get("position", 1)))
		body["shield"] = mini(profile.body_shield_cap, int(body.get("shield", 0)) + action.shield)
		emit_signal("body_health_changed", int(body["hp"]), int(body["max_hp"]), int(body["shield"]))
	elif action.action_kind == Core00ActionData.ActionKind.JAM_CARDS:
		pending_card_jam_turns = maxi(pending_card_jam_turns, profile.jammed_card_turns)
		emit_signal("card_jam_requested", profile.jammed_card_turns)
	_tick_existing_cooldowns()
	if action.cooldown_turns > 0:
		cooldowns[action.runtime_id()] = action.cooldown_turns
	result["player_hp_after"] = player_hp
	result["pending_card_jam_turns"] = pending_card_jam_turns


func _action_ready(action: Core00ActionData) -> bool:
	return action != null and get_cooldown(action.runtime_id()) <= 0


func _project_smart_advance(from_cell: int, minimum: int, maximum: int) -> int:
	var distance := absi(from_cell - player_position)
	if distance <= 1:
		return from_cell
	var desired_distance := 3 if distance >= 4 else 1
	var requested := clampi(distance - desired_distance, maxi(1, minimum), maxi(1, maximum))
	requested = mini(requested, distance - 1)
	var direction := 1 if player_position > from_cell else -1
	return _clamp_actor_cell(from_cell + direction * requested)


func _cell_behind_player(from_cell: int) -> int:
	var target := player_position + (1 if from_cell < player_position else -1)
	if target < 1 or target > _cell_count() or target == player_position:
		target = player_position - (1 if from_cell < player_position else -1)
	return _clamp_actor_cell(target)


func _tick_existing_cooldowns() -> void:
	var keys := cooldowns.keys()
	for key: Variant in keys:
		var remaining := maxi(0, int(cooldowns[key]) - 1)
		if remaining <= 0:
			cooldowns.erase(key)
		else:
			cooldowns[key] = remaining


func _handle_deaths() -> void:
	if phase == Phase.HANDS:
		if int(false_hand.get("hp", 0)) <= 0:
			false_hand["active"] = false
			# Avoid an unwinnable state if False is destroyed before its break beam.
			true_death_loop_active = false
		if int(true_hand.get("hp", 0)) <= 0:
			true_hand["active"] = false
		if not bool(true_hand.get("active", false)) and not bool(false_hand.get("active", false)):
			_enter_phase_two()
	elif phase == Phase.BODY and int(body.get("hp", 0)) <= 0:
		body["active"] = false
		phase = Phase.DEFEATED
		current_intent = _wait_intent("Core-00 defeated")
		emit_signal("phase_changed", phase)
		if not _defeat_emitted:
			_defeat_emitted = true
			emit_signal("boss_defeated")


func _enter_phase_two() -> void:
	phase = Phase.BODY
	phase_two_turn = 1
	body = _make_actor(profile.body_max_health, profile.body_start_cell)
	body["active"] = true
	if int(body["position"]) == player_position:
		body["position"] = _nearest_free_cell(player_position)
	cooldowns = {}
	damage_received_this_player_turn = 0
	pending_card_jam_turns = 0
	true_death_loop_active = false
	false_heal_pending = false
	_set_player_logic_state(LOGIC_NONE)
	current_intent = {}
	locked_intent = {}
	emit_signal("phase_changed", phase)
	emit_signal("body_health_changed", int(body["hp"]), int(body["max_hp"]), int(body["shield"]))
	refresh_intent_preview()


func _actor_for(target_id: StringName) -> Dictionary:
	match target_id:
		HAND_TRUE:
			return true_hand
		HAND_FALSE:
			return false_hand
		&"body":
			return body
		_:
			return {}


func _hand_is_alive(hand_id: StringName) -> bool:
	var actor := _actor_for(hand_id)
	return not actor.is_empty() and bool(actor.get("active", false)) and int(actor.get("hp", 0)) > 0


func _add_hand_shield(hand_id: StringName, amount: int) -> void:
	var actor := _actor_for(hand_id)
	if actor.is_empty() or not bool(actor.get("active", false)):
		return
	actor["shield"] = mini(profile.phase_one_shield_cap, int(actor.get("shield", 0)) + maxi(0, amount))
	emit_signal("hand_health_changed", hand_id, int(actor["hp"]), int(actor["max_hp"]), int(actor["shield"]))


func _heal_actor(actor: Dictionary, amount: int) -> void:
	actor["hp"] = mini(int(actor.get("max_hp", 1)), int(actor.get("hp", 0)) + maxi(0, amount))
	if actor == false_hand:
		emit_signal("hand_health_changed", HAND_FALSE, int(actor["hp"]), int(actor["max_hp"]), int(actor["shield"]))


func _damage_player(amount: int, source_id: StringName) -> void:
	if amount <= 0:
		return
	player_hp = maxi(0, player_hp - amount)
	emit_signal("player_damage_requested", amount, source_id)


func _set_player_logic_state(next_state: StringName) -> void:
	player_logic_state = next_state
	emit_signal("player_logic_state_changed", player_logic_state)


func _make_actor(max_health: int, start_cell: int) -> Dictionary:
	return {
		"hp": maxi(1, max_health),
		"max_hp": maxi(1, max_health),
		"shield": 0,
		"position": _clamp_cell(start_cell),
		"active": true,
	}


func _wait_intent(reason: String) -> Dictionary:
	return {
		"phase": phase,
		"id": &"wait",
		"display_name": "等待",
		"intent_text": reason,
		"reason": reason,
	}


func _ensure_profile() -> void:
	if profile == null:
		profile = Core00Profile.new()


func _cell_count() -> int:
	return maxi(4, profile.cell_count if profile != null else 12)


func _clamp_cell(value: int) -> int:
	return clampi(value, 1, _cell_count())


func _clamp_actor_cell(value: int) -> int:
	var clamped := _clamp_cell(value)
	if clamped == player_position:
		clamped = clampi(clamped + (1 if clamped < _cell_count() else -1), 1, _cell_count())
	return clamped


func _nearest_free_cell(blocked_cell: int) -> int:
	for distance in range(1, _cell_count()):
		var right := blocked_cell + distance
		if right <= _cell_count():
			return right
		var left := blocked_cell - distance
		if left >= 1:
			return left
	return 1


func _ensure_distinct_positions() -> void:
	if phase == Phase.BODY and int(body.get("position", -1)) == player_position:
		body["position"] = _nearest_free_cell(player_position)
	elif phase == Phase.HANDS:
		player_position = clampi(player_position, 2, maxi(2, _cell_count() - 1))


func _has_property(source: Object, property_name: StringName) -> bool:
	for entry: Dictionary in source.get_property_list():
		if StringName(entry.get("name", &"")) == property_name:
			return true
	return false


func _emit_all_health() -> void:
	if not true_hand.is_empty():
		emit_signal("hand_health_changed", HAND_TRUE, int(true_hand["hp"]), int(true_hand["max_hp"]), int(true_hand["shield"]))
	if not false_hand.is_empty():
		emit_signal("hand_health_changed", HAND_FALSE, int(false_hand["hp"]), int(false_hand["max_hp"]), int(false_hand["shield"]))
	if not body.is_empty():
		emit_signal("body_health_changed", int(body["hp"]), int(body["max_hp"]), int(body["shield"]))


func _update_debug_panel() -> void:
	var panel := find_child("DebugPanel", true, false) as CanvasItem
	if panel != null:
		panel.visible = show_debug_panel
	_set_debug_label("PhaseLabel", "Phase %d  Round %d" % [phase, round_number])
	_set_debug_label("HandsLabel", "TRUE %d/%d +%d  |  FALSE %d/%d +%d" % [
		int(true_hand.get("hp", 0)), int(true_hand.get("max_hp", 0)), int(true_hand.get("shield", 0)),
		int(false_hand.get("hp", 0)), int(false_hand.get("max_hp", 0)), int(false_hand.get("shield", 0)),
	])
	_set_debug_label("BodyLabel", "BODY %d/%d +%d  Pos %d  Distance %d" % [
		int(body.get("hp", 0)), int(body.get("max_hp", 0)), int(body.get("shield", 0)),
		int(body.get("position", 0)), get_body_distance(),
	])
	var preview := locked_intent if not locked_intent.is_empty() else current_intent
	_set_debug_label("IntentLabel", "Intent: %s" % String(preview.get("intent_text", "-")))
	_set_debug_label("PlayerLabel", "Player HP %d  Pos %d  Logic %s" % [player_hp, player_position, String(player_logic_state)])
	_set_debug_label("CooldownLabel", "Cooldowns %s  Jam pending %d" % [str(cooldowns), pending_card_jam_turns])


func _set_debug_label(node_name: String, value: String) -> void:
	var label := find_child(node_name, true, false)
	if label != null and _has_property(label, &"text"):
		label.set("text", value)
