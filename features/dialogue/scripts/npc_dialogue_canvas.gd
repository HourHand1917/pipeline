class_name NPCDialogueCanvas
extends CanvasLayer

const CANVAS_GROUP := &"npc_dialogue_canvas"

@export var bubble_scene: PackedScene = preload("res://features/dialogue/scenes/dialogue_bubble.tscn")
@export_range(1, 12, 1) var max_visible_bubbles := 6
@export_range(320.0, 720.0, 10.0) var bubble_width := 520.0
@export var bubble_offset := Vector2(0.0, -108.0)
@export_range(0.05, 0.6, 0.01) var bubble_tween_time := 0.22
@export_range(0.0, 40.0, 1.0) var bubble_spacing := 12.0
@export_range(0.0, 80.0, 1.0) var screen_margin := 24.0

@onready var root: Control = %Root
@onready var bubble_stack: Control = %BubbleStack
@onready var choice_panel: PanelContainer = %ChoicePanel

var _dialogic: Node
var _anchor: Node2D
var _active_bubble: NPCDialogueBubble
var _bubbles: Array[Control] = []
var _stack_height := 0.0
var _layout_tween: Tween
var _connected := false


func _ready() -> void:
	add_to_group(CANVAS_GROUP)
	root.hide()
	_set_choice_panel_active(false)
	call_deferred("_connect_dialogic")
	set_process(true)


func _exit_tree() -> void:
	_disconnect_dialogic()


func set_dialogue_anchor(anchor: Node2D) -> void:
	_anchor = anchor
	_connect_dialogic()
	_update_anchor_position()


func _process(_delta: float) -> void:
	if root.visible:
		_update_anchor_position()


func _connect_dialogic() -> void:
	if _connected:
		return
	_dialogic = get_node_or_null("/root/Dialogic")
	if _dialogic == null:
		push_error("NPCDialogueCanvas 找不到 /root/Dialogic。")
		return

	_dialogic.timeline_started.connect(_on_timeline_started)
	_dialogic.timeline_ended.connect(_on_timeline_ended)
	_dialogic.Text.about_to_show_text.connect(_on_about_to_show_text)
	_dialogic.Choices.question_shown.connect(_on_question_shown)
	_dialogic.Choices.choice_selected.connect(_on_choice_selected)
	_connected = true


func _disconnect_dialogic() -> void:
	if not _connected or not is_instance_valid(_dialogic):
		return
	if _dialogic.timeline_started.is_connected(_on_timeline_started):
		_dialogic.timeline_started.disconnect(_on_timeline_started)
	if _dialogic.timeline_ended.is_connected(_on_timeline_ended):
		_dialogic.timeline_ended.disconnect(_on_timeline_ended)
	if _dialogic.Text.about_to_show_text.is_connected(_on_about_to_show_text):
		_dialogic.Text.about_to_show_text.disconnect(_on_about_to_show_text)
	if _dialogic.Choices.question_shown.is_connected(_on_question_shown):
		_dialogic.Choices.question_shown.disconnect(_on_question_shown)
	if _dialogic.Choices.choice_selected.is_connected(_on_choice_selected):
		_dialogic.Choices.choice_selected.disconnect(_on_choice_selected)
	_connected = false


func _on_timeline_started() -> void:
	_clear_bubbles()
	_set_choice_panel_active(false)
	root.show()
	_update_anchor_position()


func _on_timeline_ended() -> void:
	_set_choice_panel_active(false)
	root.hide()
	_clear_bubbles()
	_anchor = null


func _on_about_to_show_text(info: Dictionary) -> void:
	root.show()
	if bool(info.get("append", false)) and is_instance_valid(_active_bubble):
		return

	if is_instance_valid(_active_bubble):
		_active_bubble.freeze_as_history()

	var bubble := bubble_scene.instantiate() as NPCDialogueBubble
	if bubble == null:
		push_error("DialogueBubble 场景根节点必须使用 NPCDialogueBubble 脚本。")
		return

	bubble.custom_minimum_size.x = bubble_width
	bubble_stack.add_child(bubble)
	bubble.setup(info)
	_active_bubble = bubble
	_bubbles.append(bubble)

	while _bubbles.size() > max_visible_bubbles:
		var oldest: Control = _bubbles.pop_front()
		if is_instance_valid(oldest):
			oldest.queue_free()

	call_deferred("_layout_bubbles", true)


func _on_question_shown(info: Dictionary) -> void:
	_set_choice_panel_active(not bool(info.get("invalid", false)))


func _on_choice_selected(_info: Dictionary) -> void:
	_set_choice_panel_active(false)


func _set_choice_panel_active(active: bool) -> void:
	if not is_instance_valid(choice_panel):
		return
	choice_panel.modulate.a = 1.0 if active else 0.0
	choice_panel.mouse_filter = Control.MOUSE_FILTER_STOP if active else Control.MOUSE_FILTER_IGNORE


func _layout_bubbles(animated := false) -> void:
	await get_tree().process_frame
	_bubbles = _bubbles.filter(func(item: Control) -> bool: return is_instance_valid(item))

	var targets: Dictionary = {}
	var cursor_y := 0.0
	for index in range(_bubbles.size() - 1, -1, -1):
		var bubble := _bubbles[index]
		var height := maxf(bubble.get_combined_minimum_size().y, bubble.size.y)
		cursor_y += height
		targets[bubble] = Vector2(-bubble_width * 0.5, -cursor_y)
		cursor_y += bubble_spacing

	_stack_height = maxf(0.0, cursor_y - bubble_spacing)
	_update_anchor_position()

	if is_instance_valid(_layout_tween):
		_layout_tween.kill()
	_layout_tween = create_tween().set_parallel(true)
	_layout_tween.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)

	for bubble in _bubbles:
		var target: Vector2 = targets[bubble]
		if animated and bubble == _active_bubble and bubble.modulate.a >= 0.99:
			bubble.position = target + Vector2(0.0, 22.0)
			bubble.modulate.a = 0.0
		_layout_tween.tween_property(bubble, "position", target, bubble_tween_time)
		_layout_tween.tween_property(bubble, "modulate:a", 1.0, bubble_tween_time)


func _update_anchor_position() -> void:
	if not is_instance_valid(bubble_stack):
		return

	var viewport_size := get_viewport().get_visible_rect().size
	var anchor_position := Vector2(viewport_size.x * 0.5, viewport_size.y * 0.62)
	if is_instance_valid(_anchor):
		anchor_position = _anchor.get_global_transform_with_canvas().origin

	anchor_position += bubble_offset
	anchor_position.x = clampf(
		anchor_position.x,
		screen_margin + bubble_width * 0.5,
		viewport_size.x - screen_margin - bubble_width * 0.5
	)
	anchor_position.y = clampf(
		anchor_position.y,
		screen_margin + _stack_height,
		viewport_size.y - screen_margin
	)
	bubble_stack.position = anchor_position


func _clear_bubbles() -> void:
	if is_instance_valid(_layout_tween):
		_layout_tween.kill()
	for bubble in _bubbles:
		if is_instance_valid(bubble):
			if bubble.has_method("freeze_as_history"):
				bubble.freeze_as_history()
			bubble.queue_free()
	_bubbles.clear()
	_active_bubble = null
	_stack_height = 0.0
