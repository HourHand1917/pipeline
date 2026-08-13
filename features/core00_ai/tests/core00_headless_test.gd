extends Node

const MANAGER_SCRIPT = preload("res://features/core00_ai/scripts/core00_enermy_mannager.gd")
const PROFILE = preload("res://features/core00_ai/profiles/core00_profile.tres")

var _failures: Array[String] = []
var _checks: int = 0


func _ready() -> void:
	_test_initial_state_and_phase_transition()
	_test_phase_one_even_odd_scans_and_cycle()
	_test_phase_one_shield_heal_and_death_loop()
	_test_phase_two_attack_ranges_and_cooldowns()
	_test_phase_two_move_teleport_and_jam()
	_test_turn_lifecycle_regressions()
	_test_full_fight_stress()
	if _failures.is_empty():
		print("CORE00_TESTS: PASS (%d checks)" % _checks)
		get_tree().quit(0)
	else:
		printerr("CORE00_TESTS: FAIL (%d failures / %d checks)" % [_failures.size(), _checks])
		for failure: String in _failures:
			printerr("  - %s" % failure)
		get_tree().quit(1)


func _test_initial_state_and_phase_transition() -> void:
	var manager = _make_manager(6)
	var state: Dictionary = manager.get_state()
	_check_equal(state["phase"], manager.Phase.HANDS, "initial: starts in hand phase")
	_check_equal(state["true_hand"]["hp"], 21, "initial: True has 21 HP")
	_check_equal(state["false_hand"]["hp"], 21, "initial: False has 21 HP")
	_check_equal(state["true_hand"]["position"], 1, "initial: True is anchored at cell 1")
	_check_equal(state["false_hand"]["position"], 12, "initial: False is anchored at cell 12")
	manager.take_damage(&"true_hand", 21)
	_check_equal(manager.get_state()["phase"], manager.Phase.HANDS, "transition: one dead hand does not start phase two")
	manager.take_damage(&"false_hand", 21)
	state = manager.get_state()
	_check_equal(state["phase"], manager.Phase.BODY, "transition: both dead hands start phase two")
	_check_equal(state["body"]["hp"], 50, "transition: body starts with 50 HP")
	_check_true(state["body"]["active"], "transition: body becomes active")
	manager.take_damage(&"body", 50)
	_check_equal(manager.get_state()["phase"], manager.Phase.DEFEATED, "transition: body death completes boss")
	manager.free()


func _test_phase_one_even_odd_scans_and_cycle() -> void:
	var even_manager = _make_manager(6, 100)
	var intent: Dictionary = even_manager.refresh_intent_preview()
	_check_equal(intent["cycle_step"], 1, "cycle: first preview is step 1")
	_check_true(intent["player_on_even_cell"], "scan: even cell is detected")
	even_manager.advance_enemy_turn_for_test()
	var state: Dictionary = even_manager.get_state()
	_check_equal(state["player_hp"], 94, "scan: protect beam deals configurable 6")
	_check_equal(state["player_logic_state"], &"true", "scan: protect beam applies True state")
	_check_equal(state["false_hand"]["shield"], 4, "cycle: False gains 4 shield while charging")
	even_manager.set_debug_snapshot({"cycle_step": 3, "player_position": 6})
	even_manager.advance_enemy_turn_for_test()
	state = even_manager.get_state()
	_check_equal(state["player_hp"], 88, "scan: break beam deals configurable 6")
	_check_equal(state["player_logic_state"], &"false", "scan: break beam applies False state")
	_check_true(not state["true_death_loop_active"], "scan: successful break beam ends death loop")
	even_manager.free()

	var odd_manager = _make_manager(5, 100)
	odd_manager.advance_enemy_turn_for_test()
	state = odd_manager.get_state()
	_check_equal(state["player_hp"], 100, "scan: odd cell avoids protect beam")
	_check_equal(state["player_logic_state"], &"none", "scan: odd cell receives no logic state")
	odd_manager.set_debug_snapshot({"cycle_step": 3, "player_position": 5})
	odd_manager.advance_enemy_turn_for_test()
	_check_equal(odd_manager.get_state()["player_hp"], 100, "scan: odd cell avoids break beam")
	odd_manager.free()


