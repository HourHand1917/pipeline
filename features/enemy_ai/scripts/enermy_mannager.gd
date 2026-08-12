extends Node
class_name EnermyMannager

## Drop-in, data-driven enemy controller.  The misspelled class name is kept on
## purpose because it is the public name requested by the project.
##
## It has no compile-time dependency on the C# battle classes.  When placed as a
## child of the existing battle root it discovers and talks to them dynamically;
## when used alone, the same public API provides a deterministic test/debug mode.

signal health_changed(current: int, maximum: int)
signal shield_changed(current: int)
signal position_changed(enemy_position: int, player_position: int)
signal intent_changed(intent: Dictionary)
signal intent_locked(intent: Dictionary)
signal intent_executed(intent: Dictionary)
signal enemy_died()
signal dust_requested(board_cell: Vector2i)

const PLAYER_TURN_PHASE := 1
const ENEMY_TURN_PHASE := 2
const BATTLE_END_PHASE := 3
const INVALID_CELL := Vector2i(-1, -1)
const MAX_BIND_ATTEMPTS := 180

@export_group("Configuration")
@export var profile: EnemyAiProfile
@export var auto_bind: bool = true
@export var show_debug_panel: bool = true
@export var dust_block_buff: Resource
@export_range(-1, 2147483647, 1) var random_seed_override: int = -1

@export_group("Standalone Inspector Debug")
@export_range(1, 99, 1, "or_greater") var debug_enemy_position: int = 6
@export_range(1, 99, 1, "or_greater") var debug_player_position: int = 2
@export_range(1, 9999, 1, "or_greater") var debug_player_hp: int = 20
@export var debug_auto_refresh: bool = true

var current_hp: int = 1
var max_hp: int = 1
var current_shield: int = 0
var enemy_position: int = 6
var player_position: int = 2
var player_hp: int = 20
var damage_received_this_player_turn: int = 0
var stunned_turns: int = 0
var prepared_dash: bool = false
var cooldowns: Dictionary = {}
var current_intent: Dictionary = {}
var locked_intent: Dictionary = {}
var round_number: int = 1

var _bound_enemy: Object
var _player: Object
var _enemy_manager: Object
var _battle_manager: Object
var _board_manager: Object
var _manual_mode: bool = false
var _bind_attempts: int = 0
var _death_emitted: bool = false
var _intent_cache: Dictionary = {}
var _last_total_durability: int = 0
var _executing_intent: bool = false


func _ready() -> void:
	_initialize_from_profile()
	_update_debug_panel()
	if auto_bind:
		call_deferred("_attempt_auto_bind")
	elif debug_auto_refresh:
		refresh_intent_preview()


# -----------------------------------------------------------------------------
# Public setup / debug API
# -----------------------------------------------------------------------------

func configure_manual_state(
	profile_override: EnemyAiProfile,
	enemy_cell: int,
	player_cell: int,
	new_player_hp: int = 20,
	enemy_hp: int = -1,
	shield: int = -1
) -> Dictionary:
	_manual_mode = true
	auto_bind = false
	profile = profile_override
	_initialize_from_profile()
	enemy_position = _clamp_cell(enemy_cell)
	player_position = _clamp_cell(player_cell)
	player_hp = maxi(0, new_player_hp)
	if enemy_hp >= 0:
		current_hp = clampi(enemy_hp, 0, max_hp)
	if shield >= 0:
		current_shield = maxi(0, shield)
	_last_total_durability = current_hp + current_shield
	_invalidate_intent_cache()
	refresh_intent_preview()
	return get_state()


func set_debug_snapshot(snapshot: Dictionary) -> Dictionary:
	_manual_mode = true
	auto_bind = false
	_apply_snapshot(snapshot)
	_invalidate_intent_cache()
	refresh_intent_preview()
	return get_state()


func notify_player_action(snapshot: Dictionary = {}) -> Dictionary:
	## Call after every player action.  Repeated calls with an unchanged state do
	## not reroll randomness: the state fingerprint returns the cached intent.
	if not snapshot.is_empty():
		_apply_snapshot(snapshot)
	_sync_bound_state()
	return refresh_intent_preview()


func get_state() -> Dictionary:
	return {
		"enemy_id": profile.enemy_id if profile != null else &"",
		"brain_type": profile.brain_type if profile != null else -1,
		"enemy_hp": current_hp,
		"max_hp": max_hp,
		"shield": current_shield,
		"enemy_position": enemy_position,
		"player_position": player_position,
		"player_hp": player_hp,
		"cell_count": _cell_count(),
		"distance": get_distance(),
		"damage_received_this_player_turn": damage_received_this_player_turn,
		"cooldowns": cooldowns.duplicate(true),
		"stunned_turns": stunned_turns,
		"prepared_dash": prepared_dash,
		"round_number": round_number,
		"current_intent": current_intent.duplicate(true),
		"locked_intent": locked_intent.duplicate(true),
		"is_bound": is_instance_valid(_bound_enemy),
		"manual_mode": _manual_mode,
	}


func get_distance() -> int:
	## Distance is the absolute difference between track indexes.  Adjacent
	## characters therefore have distance 1.
	return absi(enemy_position - player_position)


