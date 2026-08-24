extends Node

const F4_SCENE := "res://features/exploration/scenes/f4/f4.tscn"
const OUTRO_TIMELINE := "res://features/dialogue/npc/timelines/Code_00战后.dtl"
const FINISH_SCENE := "res://features/exploration/f4_sequence/f4_phase_two_outro_test_finish.tscn"


func _ready() -> void:
	var game_state := get_node("/root/GameState")
	game_state.call("SetObjectState", "f4", "f4_core00_phase_two", {"defeated": true})
	game_state.call("ClearObjectState", "f4", "f4_core00_phase_two_outro")

	var packed := load(F4_SCENE) as PackedScene
	if packed == null:
		_fail("F4 scene failed to load")
		return
	var f4 := packed.instantiate()
	var sequence := f4.get_node_or_null("Core00Encounter")
	if sequence == null:
		_fail("Core00Encounter is missing")
		return
	sequence.PhaseTwoVictoryScenePath = FINISH_SCENE
	add_child(f4)

	for _frame in 180:
		if Dialogic.current_timeline != null:
			break
		await get_tree().process_frame
	if Dialogic.current_timeline == null:
		_fail("phase-two victory did not force the post-battle timeline")
		return
	if Dialogic.current_timeline.resource_path != OUTRO_TIMELINE:
		_fail("phase-two victory started the wrong timeline")
		return

	# The contract test validates all 14 authored lines verbatim.  Here we end
	# the real Dialogic timeline through its public API to exercise the same
	# timeline_ended signal used after normal player advancement.
	Dialogic.end_timeline(true)
	for _frame in 60:
		if Dialogic.current_timeline == null:
			break
		await get_tree().process_frame
	if Dialogic.current_timeline != null:
		_fail("post-battle Dialogic timeline did not emit its completion")
		return

	# F4BossSequence owns the transition.  The finish scene checks that the
	# replay guard was committed before the ending was opened.
	await get_tree().create_timer(8.0).timeout
	_fail("post-battle dialogue completed but the ending scene was not opened")


func _fail(message: String) -> void:
	push_error("F4_PHASE_TWO_OUTRO_RUNTIME_TEST_FAIL: " + message)
	get_tree().quit(1)
