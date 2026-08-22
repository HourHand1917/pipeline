extends Node

var _checks := 0
var _failures: Array[String] = []
var _cancel_signal_count := 0
var _dialogue_finished_count := 0


func _ready() -> void:
	var canvas_scene := load("res://features/dialogue/scenes/npc_dialogue_canvas.tscn") as PackedScene
	_check(canvas_scene != null, "dialogue canvas scene loads")
	if canvas_scene == null:
		_finish()
		return

	var canvas := canvas_scene.instantiate() as NPCDialogueCanvas
	add_child(canvas)
	await _wait_frames(3)
	canvas.dialogue_cancel_requested.connect(_on_cancel_requested)
	_check(canvas.allow_escape_to_exit, "ESC exit is enabled by default")
	_check(canvas.escape_action == &"ui_cancel", "ESC exit action is Inspector configurable")

	var owner := DialogicCharacter.new()
	owner.display_name = "酒吧老板"
	owner.color = Color("e8a640")
	var rubber := DialogicCharacter.new()
	rubber.display_name = "RUBBER"
	rubber.color = Color("f06140")
	var reb := DialogicCharacter.new()
	reb.display_name = "reb"
	reb.color = Color("7b86d4")

	canvas._on_timeline_started()
	canvas._on_about_to_show_text({
		"text": "这些日子大家过得都苦。",
		"character": owner,
		"append": false,
	})
	await _wait_frames(2)
	var stack := canvas.get_node("Root/BubbleStack")
	var owner_bubble := stack.get_child(0) as NPCDialogueBubble
	owner_bubble.get_dialog_text_node().text = "这些日子大家过得都苦。"
	canvas._on_about_to_show_text({"text": "大家都来喝两杯。", "character": owner, "append": true})
	owner_bubble.get_dialog_text_node().text += "大家都来喝两杯。"

	# This is Dialogic's real ordering: it updates every registered name label
	# before the Canvas receives about_to_show_text for the next line.
	Dialogic.Text.update_name_label(rubber, true)
	owner_bubble.get_dialog_text_node().text = "不应该覆盖到老板气泡的下一句。"
	canvas._on_about_to_show_text({
		"text": "这世道，能活就不错了。",
		"character": rubber,
		"append": false,
	})
	await _wait_frames(2)
	var rubber_bubble := stack.get_child(1) as NPCDialogueBubble
	rubber_bubble.get_dialog_text_node().text = "这世道，能活就不错了。"

	_check(owner_bubble.speaker_name.text == "酒吧老板", "history keeps its original speaker")
	_check(owner_bubble.get_dialog_text_node().text == "这些日子大家过得都苦。大家都来喝两杯。", "history keeps its complete appended body")
	_check(not owner_bubble.speaker_name.is_in_group("dialogic_name_label"), "history speaker leaves Dialogic update group")
	_check(not owner_bubble.get_dialog_text_node().is_in_group("dialogic_dialog_text"), "history body leaves Dialogic update group")

	Dialogic.Text.update_name_label(reb, true)
	rubber_bubble.get_dialog_text_node().text = "不应该覆盖到 RUBBER 气泡的第三句。"
	canvas._on_about_to_show_text({
		"text": "返回一个错误。",
		"character": reb,
		"append": false,
	})
	await _wait_frames(2)
	_check(owner_bubble.speaker_name.text == "酒吧老板", "oldest speaker remains stable after multiple lines")
	_check(owner_bubble.get_dialog_text_node().text == "这些日子大家过得都苦。大家都来喝两杯。", "oldest body remains stable after multiple lines")
	_check(rubber_bubble.speaker_name.text == "RUBBER", "second history bubble keeps its speaker")
	_check(rubber_bubble.get_dialog_text_node().text == "这世道，能活就不错了。", "second history bubble keeps its body")

	canvas._on_timeline_ended()
	var timeline := DialogicTimeline.new()
	timeline.from_text("按 ESC 应当立即退出这段对话。")
	Dialogic.start_timeline(timeline)
	await _wait_frames(4)
	_check(Dialogic.current_timeline != null, "ESC test timeline starts")

	var escape_event := InputEventKey.new()
	escape_event.keycode = KEY_ESCAPE
	escape_event.physical_keycode = KEY_ESCAPE
	escape_event.pressed = true
	canvas._input(escape_event)
	await _wait_frames(4)
	_check(_cancel_signal_count == 1, "ESC emits one explicit cancellation signal")
	_check(Dialogic.current_timeline == null, "configured ESC action calls Dialogic.end_timeline(true)")
	_check(not canvas.get_node("Root").visible, "ESC exit hides the dialogue canvas")

	canvas.allow_escape_to_exit = false
	Dialogic.start_timeline(timeline)
	await _wait_frames(4)
	canvas._input(escape_event)
	await _wait_frames(2)
	_check(Dialogic.current_timeline != null, "Inspector switch can disable ESC exit")
	Dialogic.end_timeline(true)
	await _wait_frames(3)
	canvas.queue_free()
	await _wait_frames(3)
	await _test_npc_cancel_followups(escape_event)
	_finish()