func get_cooldown(action_id: StringName) -> int:
	return maxi(0, int(cooldowns.get(action_id, cooldowns.get(String(action_id), 0))))


func take_damage(amount: int) -> int:
	if amount <= 0 or current_hp <= 0:
		return 0
	var old_total := current_hp + current_shield
	if is_instance_valid(_bound_enemy) and _call_first(_bound_enemy, [&"TakeDamage", &"take_damage"], [amount])[0]:
		_sync_bound_state()
		return maxi(0, old_total - current_hp - current_shield)

	var shield_damage := mini(current_shield, amount)
	current_shield -= shield_damage
	var hp_damage := mini(current_hp, amount - shield_damage)
	current_hp -= hp_damage
	var received := shield_damage + hp_damage
	damage_received_this_player_turn += received
	_last_total_durability = current_hp + current_shield
	emit_signal("shield_changed", current_shield)
	emit_signal("health_changed", current_hp, max_hp)
	_handle_possible_death()
	_state_changed()
	return received


func add_shield(amount: int) -> int:
	if amount <= 0:
		return current_shield
	if is_instance_valid(_bound_enemy) and _call_first(_bound_enemy, [&"AddShield", &"add_shield"], [amount])[0]:
		_sync_bound_state()
		return current_shield
	current_shield += amount
	_last_total_durability = current_hp + current_shield
	emit_signal("shield_changed", current_shield)
	_state_changed()
	return current_shield


func heal(amount: int) -> int:
	if amount <= 0 or current_hp <= 0:
		return current_hp
	if is_instance_valid(_bound_enemy) and _call_first(_bound_enemy, [&"Heal", &"heal"], [amount])[0]:
		_sync_bound_state()
		return current_hp
	current_hp = mini(max_hp, current_hp + amount)
	_last_total_durability = current_hp + current_shield
	emit_signal("health_changed", current_hp, max_hp)
	_state_changed()
	return current_hp


func bind_enemy(enemy: Object) -> void:
	if not is_instance_valid(enemy):
		return
	_bound_enemy = enemy
	_connect_first_signal(enemy, [&"health_changed", &"HealthChanged"], _on_bound_health_changed)
	_connect_first_signal(enemy, [&"shield_changed", &"ShieldChanged"], _on_bound_shield_changed)
	_connect_first_signal(enemy, [&"position_changed", &"PositionChanged"], _on_bound_enemy_position_changed)
	_connect_first_signal(enemy, [&"died", &"Died"], _on_bound_enemy_died)
	_sync_bound_state()
	_invalidate_intent_cache()
	refresh_intent_preview()


# -----------------------------------------------------------------------------
# Intent lifecycle
# -----------------------------------------------------------------------------

func refresh_intent_preview() -> Dictionary:
	_sync_bound_state()
	if profile == null:
		current_intent = _wait_intent(&"missing_profile", "No AI profile", "missing_profile")
	else:
		var fingerprint := _state_fingerprint()
		if _intent_cache.has(fingerprint):
			current_intent = (_intent_cache[fingerprint] as Dictionary).duplicate(true)
		else:
			current_intent = _evaluate_intent(fingerprint)
			_intent_cache[fingerprint] = current_intent.duplicate(true)
	emit_signal("intent_changed", current_intent.duplicate(true))
	_update_debug_panel()
	return current_intent.duplicate(true)


func lock_intent() -> Dictionary:
	if current_intent.is_empty():
		refresh_intent_preview()
	locked_intent = current_intent.duplicate(true)
	emit_signal("intent_locked", locked_intent.duplicate(true))
	_update_debug_panel()
	return locked_intent.duplicate(true)


func execute_locked_intent() -> Dictionary:
	if _executing_intent:
		return {}
	if locked_intent.is_empty():
		lock_intent()
	_executing_intent = true
	var result := locked_intent.duplicate(true)
	var action: EnemyAiAction = result.get("action") as EnemyAiAction
	var intent_id := StringName(result.get("id", &"wait"))

	if intent_id == &"stunned":
		stunned_turns = maxi(0, stunned_turns - 1)
	else:
		var enemy_to := int(result.get("enemy_to", enemy_position))
		var player_to := int(result.get("player_to", player_position))
		if action != null and action.action_type == EnemyAiAction.ActionType.DASH:
			_set_player_position(player_to)
			_set_enemy_position(enemy_to)
			prepared_dash = false
		elif enemy_to != enemy_position:
			_set_enemy_position(enemy_to)

		if action != null:
			if action.damage > 0:
				_damage_player(action.damage)
			if action.shield > 0:
				add_shield(action.shield)
			if action.sets_prepared_dash:
				prepared_dash = true
			if action.stun_self_turns > 0:
				stunned_turns = maxi(stunned_turns, action.stun_self_turns)
			if action.applies_dust:
				_apply_dust(result.get("dust_cell", INVALID_CELL) as Vector2i)

	_tick_existing_cooldowns()
	if action != null and action.cooldown_turns > 0:
		cooldowns[action.runtime_id()] = action.cooldown_turns

	result["executed"] = true
	result["state_after"] = get_state()
	locked_intent = {}
	current_intent = {}
	_invalidate_intent_cache()
	_executing_intent = false
	emit_signal("intent_executed", result.duplicate(true))
	_update_debug_panel()
	return result


