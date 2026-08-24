extends SceneTree

## Runtime ownership contract for battle action audio.
##
## Each BattleAnimationMachine owns exactly one AudioStreamPlayer. Sounds from
## different actors may coexist, while sounds from the same actor are replaced
## and stopped at the exact animation lifecycle boundary.

const BOOM_PROFILE := "res://anime/profiles/boom.tres"

var failures: PackedStringArray = []


func _init() -> void:
	call_deferred("_run")


func _run() -> void:
	var profile := load(BOOM_PROFILE) as BattleAnimationProfile
	_check(profile != null, "Boom profile loads")
	if profile == null:
		_finish()
		return

	var global_audio := get_root().get_node_or_null("AudioManager")
	var global_child_count := global_audio.get_child_count() if global_audio != null else 0
	var actor_a := Node.new()
	var actor_b := Node.new()
	actor_a.name = "LifecycleActorA"
	actor_b.name = "LifecycleActorB"
	get_root().add_child(actor_a)
	get_root().add_child(actor_b)
	var machine_a := BattleAnimationMachine.new()
	var machine_b := BattleAnimationMachine.new()
	get_root().add_child(machine_a)
	get_root().add_child(machine_b)
	machine_a.bind(actor_a, profile, null)
	machine_b.bind(actor_b, profile, null)

	_trigger(machine_a, &"attack", 12, BattleAnimationMachine.PRIORITY_ACTION)
	var actor_a_attack := machine_a.audio_player.stream
	_trigger(machine_b, &"hurt", 4, BattleAnimationMachine.PRIORITY_HURT)
	var actor_b_hurt := machine_b.audio_player.stream
	_check(actor_a_attack != null and actor_b_hurt != null,
		"both actors trigger their intended authored sounds")
	_check(machine_a.audio_player != machine_b.audio_player,
		"each combatant has an independent audio owner")
	_check(machine_a.audio_player.playing and machine_b.audio_player.playing,
		"different combatants may sound at the same time")
	_check(machine_a.audio_player.bus == &"SFX" and machine_b.audio_player.bus == &"SFX",
		"animation sounds remain on the SFX bus")
	if global_audio != null:
		_check(global_audio.get_child_count() == global_child_count,
			"action cues create no persistent global one-shots")

	# Replacing A cannot touch B, but must synchronously clear A's old sample.
	machine_a.request_animation(&"hurt", BattleAnimationMachine.PRIORITY_HURT, true)
	_check(not machine_a.audio_player.playing and machine_a.audio_player.stream == null,
		"replacement stops actor A's prior cue immediately")
	_check(machine_b.audio_player.playing and machine_b.audio_player.stream == actor_b_hurt,
		"replacing actor A never stops actor B")
	machine_a.sprite.frame = 4
	machine_a._play_audio_for_current_frame()
	_check(machine_a.audio_player.playing and machine_a.audio_player.stream == actor_b_hurt,
		"replacement animation starts its matching cue at the authored frame")
	_check(machine_a.audio_player.stream != actor_a_attack,
		"the stale attack sample is no longer owned by actor A")

	# Natural finish, forced detach, rewind, and destruction are all terminal
	# boundaries for the currently owned sound.
	machine_a._on_animation_finished()
	_check(not machine_a.audio_player.playing and machine_a.audio_player.stream == null,
		"natural animation completion stops and releases its sound")
	_trigger(machine_a, &"attack", 12, BattleAnimationMachine.PRIORITY_ACTION)
	machine_a.detach_from_slot()
	_check(not machine_a.audio_player.playing and machine_a.audio_player.stream == null,
		"detaching a combatant stops its action sound")
	_trigger(machine_a, &"attack", 12, BattleAnimationMachine.PRIORITY_ACTION)
	machine_a.request_animation(&"attack", BattleAnimationMachine.PRIORITY_ACTION, true)
	_check(not machine_a.audio_player.playing and machine_a.audio_player.stream == null,
		"restarting the same action cannot overlap its earlier cue")

	_check(actor_a.has_meta(&"anime_frame_audio") and actor_b.has_meta(&"anime_frame_audio"),
		"legacy immediate SFX stays suppressed while frame audio owns the actors")
	machine_a.free()
	_check(not actor_a.has_meta(&"anime_frame_audio"),
		"destroying a machine releases its legacy-audio ownership marker")
	_check(machine_b.audio_player.playing,
		"destroying actor A's machine leaves actor B's cue untouched")
	machine_b.free()
	actor_a.free()
	actor_b.free()

	_finish()


func _trigger(machine: BattleAnimationMachine, animation: StringName, frame: int, priority: int) -> void:
	machine.request_animation(animation, priority, true)
	machine.sprite.frame = frame
	# Explicit invocation keeps this contract deterministic even with a dummy
	# audio driver and before the first rendered frame.
	machine._play_audio_for_current_frame()


func _finish() -> void:
	if failures.is_empty():
		print("AUDIO_LIFECYCLE_TEST_PASS: actor-owned cues stop on replace, finish, detach and free")
		call_deferred("_quit_after_cleanup", 0)
		return
	for failure: String in failures:
		push_error("AUDIO_LIFECYCLE_TEST_FAIL: %s" % failure)
	call_deferred("_quit_after_cleanup", 1)


func _quit_after_cleanup(exit_code: int) -> void:
	await process_frame
	quit(exit_code)


func _check(condition: bool, message: String) -> void:
	if not condition:
		failures.append(message)
