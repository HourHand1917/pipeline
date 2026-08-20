extends Node

var _checks := 0
var _failures: Array[String] = []


func _ready() -> void:
	get_window().size = Vector2i(1280, 720)
	await get_tree().process_frame
	await get_tree().process_frame

	_check(get_node_or_null("/root/Dialogic") != null, "Dialogic autoload exists")
	_check(InputMap.has_action("interact"), "interact action exists")
	_check(InputMap.has_action("dialogic_default_action"), "Dialogic input action exists")

	var canvas_scene := load("res://features/dialogue/scenes/npc_dialogue_canvas.tscn") as PackedScene
	var npc_scene := load("res://features/dialogue/scenes/npc_interactables.tscn") as PackedScene
	var choice_scene := load("res://features/dialogue/scenes/dialogue_choice_bubble.tscn") as PackedScene
	var anchor_scene := load("res://features/dialogue/scenes/dialogue_speaker_anchor.tscn") as PackedScene
	var wasteland_theme := load("res://features/dialogue/themes/wasteland_industrial_dialogue_theme.tres") as Theme
	_check(canvas_scene != null, "canvas scene loads")
	_check(npc_scene != null, "npc scene loads")
	_check(choice_scene != null, "choice bubble scene loads")
	_check(anchor_scene != null, "speaker anchor scene loads")
	_check(wasteland_theme != null, "wasteland industrial theme loads")
	if canvas_scene == null or npc_scene == null or choice_scene == null or anchor_scene == null:
		_finish()
		return

	var reusable_choice := choice_scene.instantiate()
	_check(reusable_choice is DialogicNode_ChoiceButton, "choice bubble inherits official DialogicNode_ChoiceButton")
	_check(reusable_choice is NPCDialogueChoiceBubble, "choice bubble uses the project visual subclass")
	reusable_choice.queue_free()

	var canvas := canvas_scene.instantiate()
	add_child(canvas)
	await get_tree().process_frame
	_check(canvas.is_in_group("npc_dialogue_canvas"), "canvas registers discovery group")
	_check(canvas.max_visible_bubbles == 8, "history length is exposed with a practical default")
	_check(canvas.max_bubble_width == 520.0, "maximum bubble width is adjustable")
	_check(canvas.get_node_or_null("Root/ChoicePanel") == null, "choices have no outer panel")
	var choice_list := canvas.get_node("Root/ChoiceList") as VBoxContainer
	_check(choice_list != null, "choice list exists directly under the canvas")
	var official_choices := get_tree().get_nodes_in_group("dialogic_choice_button")
	_check(official_choices.size() == 8, "eight official choice buttons")
	_check(official_choices.all(func(button: Node) -> bool: return button is NPCDialogueChoiceBubble), "all choices use independent bubble scenes")
	_check(official_choices.all(func(button: Node) -> bool: return button.get_parent() == choice_list), "every choice bubble is a direct list item")
	_check(official_choices.all(func(button: Node) -> bool: return (button as Control).theme == wasteland_theme), "all choice bubbles reuse the wasteland theme")

	# 三个角色只需各拖一个 Anchor 并配置 .dch。Canvas 会按 Dialogic Character 自动选位置。
	var character_paths := [
		"res://features/dialogue/test_content/characters/鼠鼠.dch",
		"res://features/dialogue/test_content/characters/RUBBER.dch",
		"res://features/dialogue/test_content/characters/reb.dch",
	]
	var anchor_positions := [Vector2(230, 620), Vector2(500, 600), Vector2(1080, 570)]
	var anchors: Array[DialogueSpeakerAnchor] = []
	for index in character_paths.size():
		var speaker_anchor := anchor_scene.instantiate() as DialogueSpeakerAnchor
		speaker_anchor.dialogic_character = load(character_paths[index]) as DialogicCharacter
		speaker_anchor.position = anchor_positions[index]
		add_child(speaker_anchor)
		anchors.append(speaker_anchor)
	_check(anchors.all(func(item: DialogueSpeakerAnchor) -> bool: return item.is_in_group("npc_dialogue_speaker_anchor")), "speaker anchors auto-register")

	canvas.max_visible_bubbles = 3
	canvas.min_bubble_width = 180.0
	canvas.max_bubble_width = 360.0
	canvas._on_timeline_started()
	canvas._on_about_to_show_text({"text": "短句。", "character": anchors[0].dialogic_character, "append": false})
	await get_tree().process_frame
	await get_tree().create_timer(0.25).timeout
	var stack := canvas.get_node("Root/BubbleStack")
	var first_bubble := stack.get_child(0) as NPCDialogueBubble
	var first_start_y := first_bubble.position.y
	_check(first_bubble.get_bubble_width() >= 180.0 and first_bubble.get_bubble_width() <= 360.0, "short bubble respects adjustable width bounds")
	_check(absf(first_bubble.global_position.x + 34.0 - anchors[0].get_screen_position().x) < 2.0, "mouse speaker bubble uses mouse anchor: bubble=%s target=%s anchor=%s viewport=%s" % [first_bubble.global_position.x, canvas._bubble_targets.get(first_bubble), anchors[0].get_screen_position().x, get_viewport().get_visible_rect().size])

	canvas._on_about_to_show_text({"text": "这是一个更长的回复，用来确认气泡会限制最大宽度并自动换行。", "character": anchors[1].dialogic_character, "append": false})
	await get_tree().process_frame
	await get_tree().create_timer(0.25).timeout
	var second_bubble := stack.get_child(1) as NPCDialogueBubble
	_check(first_bubble.position.y < first_start_y, "existing bubble moves upward after a new line")
	_check(second_bubble.get_bubble_width() <= 360.0, "long bubble never exceeds maximum width")
	_check(absf(second_bubble.global_position.x + 34.0 - anchors[1].get_screen_position().x) < 2.0, "rubber speaker bubble uses rubber anchor: bubble=%s anchor=%s" % [second_bubble.global_position.x, anchors[1].get_screen_position().x])
	var first_after_second_y := first_bubble.position.y
	var second_start_y := second_bubble.position.y

	canvas._on_about_to_show_text({"text": "第三位角色从右侧位置发言。", "character": anchors[2].dialogic_character, "append": false})
	await get_tree().process_frame
	await get_tree().create_timer(0.25).timeout
	var third_bubble := stack.get_child(2) as NPCDialogueBubble
	_check(first_bubble.position.y <= first_after_second_y, "oldest bubble never moves downward")
	_check(second_bubble.position.y < second_start_y, "second bubble is pushed upward")
	_check(absf(third_bubble.global_position.x + third_bubble.get_bubble_width() - 34.0 - anchors[2].get_screen_position().x) < 2.0, "reb speaker bubble uses right anchor: bubble=%s width=%s anchor=%s" % [third_bubble.global_position.x, third_bubble.get_bubble_width(), anchors[2].get_screen_position().x])

	canvas._on_about_to_show_text({"text": "第四句触发历史上限。", "character": anchors[0].dialogic_character, "append": false})
	await get_tree().process_frame
	_check(stack.get_child_count() == 3, "adjustable history limit removes only the oldest bubble")
	canvas._on_timeline_ended()
	canvas.max_visible_bubbles = 8

	var npc := npc_scene.instantiate()
	add_child(npc)
	var anchor := npc.get_node_or_null("BubbleAnchor")
	_check(anchor != null, "npc exposes bubble anchor")
	_check(anchor is DialogueSpeakerAnchor, "npc bundles a drag-configured speaker anchor")
	_check(npc.get_node_or_null("Sprite") != null, "npc keeps base Sprite node")
	_check(npc.get_node_or_null("DetectionRange") != null, "npc keeps base DetectionRange node")
	_check(npc.get_node_or_null("ClickZone/ClickShape") != null, "npc keeps base click nodes")

	var timeline := DialogicTimeline.new()
	timeline.from_text("第一条测试对白。\n第二条会把第一条向上顶。\n- 选择左边\n\t你点击了左边。\n- 选择右边\n\t你点击了右边。")
	npc.set("Timeline", timeline)

	var player_scene := load("res://features/exploration/scenes/player.tscn") as PackedScene
	var player := player_scene.instantiate()
	add_child(player)
	npc.emit_signal("body_entered", player)
	var interact_event := InputEventAction.new()
	interact_event.action = &"interact"
	interact_event.pressed = true
	npc.call("_UnhandledInput", interact_event)
	npc.call("_UnhandledInput", interact_event)

	await get_tree().process_frame
	await get_tree().process_frame
	_check(Dialogic.current_timeline != null, "NPC starts configured timeline through Dialogic")
	_check(canvas.get_node("Root").visible, "canvas becomes visible")
	_check(canvas.get_node("Root/BubbleStack").get_child_count() == 1, "first line creates one bubble")
	_check(get_tree().get_nodes_in_group("dialogic_dialog_text").size() == 1, "only active bubble is a Dialogic text node")

	Dialogic.Text.skip_text_reveal()
	await get_tree().process_frame
	Dialogic.Inputs.dialogic_action.emit()
	await get_tree().process_frame
	await get_tree().process_frame
	_check(canvas.get_node("Root/BubbleStack").get_child_count() == 2, "second line pushes history upward")
	_check(get_tree().get_nodes_in_group("dialogic_dialog_text").size() == 1, "history bubble is frozen")

	Dialogic.Text.skip_text_reveal()
	await get_tree().process_frame
	Dialogic.Inputs.dialogic_action.emit()
	await get_tree().create_timer(0.3).timeout

	var visible_choices: Array[Node] = get_tree().get_nodes_in_group("dialogic_choice_button").filter(
		func(button: Node) -> bool: return button.visible
	)
	_check(visible_choices.size() == 2, "Dialogic populates the two configured choices")
	_check(choice_list.modulate.a > 0.9, "independent choice bubbles are shown")
	_check(visible_choices.all(func(button: Node) -> bool: return button.get_node_or_null("Content/Row/ChoiceText") != null), "each choice owns its complete text bubble")
	_check(visible_choices[0].get_node("Content/Row/ChoiceText").text == "选择左边", "Dialogic writes text into the custom bubble")
	if not visible_choices.is_empty():
		visible_choices[0].call("_pressed")
		await get_tree().process_frame
		await get_tree().process_frame
		_check(canvas.get_node("Root/BubbleStack").get_child_count() == 3, "mouse choice continues its Dialogic branch")

	Dialogic.end_timeline(true)
	await get_tree().process_frame
	_finish()


func _check(condition: bool, label: String) -> void:
	_checks += 1
	if not condition:
		_failures.append(label)


func _finish() -> void:
	if _failures.is_empty():
		print("NPC_DIALOGUE_RUNTIME_TEST_PASS checks=", _checks)
		get_tree().quit(0)
	else:
		for failure in _failures:
			push_error("NPC dialogue test failed: " + failure)
		print("NPC_DIALOGUE_RUNTIME_TEST_FAIL checks=", _checks, " failures=", _failures.size())
		get_tree().quit(1)