func advance_enemy_turn_for_test() -> Dictionary:
	## Headless test convenience: lock the current final state, execute it, and
	## advance the local round.  WAIT and stunned turns also tick cooldowns.
	if current_intent.is_empty():
		refresh_intent_preview()
	lock_intent()
	var result := execute_locked_intent()
	round_number += 1
	damage_received_this_player_turn = 0
	refresh_intent_preview()
	return result


# -----------------------------------------------------------------------------
# Decision rules
# -----------------------------------------------------------------------------

func _evaluate_intent(fingerprint: String) -> Dictionary:
	if stunned_turns > 0:
		return _wait_intent(&"stunned", "Stunned", "dash_recovery")

	var legal: Array[EnemyAiAction] = []
	for action: EnemyAiAction in profile.actions:
		if action != null and _is_action_legal(action):
			legal.append(action)

	match profile.brain_type:
		EnemyAiProfile.BrainType.ROCKY:
			return _evaluate_rocky(legal, fingerprint)
		EnemyAiProfile.BrainType.SHARKK:
			return _evaluate_sharkk(legal, fingerprint)
		_:
			return _evaluate_boom(legal, fingerprint)


func _evaluate_boom(legal: Array[EnemyAiAction], fingerprint: String) -> Dictionary:
	## Boom deliberately has no memory or defense: it simply rolls among the
	## highest-priority legal approach/attack actions configured in its profile.
	var chosen := _choose_by_priority(legal, fingerprint + ":boom")
	return _intent_from_action(chosen) if chosen != null else _wait_intent(&"boom_wait", "Wait", "no_legal_action")


func _evaluate_rocky(legal: Array[EnemyAiAction], fingerprint: String) -> Dictionary:
	var attacks := _actions_of_types(legal, [EnemyAiAction.ActionType.ATTACK])
	var defensive := _actions_of_types(legal, [
		EnemyAiAction.ActionType.DEFEND_RETREAT,
		EnemyAiAction.ActionType.RETREAT,
	])
	var advances := _actions_of_types(legal, [EnemyAiAction.ActionType.ADVANCE])

	# At distance 1 Rocky teaches the player that the heavy close hit matters.
	# When it is cooling down the weaker mid attack remains available.
	if not attacks.is_empty():
		var best_damage := 0
		for action: EnemyAiAction in attacks:
			best_damage = maxi(best_damage, action.damage)
		var strongest: Array[EnemyAiAction] = []
		for action: EnemyAiAction in attacks:
			if action.damage == best_damage:
				strongest.append(action)

		var hp_ratio := float(current_hp) / float(maxi(1, max_hp))
		var reacting := damage_received_this_player_turn >= profile.reactive_damage_threshold \
			or hp_ratio <= profile.defensive_hp_ratio
		if reacting and not defensive.is_empty():
			var reaction := _choose_by_priority(defensive, fingerprint + ":rocky_react")
			return _intent_from_action(reaction, true, "react_to_damage_or_low_hp")
		return _intent_from_action(_choose_by_priority(strongest, fingerprint + ":rocky_attack"))

	# Outside attack range, choose the advance that creates the best next-turn
	# damage; distance is the tie-breaker so moving 2 is useful but not automatic.
	if not advances.is_empty():
		var best_score := -1000000
		var best_moves: Array[EnemyAiAction] = []
		for action: EnemyAiAction in advances:
			var projected := _project_move(enemy_position, action.move_toward, true)
			var next_damage := _best_configured_attack_damage(absi(projected - player_position))
			var score := next_damage * 100 - absi(projected - player_position)
			if score > best_score:
				best_score = score
				best_moves = [action]
			elif score == best_score:
				best_moves.append(action)
		return _intent_from_action(_weighted_choose(best_moves, fingerprint + ":rocky_move"))

	var fallback := _choose_by_priority(legal, fingerprint + ":rocky_fallback")
	return _intent_from_action(fallback) if fallback != null else _wait_intent(&"rocky_wait", "Wait", "no_legal_action")


func _evaluate_sharkk(legal: Array[EnemyAiAction], fingerprint: String) -> Dictionary:
	var dust_actions := _actions_of_types(legal, [EnemyAiAction.ActionType.DUST_RETREAT])
	if damage_received_this_player_turn >= profile.reactive_damage_threshold and not dust_actions.is_empty():
		return _intent_from_action(
			_choose_by_priority(dust_actions, fingerprint + ":sharkk_forced_dust"),
			true,
			"received_%d_damage" % damage_received_this_player_turn
		)

	if prepared_dash:
		var dashes := _actions_of_types(legal, [EnemyAiAction.ActionType.DASH])
		if not dashes.is_empty():
			return _intent_from_action(_choose_by_priority(dashes, fingerprint + ":sharkk_dash"), true, "dash_prepared")

	# Attacks are strongly preferred, but a normal mid/close-range dust retreat
	# remains in the same deterministic weighted pool.  Profile weights (for
	# example attack 4 : dust 1) control the exact frequency without rerolling an
	# unchanged state.
	var attacks := _actions_of_types(legal, [EnemyAiAction.ActionType.ATTACK])
	if not attacks.is_empty():
		var attack_pool: Array[EnemyAiAction] = attacks.duplicate()
		attack_pool.append_array(dust_actions)
		return _intent_from_action(_weighted_choose(attack_pool, fingerprint + ":sharkk_attack_or_dust"))

	var chosen := _choose_by_priority(legal, fingerprint + ":sharkk_move")
	return _intent_from_action(chosen) if chosen != null else _wait_intent(&"sharkk_wait", "Wait", "no_legal_action")


