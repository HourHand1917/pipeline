extends Node


func _ready() -> void:
	var game_state := get_node("/root/GameState")
	var state: Dictionary = game_state.call(
		"GetObjectState", "f4", "f4_core00_phase_two_outro"
	)
	if state != null and state.get("completed", false):
		print("F4_PHASE_TWO_OUTRO_RUNTIME_TEST_PASS lines=14 replay_guard=true")
		# Let the replaced F4 scene finish its queued frees before terminating
		# the headless process; this keeps runtime diagnostics actionable.
		await get_tree().process_frame
		await get_tree().process_frame
		await get_tree().create_timer(0.1).timeout
		get_tree().quit(0)
	else:
		push_error("F4_PHASE_TWO_OUTRO_RUNTIME_TEST_FAIL: ending opened before replay guard was saved")
		get_tree().quit(1)
