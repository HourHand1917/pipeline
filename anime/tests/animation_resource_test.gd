extends SceneTree

const MANIFEST_PATH := "res://anime/import_manifest.json"
const PROFILE_PATHS := [
	"res://anime/profiles/player.tres",
	"res://anime/profiles/boom.tres",
	"res://anime/profiles/rocky.tres",
	"res://anime/profiles/sharkk.tres",
	"res://anime/profiles/core00_true_hand.tres",
	"res://anime/profiles/core00_false_hand.tres",
	"res://anime/profiles/core00_body.tres",
]

const EXPECTED_COUNTS := {
	"player": {
		"idle": 53, "move_forward": 27, "move_backward": 23,
		"attack": 27, "hurt": 23, "buff": 53,
	},
	"boom": {"idle": 47, "attack": 41, "hurt": 30, "death": 30},
	"rocky": {
		"idle": 47, "move_forward": 22, "move_backward": 22,
		"retreat": 22, "attack_mid": 23, "attack_close": 23,
		"defend": 22, "hurt": 23, "death": 93,
	},
	"sharkk": {
		"idle": 47, "move_forward": 9, "move_backward": 10,
		"attack": 28, "charge_prepare": 10, "charge": 28,
		"stunned": 17, "hurt": 17, "death": 17,
	},
	"core_hand": {
		"enter_left": 60, "enter_right": 60, "idle_1": 68,
		"idle_2": 68, "hurt": 22, "heal_snap": 58,
		"finger_flick": 55, "heavy_punch": 60, "death": 81,
	},
	"core_body": {
		"idle": 47, "move_forward": 14, "move_backward": 22,
		"retreat": 22, "sniper": 36, "attack_close": 36,
		"pulse_3": 36, "teleport_guard": 22, "jam_cast": 36,
		"hurt": 17, "death": 17,
	},
}

const EXPECTED_LOOPS := {
	"player": ["idle"],
	"boom": ["idle"],
	"rocky": ["idle"],
	"sharkk": ["idle"],
	"core_hand": ["idle_1", "idle_2"],
	"core_body": ["idle"],
}

const EXPECTED_ACTION_IDS := {
	"player": [
		"armored_shield", "deadly_kiss", "hemostatic_pump", "hydraulic_fist",
		"knuckle_striker", "mechanical_shoes", "military_power_pack", "pea_gun",
		"quad_coil_gun", "rocket_fist", "scrap_battery", "scrap_fist",
		"simple_cannon", "tactical_armor",
	],
	"boom": [
		"boom_advance_1", "boom_advance_2", "boom_attack",
		"trained_boom_advance_1", "trained_boom_advance_2",
		"trained_boom_attack_1", "trained_boom_attack_2",
	],
	"rocky": [
		"rocky_advance_1", "rocky_advance_2", "rocky_close_attack",
		"rocky_mid_attack", "rocky_defend", "rocky_retreat",
		"trained_rocky_advance_1", "trained_rocky_advance_2",
		"trained_rocky_close_attack", "trained_rocky_mid_attack",
		"trained_rocky_defend", "trained_rocky_retreat",
	],
	"sharkk": [
		"sharkk_advance", "sharkk_attack", "sharkk_punch",
		"sharkk_prepare_charge", "sharkk_charge", "sharkk_sand_retreat",
		"sharkk_stunned", "trained_sharkk_advance", "trained_sharkk_attack",
		"trained_sharkk_prepare_charge", "trained_sharkk_charge_d1",
		"trained_sharkk_charge_d2", "trained_sharkk_charge_d3",
		"trained_sharkk_charge_d4", "trained_sharkk_sand_retreat",
		"trained_sharkk_stunned",
	],
	"core00_true_hand": [
		"core_true_guard_beam", "core_true_guard_only", "core_true_death_loop",
		"core_true_send_heal", "core_true_charge",
	],
	"core00_false_hand": [
		"core_false_charge", "core_false_charge_complete", "core_false_break_beam",
		"core_false_break_only", "core_false_stun", "core_false_heal",
	],
	"core00_body": [
		"core_body_advance_1", "core_body_advance_2", "core_body_advance_3",
		"core_body_sniper", "core_body_gunstock", "core_body_pulse",
		"core_body_teleport_guard", "core_body_jam", "core_body_reposition",
		"trained_core_body_advance_1", "trained_core_body_advance_2",
		"trained_core_body_advance_3", "trained_core_body_sniper",
		"trained_core_body_gunstock", "trained_core_body_pulse",
		"trained_core_body_teleport_guard", "trained_core_body_disable",
	],
}

var checks := 0
var failures: Array[String] = []


func _initialize() -> void:
	call_deferred(&"_run")