func _is_action_legal(action: EnemyAiAction) -> bool:
	if get_cooldown(action.runtime_id()) > 0:
		return false
	if not action.is_in_range(get_distance()):
		return false
	if action.requires_damage_taken_at_least > damage_received_this_player_turn:
		return false
	if action.requires_prepared_dash and not prepared_dash:
		return false

	if action.action_type == EnemyAiAction.ActionType.DASH:
		return bool(_dash_projection(action).get("valid", false))
	if action.move_toward > 0:
		return _project_move(enemy_position, action.move_toward, true) != enemy_position
	if action.move_away > 0:
		var projected := _project_move(enemy_position, action.move_away, false)
		if projected == enemy_position:
			# Taking 10+ damage makes Sharkk's dust intent mandatory.  At a track
			# boundary the retreat portion is safely truncated to zero, but the
			# debuff still executes instead of silently losing the reaction.
			return action.action_type == EnemyAiAction.ActionType.DUST_RETREAT \
				and profile != null \
				and damage_received_this_player_turn >= profile.reactive_damage_threshold
		if action.action_type == EnemyAiAction.ActionType.PREPARE_DASH:
			return _can_dash_from(projected, action.push_player)
	return true


func _intent_from_action(action: EnemyAiAction, forced: bool = false, reason: String = "") -> Dictionary:
	if action == null:
		return _wait_intent(&"wait", "Wait", "no_action")
	var enemy_to := enemy_position
	var player_to := player_position
	if action.action_type == EnemyAiAction.ActionType.DASH:
		var projection := _dash_projection(action)
		enemy_to = int(projection.get("enemy_to", enemy_position))
		player_to = int(projection.get("player_to", player_position))
	elif action.move_toward > 0:
		enemy_to = _project_move(enemy_position, action.move_toward, true)
	elif action.move_away > 0:
		enemy_to = _project_move(enemy_position, action.move_away, false)

	var text := action.intent_text
	if text.is_empty():
		text = action.display_name
	var dust_cell := _choose_dust_cell(_state_fingerprint() + ":dust") if action.applies_dust else INVALID_CELL
	return {
		"id": action.runtime_id(),
		"display_name": action.display_name,
		"intent_text": text,
		"action_type": action.action_type,
		"action": action,
		"damage": action.damage,
		"shield": action.shield,
		"distance": get_distance(),
		"distance_after": absi(enemy_to - player_to),
		"enemy_from": enemy_position,
		"enemy_to": enemy_to,
		"player_from": player_position,
		"player_to": player_to,
		"cooldown_turns": action.cooldown_turns,
		"dust_cell": dust_cell,
		"is_forced": forced,
		"reason": reason,
	}


func _wait_intent(intent_id: StringName, label: String, reason: String) -> Dictionary:
	return {
		"id": intent_id,
		"display_name": label,
		"intent_text": label,
		"action_type": EnemyAiAction.ActionType.WAIT,
		"action": null,
		"damage": 0,
		"shield": 0,
		"distance": get_distance(),
		"distance_after": get_distance(),
		"enemy_from": enemy_position,
		"enemy_to": enemy_position,
		"player_from": player_position,
		"player_to": player_position,
		"cooldown_turns": 0,
		"dust_cell": INVALID_CELL,
		"is_forced": reason == "dash_recovery",
		"reason": reason,
	}


# -----------------------------------------------------------------------------
# Selection helpers
# -----------------------------------------------------------------------------

func _actions_of_types(source: Array[EnemyAiAction], types: Array) -> Array[EnemyAiAction]:
	var result: Array[EnemyAiAction] = []
	for action: EnemyAiAction in source:
		if action.action_type in types:
			result.append(action)
	return result


func _choose_by_priority(actions: Array[EnemyAiAction], salt: String) -> EnemyAiAction:
	if actions.is_empty():
		return null
	var highest := -2147483648
	for action: EnemyAiAction in actions:
		highest = maxi(highest, action.base_priority)
	var top: Array[EnemyAiAction] = []
	for action: EnemyAiAction in actions:
		if action.base_priority == highest:
			top.append(action)
	return _weighted_choose(top, salt)


func _weighted_choose(actions: Array[EnemyAiAction], salt: String) -> EnemyAiAction:
	if actions.is_empty():
		return null
	if actions.size() == 1:
		return actions[0]
	var stable := actions.duplicate()
	stable.sort_custom(func(a: EnemyAiAction, b: EnemyAiAction) -> bool: return String(a.runtime_id()) < String(b.runtime_id()))
	var total := 0.0
	for action: EnemyAiAction in stable:
		total += maxf(0.0, action.random_weight)
	if total <= 0.0:
		return stable[0]
	var rng := RandomNumberGenerator.new()
	rng.seed = _seed_for(salt)
	var roll := rng.randf_range(0.0, total)
	var cursor := 0.0
	for action: EnemyAiAction in stable:
		cursor += maxf(0.0, action.random_weight)
		if roll <= cursor:
			return action
	return stable.back()


