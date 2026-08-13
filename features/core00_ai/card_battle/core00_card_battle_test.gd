extends Node

const BATTLE_SCRIPT := preload("res://features/core00_ai/card_battle/core00_card_battle.gd")

var checks := 0
var failures: PackedStringArray = []
var battle: Core00CardBattle


func _ready() -> void:
	battle = BATTLE_SCRIPT.new() as Core00CardBattle
	battle.build_visual_ui = false
	add_child(battle)
	await get_tree().process_frame
	_test_catalog_and_charge_persistence()
	_test_range_gate()
	_test_exact_cooldown_semantics()
	_test_multihit()
	_test_sequential_movement()
	_test_anchored_hands_and_start_positions()
	_test_phase_transition()
	_test_jam()
	_test_victory()
	if failures.is_empty():
		print("CORE00 CARD BATTLE TEST PASS (%d checks)" % checks)
		get_tree().quit(0)
	else:
		for failure in failures:
			push_error(failure)
		print("CORE00 CARD BATTLE TEST FAIL (%d failures / %d checks)" % [failures.size(), checks])
		get_tree().quit(1)


func _test_catalog_and_charge_persistence() -> void:
	battle.debug_reset(6, 50)
	_check(battle.cards.size() == 14, "catalog loader must provide all 14 cards")
	battle.debug_set_light_points(1)
	var charged := battle.charge_card(&"scrap_fist", 1)
	_check(bool(charged.get("ok", false)), "scrap fist accepts one light point")
	_check(int(battle.get_card_state(&"scrap_fist").get("charge", -1)) == 1, "partial charge is recorded")
	battle.end_player_turn()
	_check(int(battle.get_card_state(&"scrap_fist").get("charge", -1)) == 1, "partial charge persists across turns")
	_check(battle.light_points == 2, "new player turn resets light points to two")


func _test_range_gate() -> void:
	battle.debug_reset(6, 50)
	battle.debug_set_card_charge(&"finger_knuckle_striker", 1)
	var blocked := battle.use_card(&"finger_knuckle_striker", &"true_hand")
	_check(not bool(blocked.get("ok", true)), "melee card is rejected outside range")
	_check(int(battle.get_card_state(&"finger_knuckle_striker").get("charge", 0)) == 1, "failed range check does not consume charge")
	_check(battle.debug_set_player_position(2), "debug position can move adjacent to True hand")
	var used := battle.use_card(&"finger_knuckle_striker", &"true_hand")
	_check(bool(used.get("ok", false)), "melee card succeeds at distance one")


func _test_exact_cooldown_semantics() -> void:
	battle.debug_reset(2, 50)
	battle.debug_set_card_charge(&"finger_knuckle_striker", 1)
	_check(bool(battle.use_card(&"finger_knuckle_striker", &"true_hand").get("ok", false)), "cooldown test card is used")
	_check(int(battle.get_card_state(&"finger_knuckle_striker").get("cooldown_remaining", -1)) == 1, "CD1 starts at one")
	battle.end_player_turn()
	_check(int(battle.get_card_state(&"finger_knuckle_striker").get("cooldown_remaining", -1)) == 1, "CD1 blocks the next full player turn")
	_check(not bool(battle.charge_card(&"finger_knuckle_striker", 1).get("ok", true)), "cooling card cannot be charged")
	battle.end_player_turn()
	_check(int(battle.get_card_state(&"finger_knuckle_striker").get("cooldown_remaining", -1)) == 0, "CD1 becomes ready after exactly one blocked turn")


func _test_multihit() -> void:
	battle.debug_reset(9, 50)
	battle.debug_set_card_charge(&"deadly_kiss", 3)
	var before := int(battle.boss.false_hand.get("hp", 0))
	var result := battle.use_card(&"deadly_kiss", &"false_hand")
	_check(bool(result.get("ok", false)), "two-hit gun is usable in range")
	_check((result.get("effects", []) as Array).size() == 2, "two-hit gun executes two effect entries")
	_check(before - int(battle.boss.false_hand.get("hp", 0)) == 10, "two independent five-damage hits total ten")


func _test_sequential_movement() -> void:
	battle.debug_reset(4, 50)
	battle.debug_set_card_charge(&"rocket_punch", 2)
	var result := battle.use_card(&"rocket_punch", &"true_hand")
	_check(bool(result.get("ok", false)), "rocket punch is usable at distance three")
	_check(battle.player_position == 3, "rocket punch moves player toward target before damage")
	_check(int(battle.boss.true_hand.get("hp", 0)) == 17, "rocket punch deals four after moving")


func _test_anchored_hands_and_start_positions() -> void:
	battle.debug_reset(1, 50)
	_check(battle.player_position == 2, "phase-one start clamps away from True hand")
	_check(battle.player_position == battle.boss.player_position, "battle and manager positions stay synchronized")
	battle.debug_reset(9, 50)
	battle.debug_set_card_charge(&"improvised_cannon", 3)
	var before := int(battle.boss.false_hand.get("position", 0))
	var result := battle.use_card(&"improvised_cannon", &"false_hand")
	_check(bool(result.get("ok", false)), "improvised cannon can hit the fixed False hand")
	_check(int(battle.boss.false_hand.get("position", 0)) == before, "phase-one fixed hand ignores push displacement")


func _test_phase_transition() -> void:
	battle.debug_reset(6, 50)
	battle.debug_damage_target(&"true_hand", 999)
	battle.debug_damage_target(&"false_hand", 999)
	_check(battle.boss.phase == Core00EnermyMannager.Phase.BODY, "destroying both hands enters phase two")
	_check(int(battle.boss.body.get("hp", 0)) == 50, "phase-two body starts with 50 HP")
	_check(battle.selected_target_id == &"body", "target automatically switches to body")


func _test_jam() -> void:
	battle.debug_reset(6, 50)
	battle.debug_set_card_charge(&"scrap_fist", 1)
	battle.debug_queue_jam(1)
	_check(battle.debug_apply_pending_jam() == 1, "queued jam is applied")
	for card in battle.cards:
		_check(int(battle.get_card_state(card.id).get("cooldown_remaining", 0)) >= 1, "jam cools every card: %s" % card.id)
	_check(int(battle.get_card_state(&"scrap_fist").get("charge", 0)) == 1, "jam preserves accumulated charge")


func _test_victory() -> void:
	battle.debug_reset(6, 50)
	battle.debug_force_phase_two()
	battle.debug_damage_target(&"body", 999)
	_check(battle.battle_over, "body defeat ends battle")
	_check(battle.battle_result == &"victory", "body defeat produces victory result")


func _check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)
