extends Node

var _checks := 0
var _failures: Array[String] = []


func _ready() -> void:
	var demo_scene := load("res://features/dialogue/examples/npc_dialogue_configuration_example.tscn") as PackedScene
	_check(demo_scene != null, "example scene loads")
	if demo_scene == null:
		_finish()
		return

	var demo := demo_scene.instantiate()
	add_child(demo)
	await _wait_frames(4)

	var player := demo.get_node("MapLayer/Player")
	var npc := demo.get_node("MapLayer/FriendlyMouse")
	var canvas := demo.get_node("NPCDialogueCanvas")
	npc.call("HandleInteract")
	await _wait_frames(7)

	_check(Dialogic.current_timeline != null, "configured timeline starts")
	_check(not bool(player.get("MovementEnabled")), "player movement locks during dialogue")
	_check(canvas.layer >= 100, "dialogue canvas moves above gameplay UI")
	var blocker := canvas.get_node_or_null("Root/DialogueInputBlocker") as Control
	_check(blocker != null, "full-screen dialogue mouse blocker exists")
	if blocker != null:
		_check(blocker.mouse_filter == Control.MOUSE_FILTER_STOP, "mouse blocker stops non-dialogue clicks")
		_check(blocker.anchor_right == 1.0 and blocker.anchor_bottom == 1.0, "mouse blocker covers the viewport")

	await _advance_line()
	var player_column := canvas.get_node("Root/PlayerBubbleColumn") as Control
	var npc_column := canvas.get_node("Root/NPCBubbleColumn") as Control
	_check(player_column.get_child_count() == 1, "RUBBER routes to the player anchor")
	if player_column.get_child_count() > 0:
		_check(_tail_is_left(player_column.get_child(0) as Control), "RUBBER tail is left when player is left")

	await _advance_line()
	_check(player_column.get_child_count() == 2, "reb also routes to the player anchor")
	for bubble in player_column.get_children():
		_check(_tail_is_left(bubble as Control), "all main-character tails remain left")

	# Direct repositioning verifies the dynamic flip without bypassing the production movement lock.
	player.global_position.x = npc.global_position.x + 220.0
	await _wait_frames(3)
	_check(player_column.position.x > npc_column.position.x, "player anchor updates to NPC right side")
	for bubble in player_column.get_children():
		_check(_tail_is_right(bubble as Control), "all main-character tails flip right")

	await _advance_line()
	await _advance_line()
	var visible_choices: Array[Node] = get_tree().get_nodes_in_group("dialogic_choice_button").filter(
		func(button: Node) -> bool: return button.visible
	)
	_check(visible_choices.size() == 2, "Dialogic shows two full-bubble choices")
	for choice in visible_choices:
		_check(_tail_is_right(choice as Control), "choice tail follows the player on the right")

	Dialogic.end_timeline(true)
	await _wait_frames(3)
	_check(bool(player.get("MovementEnabled")), "player movement unlocks after dialogue")
	_check(canvas.layer == 24, "dialogue canvas layer restores after dialogue")
	_check(canvas.get_node_or_null("Root/DialogueInputBlocker") == null, "mouse blocker is removed after dialogue")
	_finish()


func _advance_line() -> void:
	Dialogic.Text.skip_text_reveal()
	await get_tree().process_frame
	Dialogic.Inputs.dialogic_action.emit()
	await _wait_frames(7)


func _tail_is_left(bubble: Control) -> bool:
	if bubble == null:
		return false
	var tail := bubble.get_node_or_null("BubbleTail") as Polygon2D
	return tail != null and tail.scale.x > 0.0 and tail.position.x < bubble.size.x * 0.5


func _tail_is_right(bubble: Control) -> bool:
	if bubble == null:
		return false
	var tail := bubble.get_node_or_null("BubbleTail") as Polygon2D
	return tail != null and tail.scale.x < 0.0 and tail.position.x > bubble.size.x * 0.5


func _wait_frames(count: int) -> void:
	for _index in count:
		await get_tree().process_frame


func _check(condition: bool, label: String) -> void:
	_checks += 1
	if not condition:
		_failures.append(label)


func _finish() -> void:
	if _failures.is_empty():
		print("NPC_DIALOGUE_MODAL_TAIL_TEST_PASS checks=", _checks)
		get_tree().quit(0)
	else:
		for failure in _failures:
			push_error("NPC dialogue modal/tail test failed: " + failure)
		print("NPC_DIALOGUE_MODAL_TAIL_TEST_FAIL checks=", _checks, " failures=", _failures.size())
		get_tree().quit(1)