func _best_configured_attack_damage(distance: int) -> int:
	var result := 0
	for action: EnemyAiAction in profile.actions:
		if action != null \
			and action.action_type in [EnemyAiAction.ActionType.ATTACK, EnemyAiAction.ActionType.DASH] \
			and action.is_in_range(distance) \
			and get_cooldown(action.runtime_id()) <= 0:
			result = maxi(result, action.damage)
	return result


func _state_fingerprint() -> String:
	var cooldown_parts: PackedStringArray = []
	var keys := cooldowns.keys()
	keys.sort_custom(func(a: Variant, b: Variant) -> bool: return String(a) < String(b))
	for key: Variant in keys:
		cooldown_parts.append("%s=%d" % [String(key), int(cooldowns[key])])
	return "%s|r%d|e%d|p%d|eh%d|es%d|ph%d|d%d|st%d|pd%s|%s|b%s" % [
		String(profile.enemy_id) if profile != null else "none",
		round_number,
		enemy_position,
		player_position,
		current_hp,
		current_shield,
		player_hp,
		damage_received_this_player_turn,
		stunned_turns,
		str(prepared_dash),
		",".join(cooldown_parts),
		_board_state_signature(),
	]


func _seed_for(salt: String) -> int:
	var base_seed := random_seed_override if random_seed_override >= 0 else (profile.random_seed if profile != null else 0)
	return int(base_seed) ^ int(salt.hash())


func _board_state_signature() -> String:
	## Lighting a cell is a real player-state change for Sharkk because dust may
	## only target an unlit cell.  Including the light map in the fingerprint
	## prevents a cached preview from pointing at a cell the player lit later.
	if not is_instance_valid(_board_manager):
		return "none"
	var columns := int(_get_first_property(_board_manager, [&"columns", &"Columns"], 0))
	var rows := int(_get_first_property(_board_manager, [&"rows", &"Rows"], 0))
	if columns <= 0 or rows <= 0:
		return "empty"
	var bits := PackedStringArray()
	for y in range(rows):
		for x in range(columns):
			var result := _call_first(_board_manager, [&"GetCell", &"get_cell"], [Vector2i(x, y)])
			if result[0] and result[1] != null:
				bits.append("1" if bool(_get_first_property(result[1], [&"is_lit", &"IsLit"], false)) else "0")
			else:
				bits.append("x")
	return "".join(bits)


# -----------------------------------------------------------------------------
# Movement and effects
# -----------------------------------------------------------------------------

func _project_move(origin: int, steps: int, toward_player: bool) -> int:
	var result := _clamp_cell(origin)
	var direction := signi(player_position - result)
	if direction == 0:
		return result
	if not toward_player:
		direction *= -1
	var occupied := _occupied_enemy_cells()
	for _step in range(maxi(0, steps)):
		var next := result + direction
		if next < 1 or next > _cell_count():
			break
		if next == player_position or occupied.has(next):
			break
		result = next
	return result


func _dash_projection(action: EnemyAiAction) -> Dictionary:
	if not _can_dash_from(enemy_position, action.push_player):
		return {"valid": false, "enemy_to": enemy_position, "player_to": player_position}
	var direction := signi(player_position - enemy_position)
	var push_steps := maxi(1, action.push_player)
	return {
		"valid": true,
		"enemy_to": player_position,
		"player_to": player_position + direction * push_steps,
	}


func _can_dash_from(origin: int, push_steps: int) -> bool:
	var direction := signi(player_position - origin)
	if direction == 0:
		return false
	var distance := absi(player_position - origin)
	if distance < 1 or distance > 4:
		return false
	var occupied := _occupied_enemy_cells()
	# Dash may cross empty cells but never another enemy.
	for step in range(1, distance):
		if occupied.has(origin + direction * step):
			return false
	var final_push := player_position + direction * maxi(1, push_steps)
	if final_push < 1 or final_push > _cell_count():
		return false
	if occupied.has(final_push):
		return false
	return true


func _set_enemy_position(value: int) -> void:
	var legal := _clamp_cell(value)
	if legal == player_position:
		return
	if is_instance_valid(_bound_enemy):
		_call_first(_bound_enemy, [&"SetMapPosition", &"set_map_position"], [legal])
		_call_first(_bound_enemy, [&"UpdateFacing", &"update_facing"], [player_position])
	enemy_position = legal
	emit_signal("position_changed", enemy_position, player_position)
	_state_changed()


func _set_player_position(value: int) -> void:
	var legal := _clamp_cell(value)
	if legal == enemy_position:
		return
	if is_instance_valid(_player):
		_call_first(_player, [&"SetMapPosition", &"set_map_position"], [legal])
	player_position = legal
	emit_signal("position_changed", enemy_position, player_position)
	_state_changed()


