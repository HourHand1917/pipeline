extends Node

const CAMPAIGN_PATH := "res://features/combat_campaign/data/default_combat_campaign.tres"

var checks := 0
var failures := 0


func _ready() -> void:
	var campaign := load(CAMPAIGN_PATH) as CombatCampaignData
	_check(campaign != null, "campaign resource must load")
	if campaign != null:
		_check(campaign.is_configuration_valid(), "campaign configuration must be valid")
		_check(campaign.battle_count == 4, "campaign must contain exactly four battles")
		_check(campaign.waves.size() == 5, "four battles must contain five runtime waves")
		var expected := [
			[1, 1, &"boom"],
			[2, 1, &"rocky_and_boom"],
			[3, 1, &"sharkk"],
			[4, 1, &"core00_hands"],
			[4, 2, &"core00_body"],
		]
		for index in expected.size():
			var wave := campaign.waves[index]
			_check(wave.battle_number == expected[index][0], "battle number mismatch at %d" % index)
			_check(wave.wave_number == expected[index][1], "wave number mismatch at %d" % index)
			_check(wave.id == expected[index][2], "wave id mismatch at %d" % index)
			_check(wave.game_rules != null and wave.game_rules.is_configuration_valid(), "invalid GameRules at %d" % index)
		_check(not campaign.waves[3].preserve_player_state, "Core hands must start as a fresh battle")
		_check(campaign.waves[4].preserve_player_state, "Core body must preserve player state")
		_check(campaign.waves[4].preserve_board_runtime_state, "Core body must preserve card runtime state")
		_check(not campaign.waves[4].show_build_before, "Core phase transition must skip build")

	var scene_text := FileAccess.get_file_as_string("res://features/combat_campaign/scenes/combat_campaign.tscn")
	_check(scene_text.contains("main_enemy_ai_node.tscn"), "campaign scene must include the AI bridge main")
	_check(scene_text.contains("CombatCampaignController"), "campaign scene must include controller")

	if failures == 0:
		print("COMBAT_CAMPAIGN_CONFIG_TEST PASS checks=%d" % checks)
	else:
		push_error("COMBAT_CAMPAIGN_CONFIG_TEST FAIL failures=%d checks=%d" % [failures, checks])
	get_tree().quit(0 if failures == 0 else 1)


func _check(condition: bool, message: String) -> void:
	checks += 1
	if condition:
		return
	failures += 1
	push_error("[CAMPAIGN TEST] " + message)