func _test_phase_one_shield_heal_and_death_loop() -> void:
	var manager = _make_manager(5)
	manager.set_debug_snapshot({"cycle_step": 2})
	manager.advance_enemy_turn_for_test()
	var state: Dictionary = manager.get_state()
	_check_true(state["true_death_loop_active"], "loop: step 2 enables True death loop")
	_check_equal(state["true_hand"]["shield"], 4, "loop: step 2 grants True shield")
	manager.take_damage(&"true_hand", 999)
	state = manager.get_state()
	_check_equal(state["true_hand"]["hp"], 1, "loop: protected True cannot drop below 1 HP")
	_check_equal(state["phase"], manager.Phase.HANDS, "loop: protected True cannot transition phase")
	manager.set_debug_snapshot({"cycle_step": 3, "player_position": 6})
	manager.advance_enemy_turn_for_test()
	manager.take_damage(&"true_hand", 999)
	_check_equal(manager.get_state()["true_hand"]["hp"], 0, "loop: break beam makes True killable")

	var healer = _make_manager(5)
	healer.take_damage(&"false_hand", 10)
	healer.set_debug_snapshot({"cycle_step": 4})
	healer.advance_enemy_turn_for_test()
	_check_true(healer.get_state()["false_heal_pending"], "heal: step 4 queues medkit")
	healer.advance_enemy_turn_for_test()
	state = healer.get_state()
	_check_equal(state["false_hand"]["hp"], 16, "heal: step 5 restores 5 HP")
	_check_true(not state["false_heal_pending"], "heal: pending medkit is consumed")
	healer.set_debug_snapshot({"cycle_step": 2})
	healer.advance_enemy_turn_for_test()
	healer.set_debug_snapshot({"cycle_step": 3})
	healer.advance_enemy_turn_for_test()
	_check_equal(healer.get_state()["true_hand"]["shield"], 8, "shield: repeated gain clamps at configured cap 8")
	manager.free()
	healer.free()


func _test_phase_two_attack_ranges_and_cooldowns() -> void:
	var manager = _make_manager(2, 100)
	manager.force_phase_two()
	manager.set_debug_snapshot({"body_position": 8, "player_position": 2, "round_number": 1})
	var intent: Dictionary = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"core00_sniper", "phase2: distance 6 selects sniper")
	_check_equal(intent["damage"], 12, "phase2: sniper damage is 12")
	manager.advance_enemy_turn_for_test()
	_check_equal(manager.get_state()["player_hp"], 88, "phase2: sniper applies 12 damage")
	_check_equal(manager.get_cooldown(&"core00_sniper"), 4, "cooldown: sniper starts at 4")
	_check_true(manager.refresh_intent_preview()["id"] != &"core00_sniper", "cooldown: sniper cannot immediately repeat")

	manager.set_debug_snapshot({"body_position": 3, "player_position": 2, "cooldowns": {}, "round_number": 1})
	intent = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"core00_gunstock", "phase2: distance 1 selects gunstock")
	_check_equal(intent["damage"], 9, "phase2: gunstock damage is 9")

	manager.set_debug_snapshot({"body_position": 5, "player_position": 2, "cooldowns": {}, "round_number": 1})
	intent = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"core00_pulse", "phase2: distance 3 selects pulse")
	_check_equal(intent["hit_count"], 3, "phase2: pulse has three hits")
	_check_equal(intent["damage_per_hit"], 2, "phase2: pulse deals 2 per hit")
	manager.free()


func _test_phase_two_move_teleport_and_jam() -> void:
	var manager = _make_manager(2, 100)
	manager.force_phase_two()
	manager.set_debug_snapshot({"body_position": 7, "player_position": 2, "round_number": 1, "cooldowns": {}})
	var intent: Dictionary = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"core00_advance", "move: uncovered distance 5 advances")
	_check_between(int(intent["body_to"]), 4, 6, "move: advances 1 to 3 cells toward player")
	_check_true(int(intent["body_to"]) < 7, "move: body moves in correct direction")

	manager.set_debug_snapshot({
		"body_position": 8,
		"player_position": 5,
		"round_number": 1,
		"cooldowns": {},
		"damage_received_this_player_turn": 8,
	})
	intent = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"core00_teleport_defend", "reaction: 8 received damage selects teleport")
	manager.advance_enemy_turn_for_test()
	var state: Dictionary = manager.get_state()
	_check_equal(state["body"]["position"], 4, "reaction: teleport lands behind player")
	_check_equal(state["body"]["shield"], 4, "reaction: teleport grants 4 shield")
	_check_equal(manager.get_cooldown(&"core00_teleport_defend"), 3, "reaction: teleport cooldown is 3")
	for _repeat in range(6):
		manager.set_debug_snapshot({"body_position": 8, "player_position": 5, "cooldowns": {}, "damage_received_this_player_turn": 8})
		manager.advance_enemy_turn()
	_check_equal(manager.get_state()["body"]["shield"], 12, "reaction: repeated teleport shield clamps at configured cap")
	manager.set_debug_snapshot({"body_position": 3, "player_position": 2, "cooldowns": {&"core00_gunstock": 1}, "damage_received_this_player_turn": 0})
	_check_true(manager.refresh_intent_preview()["id"] != &"core00_teleport_defend", "reaction: cooling attack alone never triggers defensive teleport")

	manager.set_debug_snapshot({"round_number": 4, "damage_received_this_player_turn": 0, "cooldowns": {}})
	intent = manager.refresh_intent_preview()
	_check_equal(intent["id"], &"core00_jam_cards", "jam: configured fourth round selects jam")
	manager.advance_enemy_turn_for_test()
	_check_equal(manager.get_state()["pending_card_jam_turns"], 1, "jam: effect is queued for next player turn")
	var cards: Array = [
		{"cooldown_remaining": 0},
		{"cooldown_remaining": 3},
		{},
	]
	_check_equal(manager.begin_player_turn(cards), 1, "jam: player-turn hook consumes one-turn jam")
	_check_equal(cards[0]["cooldown_remaining"], 1, "jam: ready card becomes cooldown 1")
	_check_equal(cards[1]["cooldown_remaining"], 3, "jam: longer existing cooldown is preserved")
	_check_equal(cards[2]["cooldown_remaining"], 1, "jam: missing cooldown field is initialized")
	_check_equal(manager.get_state()["pending_card_jam_turns"], 0, "jam: queue clears after application")
	manager.free()