func _damage_player(amount: int) -> void:
	if amount <= 0:
		return
	if is_instance_valid(_player) and _call_first(_player, [&"TakeDamage", &"take_damage"], [amount])[0]:
		_sync_player_state()
	else:
		player_hp = maxi(0, player_hp - amount)


func _apply_dust(cell: Vector2i) -> void:
	var selected := cell
	if selected == INVALID_CELL:
		selected = _choose_dust_cell(_state_fingerprint() + ":execute_dust")
	if selected != INVALID_CELL and is_instance_valid(_board_manager) and dust_block_buff != null:
		var call_result := _call_first(_board_manager, [&"GetCell", &"get_cell"], [selected])
		if call_result[0] and call_result[1] != null:
			var stats: Variant = _get_first_property(call_result[1], [&"stats", &"Stats"], null)
			if stats != null:
				_call_first(stats, [&"add_buff", &"AddBuff"], [dust_block_buff, 1])
	emit_signal("dust_requested", selected)


func _choose_dust_cell(salt: String) -> Vector2i:
	if not is_instance_valid(_board_manager):
		return INVALID_CELL
	var columns := int(_get_first_property(_board_manager, [&"columns", &"Columns"], 0))
	var rows := int(_get_first_property(_board_manager, [&"rows", &"Rows"], 0))
	if columns <= 0 or rows <= 0:
		return INVALID_CELL
	var candidates: Array[Vector2i] = []
	for y in range(rows):
		for x in range(columns):
			var position := Vector2i(x, y)
			var call_result := _call_first(_board_manager, [&"GetCell", &"get_cell"], [position])
			if not call_result[0] or call_result[1] == null:
				continue
			var is_lit := bool(_get_first_property(call_result[1], [&"is_lit", &"IsLit"], false))
			var card_instance_id := int(_get_first_property(
				call_result[1],
				[&"card_instance_id", &"CardInstanceId"],
				-1
			))
			# The design says one unlit equipment cell. Empty board slots are not
			# actionable and must never absorb Sharkk's dust debuff.
			if not is_lit and card_instance_id >= 0:
				candidates.append(position)
	if candidates.is_empty():
		return INVALID_CELL
	var rng := RandomNumberGenerator.new()
	rng.seed = _seed_for(salt)
	return candidates[rng.randi_range(0, candidates.size() - 1)]


func _tick_existing_cooldowns() -> void:
	var keys := cooldowns.keys()
	for key: Variant in keys:
		var next := maxi(0, int(cooldowns[key]) - 1)
		if next <= 0:
			cooldowns.erase(key)
		else:
			cooldowns[key] = next


# -----------------------------------------------------------------------------
# Existing battle auto-binding (dynamic, no C# type reference)
# -----------------------------------------------------------------------------

func _attempt_auto_bind() -> void:
	if not auto_bind or _manual_mode or not is_inside_tree():
		return
	var root := _find_battle_root()
	if root == null:
		_schedule_bind_retry()
		return

	_battle_manager = root.get_node_or_null("battlemanager")
	if not is_instance_valid(_battle_manager):
		_battle_manager = root.get_node_or_null("BattleManager")
	_board_manager = root.get_node_or_null("boardmanager")
	if not is_instance_valid(_board_manager):
		_board_manager = root.get_node_or_null("BoardManager")
	_player = root.get_node_or_null("PlayerBattle")
	_enemy_manager = root.get_node_or_null("EnemyManager")

	if is_instance_valid(_battle_manager):
		_connect_first_signal(_battle_manager, [&"phase_changed", &"PhaseChanged"], _on_phase_changed)
	if is_instance_valid(_board_manager):
		_connect_first_signal(_board_manager, [&"board_changed", &"BoardChanged"], _on_observed_player_action)
	if is_instance_valid(_player):
		_connect_first_signal(_player, [&"position_changed", &"PositionChanged"], _on_player_position_changed)
		_connect_first_signal(_player, [&"health_changed", &"HealthChanged"], _on_player_health_changed)
		_sync_player_state()
	if is_instance_valid(_enemy_manager):
		_connect_first_signal(_enemy_manager, [&"enemy_spawned", &"EnemySpawned"], _on_enemy_spawned)
		# One battle may contain Boom + Rocky.  Each manager binds the enemy whose
		# EnemyId matches its profile instead of all managers grabbing the primary.
		var enemies: Variant = _get_first_property(_enemy_manager, [&"Enemies", &"enemies"], [])
		if enemies is Array:
			for candidate: Variant in enemies:
				if candidate != null and _enemy_matches_profile(candidate):
					bind_enemy(candidate)
					break
		if not is_instance_valid(_bound_enemy) and profile == null:
			var primary_result := _call_first(_enemy_manager, [&"GetPrimaryEnemy", &"get_primary_enemy"], [])
			if primary_result[0] and primary_result[1] != null:
				bind_enemy(primary_result[1])

	if not is_instance_valid(_player) or not is_instance_valid(_enemy_manager):
		_schedule_bind_retry()
	else:
		refresh_intent_preview()


func _find_battle_root() -> Node:
	var cursor: Node = get_parent()
	while cursor != null:
		if cursor.has_node("battlemanager") or cursor.has_node("BattleManager"):
			return cursor
		cursor = cursor.get_parent()
	return null