func _test_npc_cancel_followups(escape_event: InputEventKey) -> void:
	var demo_scene := load("res://features/dialogue/examples/npc_dialogue_configuration_example.tscn") as PackedScene
	_check(demo_scene != null, "NPC cancellation integration scene loads")
	if demo_scene == null:
		return

	var demo := demo_scene.instantiate()
	add_child(demo)
	await _wait_frames(8)
	var player := demo.get_node("MapLayer/Player")
	var friendly := demo.get_node("MapLayer/FriendlyMouse")
	var hostile := demo.get_node("MapLayer/HostileBoom")
	var dialogue_canvas := demo.get_node("NPCDialogueCanvas") as NPCDialogueCanvas
	var shop := demo.get_node("UILayer/ShopUI") as Control
	_check(player != null and friendly != null and hostile != null, "configured Friendly/Hostile NPCs load")
	_check(dialogue_canvas != null and shop != null, "dialogue Canvas and shop load")
	if player == null or friendly == null or hostile == null or dialogue_canvas == null or shop == null:
		demo.queue_free()
		return

	if friendly.has_signal("DialogueFinished"):
		friendly.connect("DialogueFinished", _on_dialogue_finished)
	else:
		_failures.append("FriendlyNPC exposes DialogueFinished")

	# Set the shop request through the real Dialogic signal, then cancel. A
	# later normal Timeline without that signal must not inherit the request.
	var shop_signal_timeline := DialogicTimeline.new()
	shop_signal_timeline.from_text("[signal arg=\"open_shop\"]\n取消后不能打开商店。")
	friendly.set("OpenShopAfterDialogue", false)
	friendly.set("DialogueTimeline", shop_signal_timeline)
	friendly.call("HandleInteract")
	await _wait_frames(8)
	_check(Dialogic.current_timeline != null, "FriendlyNPC cancellation Timeline starts")
	_check(not bool(player.get("MovementEnabled")), "FriendlyNPC cancellation starts modal movement lock")
	dialogue_canvas._input(escape_event)
	await _wait_frames(8)
	_check(Dialogic.current_timeline == null, "FriendlyNPC ESC cancellation ends Timeline")
	_check(bool(player.get("MovementEnabled")), "FriendlyNPC ESC cancellation unlocks movement")
	_check(_dialogue_finished_count == 1, "FriendlyNPC still emits DialogueFinished when cancelled")
	_check(not shop.visible, "FriendlyNPC cancellation does not open the shop")

	var plain_timeline := DialogicTimeline.new()
	plain_timeline.from_text("这次正常结束，但没有开店信号。")
	friendly.set("DialogueTimeline", plain_timeline)
	friendly.call("HandleInteract")
	await _wait_frames(6)
	Dialogic.end_timeline(true)
	await _wait_frames(8)
	_check(not shop.visible, "cancelled open_shop request does not leak into the next dialogue")

	# Normal completion must keep the original OpenShopAfterDialogue behavior.
	friendly.set("OpenShopAfterDialogue", true)
	friendly.call("HandleInteract")
	await _wait_frames(6)
	Dialogic.end_timeline(true)
	await _wait_frames(8)
	_check(shop.visible, "normal FriendlyNPC completion still opens the shop")
	shop.hide()
	friendly.set("OpenShopAfterDialogue", false)

	# Hostile cancellation must not persist the intro or enter combat. While the
	# player remains inside AggroRadius it must stay closed; exit + re-entry arms it again.
	hostile.set_physics_process(false)
	hostile.set("DialogueTimeline", plain_timeline)
	hostile.emit_signal("body_entered", player)
	hostile.call("_PhysicsProcess", 0.0)
	await _wait_frames(8)
	_check(Dialogic.current_timeline != null, "HostileNPC intro Timeline starts")
	dialogue_canvas._input(escape_event)
	await _wait_frames(8)
	var cancelled_state := hostile.call("SaveState") as Dictionary
	_check(not bool(cancelled_state.get("intro_played", false)), "HostileNPC cancellation does not persist intro")
	_check(Dialogic.current_timeline == null, "HostileNPC cancellation does not enter battle")

	hostile.call("_PhysicsProcess", 0.0)
	await _wait_frames(3)
	_check(Dialogic.current_timeline == null, "HostileNPC stays closed while player remains in aggro range")
	hostile.emit_signal("body_exited", player)
	hostile.call("_PhysicsProcess", 0.0)
	hostile.emit_signal("body_entered", player)
	hostile.call("_PhysicsProcess", 0.0)
	await _wait_frames(8)
	_check(Dialogic.current_timeline != null, "HostileNPC can trigger again after exit and re-entry")
	dialogue_canvas._input(escape_event)
	await _wait_frames(6)
	demo.queue_free()
	await _wait_frames(3)


func _on_cancel_requested() -> void:
	_cancel_signal_count += 1


func _on_dialogue_finished() -> void:
	_dialogue_finished_count += 1


func _wait_frames(count: int) -> void:
	for _index in count:
		await get_tree().process_frame


func _check(condition: bool, label: String) -> void:
	_checks += 1
	if not condition:
		_failures.append(label)


func _finish() -> void:
	if _failures.is_empty():
		print("NPC_DIALOGUE_HISTORY_ESCAPE_TEST_PASS checks=", _checks)
		get_tree().quit(0)
	else:
		for failure in _failures:
			push_error("NPC dialogue history/ESC test failed: " + failure)
		print("NPC_DIALOGUE_HISTORY_ESCAPE_TEST_FAIL checks=", _checks, " failures=", _failures.size())
		get_tree().quit(1)