func _test_turn_lifecycle_regressions() -> void:
	var manager = _make_manager(10, 200)
	var signal_counter := {"count": 0}
	manager.hand_health_changed.connect(func(_id, _hp, _max_hp, _shield): signal_counter["count"] += 1)
	manager.force_phase_two()
	var state: Dictionary = manager.get_state()
	_check_true(int(state["body"]["position"]) != int(state["player_position"]), "lifecycle: phase-two spawn never overlaps player")
	_check_true(int(signal_counter["count"]) >= 2, "lifecycle: force phase two emits both hand health updates")
	manager.set_debug_snapshot({"phase_two_turn": 3, "round_number": 9, "body_position": 5, "player_position": 2, "cooldowns": {}})
	var result: Dictionary = manager.advance_enemy_turn()
	state = result.get("state_after", {})
	_check_equal(state.get("round_number", 0), 10, "lifecycle: production advance increments round before state_after")
	_check_equal(state.get("phase_two_turn", 0), 4, "lifecycle: production advance increments phase-two clock")
	_check_true((state.get("locked_intent", {}) as Dictionary).is_empty(), "lifecycle: production advance clears locked intent")
	_check_equal((state.get("current_intent", {}) as Dictionary).get("id", &""), &"core00_jam_cards", "jam cadence: turn 4 previews jam")
	manager.advance_enemy_turn()
	_check_equal(manager.get_state()["pending_card_jam_turns"], 1, "jam cadence: turn 4 executes jam")
	manager.begin_player_turn([])
	for _turn in range(3):
		manager.advance_enemy_turn()
	_check_equal(manager.get_state()["phase_two_turn"], 8, "jam cadence: three intervening turns lead to turn 8")
	_check_equal(manager.get_state()["current_intent"]["id"], &"core00_jam_cards", "jam cadence: jam returns on turn 8")
	manager.free()

	var transition = _make_manager(6, 100)
	transition.take_damage(&"true_hand", 21)
	_check_equal(transition.get_state()["current_intent"]["true_action"]["id"], &"destroyed", "intent: destroyed hand previews no action")
	transition.take_damage(&"false_hand", 21)
	state = transition.get_state()
	_check_equal(state["phase"], transition.Phase.BODY, "intent: second hand death enters body phase")
	_check_true(not (state["current_intent"] as Dictionary).is_empty(), "intent: phase transition preserves fresh body preview")
	transition.free()


func _test_full_fight_stress() -> void:
	var rng := RandomNumberGenerator.new()
	rng.seed = 2281901
	for simulation in range(30):
		var manager = _make_manager(rng.randi_range(2, 11), 999)
		for turn in range(60):
			var state: Dictionary = manager.get_state()
			if state["phase"] == manager.Phase.HANDS:
				if rng.randi_range(0, 1) == 0:
					manager.take_damage(&"true_hand", rng.randi_range(1, 8))
				else:
					manager.take_damage(&"false_hand", rng.randi_range(1, 8))
				# Position changes exercise both odd/even fixed-cycle branches.
				manager.set_debug_snapshot({"player_position": rng.randi_range(2, 11)})
			elif state["phase"] == manager.Phase.BODY:
				manager.take_damage(&"body", rng.randi_range(1, 7))
				manager.set_debug_snapshot({"player_position": rng.randi_range(1, 12)})
			else:
				break
			var preview: Dictionary = manager.refresh_intent_preview()
			_check_true(not preview.is_empty(), "stress %d/%d: intent exists" % [simulation, turn])
			state = manager.get_state()
			_check_between(int(state["player_position"]), 1, 12, "stress %d/%d: player stays on track" % [simulation, turn])
			if state["phase"] == manager.Phase.BODY:
				_check_between(int(state["body"]["position"]), 1, 12, "stress %d/%d: body stays on track" % [simulation, turn])
			manager.advance_enemy_turn_for_test()
		manager.free()


func _make_manager(player_cell: int, hp: int = 100):
	var manager = MANAGER_SCRIPT.new()
	manager.profile = PROFILE
	manager.auto_start = false
	manager.show_debug_panel = false
	manager.reset_encounter(player_cell, hp)
	return manager


func _check_true(condition: bool, label: String) -> void:
	_checks += 1
	if not condition:
		_failures.append(label)


func _check_equal(actual: Variant, expected: Variant, label: String) -> void:
	_checks += 1
	if actual != expected:
		_failures.append("%s (expected %s, got %s)" % [label, str(expected), str(actual)])


func _check_between(actual: int, minimum: int, maximum: int, label: String) -> void:
	_checks += 1
	if actual < minimum or actual > maximum:
		_failures.append("%s (expected %d..%d, got %d)" % [label, minimum, maximum, actual])