func _schedule_bind_retry() -> void:
	_bind_attempts += 1
	if _bind_attempts < MAX_BIND_ATTEMPTS:
		call_deferred("_attempt_auto_bind")


func _on_enemy_spawned(enemy: Object) -> void:
	if not is_instance_valid(_bound_enemy) and _enemy_matches_profile(enemy):
		bind_enemy(enemy)


func _enemy_matches_profile(enemy: Object) -> bool:
	if not is_instance_valid(enemy):
		return false
	if profile == null or profile.enemy_id.is_empty():
		return true
	var runtime_id := StringName(_get_first_property(enemy, [&"EnemyId", &"enemy_id"], &""))
	return runtime_id == profile.enemy_id


func _on_phase_changed(new_phase: int) -> void:
	if new_phase == PLAYER_TURN_PHASE:
		round_number = int(_get_first_property(_battle_manager, [&"RoundNumber", &"round_number"], round_number))
		damage_received_this_player_turn = 0
		_invalidate_intent_cache()
		refresh_intent_preview()
	elif new_phase == ENEMY_TURN_PHASE:
		# The existing BattleManager emits PhaseChanged before its delayed enemy
		# resolution, so the manager can lock the player's final state now.
		lock_intent()
		call_deferred("execute_locked_intent")
	elif new_phase == BATTLE_END_PHASE:
		locked_intent = {}


func _on_observed_player_action() -> void:
	if _is_player_turn():
		notify_player_action()


func _on_player_position_changed(value: int) -> void:
	player_position = _clamp_cell(value)
	emit_signal("position_changed", enemy_position, player_position)
	_state_changed()


func _on_player_health_changed(value: int, _maximum: int) -> void:
	player_hp = maxi(0, value)
	_state_changed()


func _on_bound_enemy_position_changed(value: int) -> void:
	enemy_position = _clamp_cell(value)
	emit_signal("position_changed", enemy_position, player_position)
	_state_changed()


func _on_bound_shield_changed(value: int) -> void:
	var decrease := maxi(0, current_shield - value)
	current_shield = maxi(0, value)
	damage_received_this_player_turn += decrease
	_last_total_durability = current_hp + current_shield
	emit_signal("shield_changed", current_shield)
	_state_changed()


func _on_bound_health_changed(value: int, maximum: int) -> void:
	var decrease := maxi(0, current_hp - value)
	current_hp = maxi(0, value)
	max_hp = maxi(1, maximum)
	damage_received_this_player_turn += decrease
	_last_total_durability = current_hp + current_shield
	emit_signal("health_changed", current_hp, max_hp)
	_handle_possible_death()
	_state_changed()


func _on_bound_enemy_died() -> void:
	current_hp = 0
	_handle_possible_death()


func _sync_bound_state() -> void:
	if not is_instance_valid(_bound_enemy):
		return
	current_hp = int(_get_first_property(_bound_enemy, [&"CurrentHp", &"current_hp"], current_hp))
	max_hp = maxi(1, int(_get_first_property(_bound_enemy, [&"MaxHp", &"max_hp"], max_hp)))
	current_shield = maxi(0, int(_get_first_property(_bound_enemy, [&"Shield", &"shield"], current_shield)))
	enemy_position = _clamp_cell(int(_get_first_property(_bound_enemy, [&"MapPosition", &"map_position"], enemy_position)))
	_last_total_durability = current_hp + current_shield
	_sync_player_state()
	_handle_possible_death()


func _sync_player_state() -> void:
	if not is_instance_valid(_player):
		return
	player_hp = maxi(0, int(_get_first_property(_player, [&"CurrentHp", &"current_hp"], player_hp)))
	player_position = _clamp_cell(int(_get_first_property(_player, [&"MapPosition", &"map_position"], player_position)))


func _occupied_enemy_cells() -> Array[int]:
	var result: Array[int] = []
	if not is_instance_valid(_enemy_manager):
		return result
	var enemies: Variant = _get_first_property(_enemy_manager, [&"Enemies", &"enemies"], [])
	if enemies is Array:
		for enemy: Variant in enemies:
			if enemy == null or enemy == _bound_enemy:
				continue
			var cell := int(_get_first_property(enemy, [&"MapPosition", &"map_position"], -1))
			if cell >= 1:
				result.append(cell)
	return result


# -----------------------------------------------------------------------------
# General utilities
# -----------------------------------------------------------------------------

func _initialize_from_profile() -> void:
	if profile != null:
		max_hp = maxi(1, profile.max_hp)
		current_hp = max_hp
		current_shield = maxi(0, profile.initial_shield)
		enemy_position = profile.clamped_start_cell()
	else:
		max_hp = 1
		current_hp = 1
		current_shield = 0
		enemy_position = maxi(1, debug_enemy_position)
	player_position = _clamp_cell(debug_player_position)
	player_hp = maxi(1, debug_player_hp)
	damage_received_this_player_turn = 0
	stunned_turns = 0
	prepared_dash = false
	cooldowns.clear()
	current_intent = {}
	locked_intent = {}
	_death_emitted = false
	_last_total_durability = current_hp + current_shield
	_invalidate_intent_cache()