func _run() -> void:
	_test_manifest_and_frames()
	_test_profiles()
	_test_scenes()
	if failures.is_empty():
		print("ANIMATION_RESOURCE_TEST_PASS checks=%d source_frames=1260 generated_frames=1671 profiles=7" % checks)
		quit(0)
		return
	for failure in failures:
		push_error("ANIMATION_RESOURCE_TEST_FAIL: %s" % failure)
	quit(1)


func _test_manifest_and_frames() -> void:
	var manifest: Variant = JSON.parse_string(FileAccess.get_file_as_string(MANIFEST_PATH))
	_check(manifest is Dictionary, "manifest parses")
	if not manifest is Dictionary:
		return
	var source_total := _count_png_recursive("res://anime_assets/frames")
	_check(source_total == 1260, "source frame total is 1260, got %d" % source_total)
	var generated_total := 0
	for actor_id: String in EXPECTED_COUNTS:
		var frames := load("res://anime/generated/%s_frames.tres" % actor_id) as SpriteFrames
		_check(frames != null, "%s SpriteFrames loads" % actor_id)
		if frames == null:
			continue
		for animation_name: String in EXPECTED_COUNTS[actor_id]:
			var expected: int = EXPECTED_COUNTS[actor_id][animation_name]
			_check(frames.has_animation(animation_name), "%s has %s" % [actor_id, animation_name])
			if not frames.has_animation(animation_name):
				continue
			var actual := frames.get_frame_count(animation_name)
			generated_total += actual
			_check(actual == expected, "%s/%s frames %d, got %d" % [actor_id, animation_name, expected, actual])
			_check(frames.get_animation_speed(animation_name) > 0.0, "%s/%s has positive fps" % [actor_id, animation_name])
			var should_loop: bool = EXPECTED_LOOPS[actor_id].has(animation_name)
			_check(frames.get_animation_loop(animation_name) == should_loop, "%s/%s loop contract" % [actor_id, animation_name])
			var texture := frames.get_frame_texture(animation_name, 0)
			_check(texture != null, "%s/%s first texture loads" % [actor_id, animation_name])
			if texture != null:
				_check(maxi(texture.get_width(), texture.get_height()) <= 256, "%s/%s runtime import limited to 256" % [actor_id, animation_name])
	_check(generated_total == 1671, "generated frame references total is 1671, got %d" % generated_total)


