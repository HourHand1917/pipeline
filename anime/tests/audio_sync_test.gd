extends SceneTree

const PROFILE_PATHS := {
	"boom": "res://anime/profiles/boom.tres",
	"rocky": "res://anime/profiles/rocky.tres",
	"sharkk": "res://anime/profiles/sharkk.tres",
	"core00_true_hand": "res://anime/profiles/core00_true_hand.tres",
	"core00_false_hand": "res://anime/profiles/core00_false_hand.tres",
}

const CONTACT_FRAMES := {
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
}

const GENERIC_ATTACK_STREAM := \
	"res://tileset/music_resource/OGG/SFX/战斗反馈/小怪/COM_Enemy_Attack.ogg"

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

	if profiles.has("boom"):
		await _test_runtime_contact_and_restart(profiles["boom"])

	if failures.is_empty():
		print("AUDIO_SYNC_TEST_PASS: 29 cues load and stay aligned to authored contact frames")
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
	if audio_host != null:
		_check(audio_host.get_child_count() == original_children + 1,
			"contact frame emits exactly one persistent one-shot")
		for index: int in range(audio_host.get_child_count() - 1, original_children - 1, -1):
			audio_host.get_child(index).free()
		machine.request_animation(&"attack", BattleAnimationMachine.PRIORITY_ACTION, true)
		machine.sprite.frame = 12
		_check(audio_host.get_child_count() == original_children + 1,
			"repeated attacks rewind and emit their contact cue again")
		for index: int in range(audio_host.get_child_count() - 1, original_children - 1, -1):
			audio_host.get_child(index).free()
	else:
		_check(machine.audio_player.playing, "minimal scene uses local audio fallback")
	machine.free()
	actor.free()


func _check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