func _apply_snapshot(snapshot: Dictionary) -> void:
	if snapshot.has("enemy_position"):
		enemy_position = _clamp_cell(int(snapshot["enemy_position"]))
	if snapshot.has("player_position"):
		player_position = _clamp_cell(int(snapshot["player_position"]))
	if snapshot.has("max_hp"):
		max_hp = maxi(1, int(snapshot["max_hp"]))
	if snapshot.has("enemy_hp"):
		current_hp = clampi(int(snapshot["enemy_hp"]), 0, max_hp)
	if snapshot.has("shield"):
		current_shield = maxi(0, int(snapshot["shield"]))
	if snapshot.has("player_hp"):
		player_hp = maxi(0, int(snapshot["player_hp"]))
	if snapshot.has("damage_received_this_player_turn"):
		damage_received_this_player_turn = maxi(0, int(snapshot["damage_received_this_player_turn"]))
	elif snapshot.has("damage_received"):
		damage_received_this_player_turn = maxi(0, int(snapshot["damage_received"]))
	if snapshot.has("stunned_turns"):
		stunned_turns = maxi(0, int(snapshot["stunned_turns"]))
	if snapshot.has("prepared_dash"):
		prepared_dash = bool(snapshot["prepared_dash"])
	if snapshot.has("cooldowns") and snapshot["cooldowns"] is Dictionary:
		cooldowns = (snapshot["cooldowns"] as Dictionary).duplicate(true)
	if snapshot.has("round_number"):
		round_number = maxi(1, int(snapshot["round_number"]))
	_last_total_durability = current_hp + current_shield
	_death_emitted = current_hp <= 0


func _state_changed() -> void:
	_invalidate_intent_cache()
	if debug_auto_refresh and not _executing_intent and _is_player_turn():
		refresh_intent_preview()
	else:
		_update_debug_panel()


func _invalidate_intent_cache() -> void:
	# A cache is only needed for repeated reads of one state.  Clearing it after
	# an actual state mutation prevents stale dictionaries from accumulating.
	_intent_cache.clear()


func _is_player_turn() -> bool:
	if _manual_mode or not is_instance_valid(_battle_manager):
		return true
	return int(_get_first_property(_battle_manager, [&"CurrentPhase", &"current_phase"], PLAYER_TURN_PHASE)) == PLAYER_TURN_PHASE


func _cell_count() -> int:
	return maxi(2, profile.cell_count if profile != null else maxi(debug_enemy_position, debug_player_position))


func _clamp_cell(value: int) -> int:
	return clampi(value, 1, _cell_count())


func _handle_possible_death() -> void:
	if current_hp <= 0 and not _death_emitted:
		_death_emitted = true
		emit_signal("enemy_died")


func _connect_first_signal(source: Object, names: Array[StringName], target: Callable) -> bool:
	if not is_instance_valid(source):
		return false
	for signal_name: StringName in names:
		if source.has_signal(signal_name):
			if not source.is_connected(signal_name, target):
				source.connect(signal_name, target)
			return true
	return false


func _call_first(source: Object, names: Array[StringName], arguments: Array) -> Array:
	if not is_instance_valid(source):
		return [false, null]
	for method_name: StringName in names:
		if source.has_method(method_name):
			return [true, source.callv(method_name, arguments)]
	return [false, null]


func _get_first_property(source: Object, names: Array[StringName], fallback: Variant) -> Variant:
	if not is_instance_valid(source):
		return fallback
	var wanted: Dictionary = {}
	for requested: StringName in names:
		wanted[_normalized_member_name(requested)] = true
	for entry: Dictionary in source.get_property_list():
		var actual := StringName(entry.get("name", &""))
		if wanted.has(_normalized_member_name(actual)):
			return source.get(actual)
	return fallback


func _normalized_member_name(value: StringName) -> String:
	return String(value).replace("_", "").to_lower()


func _update_debug_panel() -> void:
	var panel := find_child("DebugPanel", true, false) as CanvasItem
	if panel != null:
		panel.visible = show_debug_panel
	_set_debug_label("EnemyNameLabel", profile.display_name if profile != null else "No profile")
	_set_debug_label("HealthLabel", "HP %d/%d  Shield %d" % [current_hp, max_hp, current_shield])
	_set_debug_label("PositionLabel", "Enemy %d  Player %d  Distance %d" % [enemy_position, player_position, get_distance()])
	var preview := locked_intent if not locked_intent.is_empty() else current_intent
	_set_debug_label("IntentLabel", "Intent: %s" % String(preview.get("intent_text", "-")))
	_set_debug_label("CooldownLabel", "Cooldowns: %s" % str(cooldowns))
	_set_debug_label("StateLabel", "Damage %d  Prepared %s  Stun %d" % [damage_received_this_player_turn, str(prepared_dash), stunned_turns])


func _set_debug_label(node_name: String, text: String) -> void:
	var label := find_child(node_name, true, false)
	if label != null and _has_text_property(label):
		label.set("text", text)


func _has_text_property(source: Object) -> bool:
	for entry: Dictionary in source.get_property_list():
		if StringName(entry.get("name", &"")) == &"text":
			return true
	return false