func _test_profiles() -> void:
	var profiles_by_id := {}
	for path: String in PROFILE_PATHS:
		var profile := load(path) as BattleAnimationProfile
		_check(profile != null, "%s loads" % path)
		if profile != null:
			profiles_by_id[str(profile.profile_id)] = profile
	_check(profiles_by_id.size() == 7, "seven unique profiles")
	for profile_id: String in EXPECTED_ACTION_IDS:
		var profile := profiles_by_id.get(profile_id) as BattleAnimationProfile
		_check(profile != null, "profile %s exists" % profile_id)
		if profile == null:
			continue
		for action_id: String in EXPECTED_ACTION_IDS[profile_id]:
			_check(profile.action_animations.has(StringName(action_id)), "%s maps %s" % [profile_id, action_id])
	for visual_profile in ["player", "boom", "rocky", "sharkk", "core00_true_hand", "core00_false_hand", "core00_body"]:
		_check((profiles_by_id[visual_profile] as BattleAnimationProfile).has_visual_frames(), "%s has visual frames" % visual_profile)
	for enemy_profile_id in [
		"boom", "rocky", "sharkk", "core00_true_hand",
		"core00_false_hand", "core00_body",
	]:
		_check((profiles_by_id[enemy_profile_id] as BattleAnimationProfile).allow_horizontal_flip,
			"%s allows the visual to face the player" % enemy_profile_id)
	var player := profiles_by_id.get("player") as BattleAnimationProfile
	_check(player != null and not player.source_faces_right,
		"player source orientation is corrected by horizontal flip")
	for attack_card in [
		"deadly_kiss", "hydraulic_fist", "knuckle_striker", "pea_gun",
		"quad_coil_gun", "rocket_fist", "scrap_fist", "simple_cannon",
	]:
		_check(player.action_animations.get(StringName(attack_card)) == &"attack",
			"%s uses player attack animation" % attack_card)
	for utility_card in [
		"armored_shield", "hemostatic_pump", "mechanical_shoes",
		"military_power_pack", "scrap_battery", "tactical_armor",
	]:
		_check(player.action_animations.get(StringName(utility_card)) == &"buff",
			"%s uses player buff animation" % utility_card)

	var sharkk := profiles_by_id.get("sharkk") as BattleAnimationProfile
	_check(sharkk != null and not sharkk.source_faces_right,
		"Sharkk source art is calibrated as left-facing so runtime facing points at the player")
	for sand_action in [&"sharkk_sand_retreat", &"trained_sharkk_sand_retreat"]:
		var sequence := sharkk.resolve_action_sequence(sand_action)
		_check(sequence == [&"attack", &"move_backward"],
			"%s plays attack then backward movement" % sand_action)

	var boom := profiles_by_id.get("boom") as BattleAnimationProfile
	_check(boom != null and boom.audio_cues.size() == 3,
		"Boom configures attack, hurt and death frame audio")
	if boom != null:
		var expected_cues := {&"attack": 12, &"hurt": 4, &"death": 5}
		for cue: BattleAnimationAudioCue in boom.audio_cues:
			_check(cue != null and expected_cues.has(cue.animation_name),
				"Boom audio cue targets a supported animation")
			if cue == null or not expected_cues.has(cue.animation_name):
				continue
			_check(cue.trigger_frame == expected_cues[cue.animation_name],
				"%s audio uses its impact frame" % cue.animation_name)
			_check(cue.stream != null, "%s audio stream loads" % cue.animation_name)
	var audio_counts := {
		"player": 1,
		"boom": 3,
		"rocky": 9,
		"sharkk": 5,
		"core00_true_hand": 6,
		"core00_false_hand": 6,
		"core00_body": 1,
	}
	# These frames are the first drawn contact/reaction frames in the authored
	# sequences.  Keeping the table in the contract test prevents later asset
	# imports from silently shifting an impact sound back to animation start.
	var audio_contact_frames := {
		"player": {&"attack": 12},
		"boom": {&"attack": 12, &"hurt": 4, &"death": 5},
		"rocky": {
			&"idle": 0, &"move_forward": 0, &"move_backward": 0, &"retreat": 0,
			&"attack_mid": 8, &"attack_close": 8, &"defend": 0,
			&"hurt": 3, &"death": 5,
		},
		"sharkk": {
			&"attack": 13, &"charge": 13, &"hurt": 3, &"stunned": 3, &"death": 3,
		},
		"core00_true_hand": {
			&"enter_left": 0, &"finger_flick": 24, &"heal_snap": 22,
			&"heavy_punch": 30, &"hurt": 3, &"death": 5,
		},
		"core00_false_hand": {
			&"enter_right": 0, &"finger_flick": 24, &"heal_snap": 22,
			&"heavy_punch": 30, &"hurt": 3, &"death": 5,
		},
		"core00_body": {&"death": 3},
	}
	for profile_id: String in profiles_by_id:
		var configured := profiles_by_id[profile_id] as BattleAnimationProfile
		if audio_counts.has(profile_id):
			_check(configured.audio_cues.size() == audio_counts[profile_id],
				"%s exposes every authored frame-audio cue" % profile_id)
			for cue: BattleAnimationAudioCue in configured.audio_cues:
				_check(cue != null and cue.stream != null,
					"%s cue loads its audio stream" % profile_id)
				var expected_frames: Dictionary = audio_contact_frames[profile_id]
				_check(cue != null and expected_frames.has(cue.animation_name),
					"%s cue has an authored contact-frame contract" % profile_id)
				if cue != null and expected_frames.has(cue.animation_name):
					_check(cue.trigger_frame == expected_frames[cue.animation_name],
						"%s/%s audio stays on contact frame %s" % [
							profile_id, cue.animation_name, expected_frames[cue.animation_name]])
				_check(cue != null and configured.sprite_frames.has_animation(cue.animation_name),
					"%s cue targets an existing animation" % profile_id)
				if cue != null and configured.sprite_frames.has_animation(cue.animation_name):
					_check(cue.trigger_frame < configured.sprite_frames.get_frame_count(cue.animation_name),
						"%s cue trigger is inside the sequence" % profile_id)
		else:
			_check(configured.audio_cues.is_empty(),
				"%s remains silent until a designer assigns cues" % profile_id)
	_test_audio_restart(boom)


func _test_audio_restart(boom: BattleAnimationProfile) -> void:
	if boom == null:
		return
	var audio_host: Node = get_root().get_node_or_null("AudioManager")
	var original_audio_children: int = audio_host.get_child_count() if audio_host != null else 0
	var actor := Node.new()
	actor.name = "AudioCueTestActor"
	get_root().add_child(actor)
	var machine := BattleAnimationMachine.new()
	get_root().add_child(machine)
	machine.bind(actor, boom, null)
	machine.request_animation(&"hurt", BattleAnimationMachine.PRIORITY_HURT, true)
	machine.sprite.frame = 10
	machine.request_animation(&"hurt", BattleAnimationMachine.PRIORITY_HURT, true)
	_check(machine.sprite.frame == 0, "restarting the same hurt animation rewinds to frame zero")
	_check(machine.audio_player != null, "animation machine owns a dedicated audio player")
	_check(actor.has_meta(&"anime_frame_audio"), "frame audio suppresses the legacy immediate duplicate")
	machine.sprite.frame = 4
	_check(machine.audio_player.playing and machine.audio_player.stream != null,
		"contact frame uses the machine's actor-owned player")
	if audio_host != null:
		_check(audio_host.get_child_count() == original_audio_children,
			"frame cues do not leak unowned one-shots into AudioManager")
	machine.request_animation(&"attack", BattleAnimationMachine.PRIORITY_HURT, true)
	_check(not machine.audio_player.playing and machine.audio_player.stream == null,
		"replacing an animation stops and releases the previous sound")
	machine.free()
	actor.free()


