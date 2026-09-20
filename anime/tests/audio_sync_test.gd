extends SceneTree

const PROFILE_PATHS := {
	"player": "res://anime/profiles/player.tres",
	"boom": "res://anime/profiles/boom.tres",
	"rocky": "res://anime/profiles/rocky.tres",
	"sharkk": "res://anime/profiles/sharkk.tres",
	"core00_true_hand": "res://anime/profiles/core00_true_hand.tres",
	"core00_false_hand": "res://anime/profiles/core00_false_hand.tres",
	"core00_body": "res://anime/profiles/core00_body.tres",
}

const CONTACT_FRAMES := {
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

const GENERIC_ATTACK_STREAM := \
	"res://tileset/music_resource/OGG/SFX/战斗反馈/小怪/COM_Enemy_Attack.ogg"
const GENERIC_BATTLE_MUSIC := "res://tileset/music_resource/OGG/music/MUS_Battle_OGG.ogg"
const BATTLE_START_STREAM := \
	"res://features/dialogue/npc/audio/combat/common/battle_start.ogg"

var failures: PackedStringArray = []


func _init() -> void:
	call_deferred("_run")


func _run() -> void:
	var profiles := {}
	for profile_id: String in PROFILE_PATHS:
		var profile := load(PROFILE_PATHS[profile_id]) as BattleAnimationProfile
		_check(profile != null, "%s profile loads" % profile_id)
		if profile == null:
			continue
		profiles[profile_id] = profile
		var expected: Dictionary = CONTACT_FRAMES[profile_id]
		_check(profile.audio_cues.size() == expected.size(),
			"%s has one cue for every authored sound" % profile_id)
		for cue: BattleAnimationAudioCue in profile.audio_cues:
			_check(cue != null and cue.stream != null,
				"%s cue stream loads" % profile_id)
			if cue == null:
				continue
			_check(expected.has(cue.animation_name),
				"%s/%s is an expected cue" % [profile_id, cue.animation_name])
			if not expected.has(cue.animation_name):
				continue
			_check(cue.trigger_frame == expected[cue.animation_name],
				"%s/%s triggers on contact frame %s" % [
					profile_id, cue.animation_name, expected[cue.animation_name]])
			_check(profile.sprite_frames.has_animation(cue.animation_name),
				"%s/%s targets an existing animation" % [profile_id, cue.animation_name])
			if profile.sprite_frames.has_animation(cue.animation_name):
				_check(cue.trigger_frame < profile.sprite_frames.get_frame_count(cue.animation_name),
					"%s/%s contact frame is inside the sequence" % [profile_id, cue.animation_name])

	var rocky := profiles.get("rocky") as BattleAnimationProfile
	if rocky != null:
		for cue: BattleAnimationAudioCue in rocky.audio_cues:
			if cue.animation_name in [&"attack_mid", &"attack_close"]:
				_check(cue.stream.resource_path == GENERIC_ATTACK_STREAM,
					"Rocky attack uses the combat attack sound, not the movement loop")

	var expected_authored_streams := {
		"player": "res://features/dialogue/npc/audio/combat/player/attack.ogg",
		"sharkk": "res://features/dialogue/npc/audio/combat/sharkk/death.ogg",
		"core00_body": "res://features/dialogue/npc/audio/combat/core00_body/death.ogg",
	}
	for profile_id: String in expected_authored_streams:
		var profile := profiles.get(profile_id) as BattleAnimationProfile
		if profile == null:
			continue
		var expected_path: String = expected_authored_streams[profile_id]
		_check(profile.audio_cues.any(func(cue: BattleAnimationAudioCue) -> bool:
			return cue != null and cue.stream != null and cue.stream.resource_path == expected_path),
			"%s uses its newly authored source audio" % profile_id)

	if profiles.has("boom"):
		await _test_runtime_contact_and_restart(profiles["boom"])
	_test_battle_music_and_start_cue()

	if failures.is_empty():
		print("AUDIO_SYNC_TEST_PASS: 31 cues load and stay aligned to authored contact frames")
		call_deferred("_finish", 0)
		return
	for failure: String in failures:
		push_error("AUDIO_SYNC_TEST_FAIL: %s" % failure)
	call_deferred("_finish", 1)


func _finish(exit_code: int) -> void:
	# Leave the async test stack before shutdown so temporary Resources and
	# one-shot players are released rather than reported as test leaks.
	await process_frame
	quit(exit_code)


func _test_runtime_contact_and_restart(boom: BattleAnimationProfile) -> void:
	var audio_host: Node = get_root().get_node_or_null("AudioManager")
	var original_children := audio_host.get_child_count() if audio_host != null else 0
	var actor := Node.new()
	actor.name = "AudioSyncActor"
	get_root().add_child(actor)
	var machine := BattleAnimationMachine.new()
	get_root().add_child(machine)
	machine.bind(actor, boom, null)
	machine.request_animation(&"attack", BattleAnimationMachine.PRIORITY_ACTION, true)
	machine.sprite.frame = 12
	var attack_stream := machine.audio_player.stream
	_check(attack_stream != null and machine.audio_player.playing,
		"contact frame starts the actor-owned sound")
	_check(machine.current_audio_animation == &"attack",
		"the playing cue records its owning animation")
	_check(machine.audio_player.bus in [&"SFX", &"Master"],
		"action audio stays on the configured effects bus")
	if audio_host != null:
		_check(audio_host.get_child_count() == original_children,
			"frame cues never create unowned global one-shots")

	# Restarting/replacing a clip is an immediate sound lifecycle boundary.
	machine.request_animation(&"attack", BattleAnimationMachine.PRIORITY_ACTION, true)
	_check(machine.audio_player.stream == null and not machine.audio_player.playing,
		"restarting an action stops and releases its previous cue")
	machine.sprite.frame = 12
	_check(machine.audio_player.stream == attack_stream and machine.audio_player.playing,
		"repeated attacks rewind and emit their contact cue again")
	machine.request_animation(&"hurt", BattleAnimationMachine.PRIORITY_HURT, true)
	_check(machine.audio_player.stream == null and not machine.audio_player.playing,
		"a replacement animation cannot overlap the prior action sound")
	machine.sprite.frame = 4
	_check(machine.audio_player.stream != null and machine.audio_player.stream != attack_stream,
		"the replacement animation triggers its own intended cue")
	machine._on_animation_finished()
	_check(machine.audio_player.stream == null and not machine.audio_player.playing,
		"animation completion stops a sample even when the sample is longer")
	machine.free()
	actor.free()


func _test_battle_music_and_start_cue() -> void:
	for scene_path: String in [
		"res://features/dialogue/npc/scenes/boom_enemy_npc.tscn",
		"res://features/dialogue/npc/scenes/rocky_prebattle_npc.tscn",
		"res://features/dialogue/npc/scenes/sharkk_prebattle_npc.tscn",
		"res://features/dialogue/npc/scenes/core00_phase_one_enemy_npc.tscn",
	]:
		var packed := load(scene_path) as PackedScene
		_check(packed != null, "%s loads for music audit" % scene_path)
		if packed == null:
			continue
		var npc := packed.instantiate()
		var music := npc.get("BattleMusic") as AudioStream
		_check(music != null and music.resource_path == GENERIC_BATTLE_MUSIC,
			"%s carries the common battle music" % scene_path)
		npc.free()

	var rocky_map_scene := load("res://features/exploration/scenes/f1/f1_2.tscn") as PackedScene
	_check(rocky_map_scene != null, "tutorial Rocky map loads for music audit")
	if rocky_map_scene != null:
		var rocky_map := rocky_map_scene.instantiate()
		var rocky_encounter := rocky_map.get_node_or_null("MapLayer/RockyPrebattleNPC")
		var rocky_music := rocky_encounter.get("BattleMusic") as AudioStream \
			if rocky_encounter != null else null
		_check(rocky_music != null and rocky_music.resource_path == GENERIC_BATTLE_MUSIC,
			"the actual tutorial Rocky encounter carries battle music")
		rocky_map.free()

	var f4_scene := load("res://features/exploration/scenes/f4/f4.tscn") as PackedScene
	_check(f4_scene != null, "F4 scene loads for both Core phase music audit")
	if f4_scene != null:
		var f4 := f4_scene.instantiate()
		var sequence := f4.get_node_or_null("Core00Encounter")
		var music := sequence.get("BattleMusic") as AudioStream if sequence != null else null
		_check(music != null and music.resource_path == GENERIC_BATTLE_MUSIC,
			"Core phase one and phase two share the configured battle music")
		f4.free()

	var director := get_root().get_node_or_null("BattleDirector")
	var start_sfx := director.get("BattleStartSfx") as AudioStream if director != null else null
	_check(start_sfx != null and start_sfx.resource_path == BATTLE_START_STREAM,
		"BattleDirector loads the common battle-start transition cue")


func _check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
