extends Node


func _ready() -> void:
	var failures: Array[String] = []
	var demo_scene := load("res://features/dialogue/test_content/dialogue_content_demo.tscn") as PackedScene
	if demo_scene == null:
		_finish(["demo scene failed to load"])
		return

	var demo := demo_scene.instantiate()
	add_child(demo)
	await get_tree().process_frame
	await get_tree().physics_frame
	await get_tree().process_frame

	var npc := demo.get_node_or_null("NPC")
	var player := demo.get_node_or_null("Player")
	var canvas := demo.get_node_or_null("NPCDialogueCanvas")
	if npc == null or player == null or canvas == null:
		failures.append("demo is missing NPC, Player, or NPCDialogueCanvas")
		_finish(failures)
		return

	if not npc.get("IsPlayerInRange"):
		npc.emit_signal("body_entered", player)
	var interaction := InputEventAction.new()
	interaction.action = &"interact"
	interaction.pressed = true
	npc.call("_UnhandledInput", interaction)
	await get_tree().process_frame
	await get_tree().process_frame
	await get_tree().process_frame

	if Dialogic.current_timeline == null:
		failures.append("configured NPC did not start its Dialogic timeline")
	else:
		var expected := "res://features/dialogue/test_content/timelines/水龙头与上层工程师.dtl"
		if Dialogic.current_timeline.resource_path != expected:
			failures.append("NPC started a different timeline")

	var bubble_stack := canvas.get_node_or_null("Root/BubbleStack")
	if bubble_stack == null or bubble_stack.get_child_count() != 1:
		failures.append("first user-authored line did not create one dialogue bubble")

	if Dialogic.current_timeline != null:
		Dialogic.end_timeline(true)
	await get_tree().process_frame
	_finish(failures)


func _finish(failures: Array[String]) -> void:
	if failures.is_empty():
		print("DIALOGUE_CONTENT_FLOW_TEST_PASS")
		get_tree().quit(0)
	else:
		for failure: String in failures:
			push_error(failure)
		print("DIALOGUE_CONTENT_FLOW_TEST_FAIL count=", failures.size())
		get_tree().quit(1)