func _test_scenes() -> void:
	var hub_scene := load("res://anime/scenes/battle_animation_hub.tscn") as PackedScene
	var display_scene := load("res://anime/scenes/battle_animation_display.tscn") as PackedScene
	var wrapper_scene := load("res://anime/scenes/animated_combat_campaign.tscn") as PackedScene
	var battle_rule_scene := load("res://anime/scenes/animation_battle_rule_test.tscn") as PackedScene
	_check(hub_scene != null, "hub scene loads")
	_check(display_scene != null, "single drag-and-drop display scene loads")
	_check(wrapper_scene != null, "animated campaign wrapper loads")
	_check(battle_rule_scene != null, "BattleRules-driven copied combat scene loads")
	for battle_scene_path: String in [
		"res://anime/scenes/battles/boom_with_animation.tscn",
		"res://anime/scenes/battles/rocky_boom_with_animation.tscn",
		"res://anime/scenes/battles/sharkk_with_animation.tscn",
		"res://anime/scenes/battles/core00_hands_with_animation.tscn",
		"res://anime/scenes/battles/core00_body_with_animation.tscn",
	]:
		_check(load(battle_scene_path) is PackedScene, "%s loads" % battle_scene_path)
	for preset_scene_path: String in [
		"res://anime/scenes/battle_rule_presets/boom_animation_test.tscn",
		"res://anime/scenes/battle_rule_presets/rocky_boom_animation_test.tscn",
		"res://anime/scenes/battle_rule_presets/sharkk_animation_test.tscn",
		"res://anime/scenes/battle_rule_presets/core00_hands_animation_test.tscn",
		"res://anime/scenes/battle_rule_presets/core00_body_animation_test.tscn",
	]:
		_check(load(preset_scene_path) is PackedScene, "%s loads" % preset_scene_path)
	if hub_scene != null:
		var hub := hub_scene.instantiate()
		_check(hub.get_script() != null and hub.get_script().resource_path == "res://anime/scripts/BattleAnimationHub.cs", "hub scene uses typed animation bridge")
		_check((hub.get("Profiles") as Array).size() == 7, "hub has seven profiles")
		hub.free()
	if display_scene != null:
		var display := display_scene.instantiate() as Node2D
		_check(display != null and display.position == Vector2.ZERO,
			"display scene is a Node2D at origin")
		var nested_hub := display.get_node_or_null("BattleAnimationHub")
		_check(nested_hub != null and nested_hub.get_script() != null,
			"display scene contains the configured observer Hub")
		var nested_camera := display.get_node_or_null("BattleTrackCamera")
		_check(nested_camera != null and nested_camera.get_script() != null,
			"display scene contains the horizontal track camera")
		_check(display.get_script() != null \
			and display.get_script().resource_path == "res://anime/scripts/BattleAnimationDisplay.cs",
			"display root exposes camera and initial-facing settings in Inspector")
		_check(bool(display.get("EnemiesAlwaysFacePlayer")),
			"display defaults to enemies always facing the player")
		_check(int(display.get("PlayerInitialFacing")) == 0 \
			and int(display.get("EnemyInitialFacing")) == 1,
			"display exposes right-facing player and left-facing enemy defaults")
		display.free()
	var viewport_width := int(ProjectSettings.get_setting("display/window/size/viewport_width", 0))
	var viewport_height := int(ProjectSettings.get_setting("display/window/size/viewport_height", 0))
	_check(viewport_width == 1440 and viewport_height == 1080,
		"animation component preserves the existing 1440x1080 4:3 viewport")


func _count_png_recursive(path: String) -> int:
	var directory := DirAccess.open(path)
	if directory == null:
		return 0
	var total := 0
	for file_name in directory.get_files():
		if file_name.get_extension().to_lower() == "png":
			total += 1
	for folder_name in directory.get_directories():
		total += _count_png_recursive("%s/%s" % [path.trim_suffix("/"), folder_name])
	return total


func _check(condition: bool, message: String) -> void:
	checks += 1
	if not condition:
		failures.append(message)
