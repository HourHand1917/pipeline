class_name NPCDialogueCanvas
extends CanvasLayer

signal dialogue_cancel_requested

const CANVAS_GROUP := &"npc_dialogue_canvas"
const SPEAKER_ANCHOR_GROUP := &"npc_dialogue_speaker_anchor"

@export_category("气泡历史")
@export var bubble_scene: PackedScene = preload("res://features/dialogue/scenes/dialogue_bubble.tscn")
@export_range(1, 64, 1) var max_visible_bubbles := 8
@export_range(120.0, 720.0, 10.0) var min_bubble_width := 220.0
@export_range(220.0, 1200.0, 10.0) var max_bubble_width := 520.0
@export_range(0.05, 0.6, 0.01) var bubble_tween_time := 0.22
@export_range(0.0, 48.0, 1.0) var bubble_spacing := 12.0

@export_category("位置与边界")
@export var bubble_offset := Vector2(0.0, -28.0)
@export_range(0.0, 120.0, 1.0) var screen_margin := 24.0

@export_category("输入")
@export var allow_escape_to_exit := true
@export var escape_action: StringName = &"ui_cancel"

@onready var root: Control = %Root
@onready var bubble_stack: Control = %BubbleStack
@onready var choice_list: VBoxContainer = %ChoiceList

var _dialogic: Node
var _fallback_anchor: Node2D
var _active_bubble: NPCDialogueBubble
var _bubbles: Array[Control] = []
var _bubble_targets: Dictionary = {}
var _layout_tween: Tween
var _choice_tween: Tween
var _connected := false
var _escape_exit_pending := false


func _ready() -> void:
	add_to_group(CANVAS_GROUP)
	root.hide()
	_apply_inspector_sizes()
	_set_choice_list_active(false)
	call_deferred("_connect_dialogic")


func _exit_tree() -> void:
	_disconnect_dialogic()


func _input(event: InputEvent) -> void:
	if not allow_escape_to_exit or _escape_exit_pending:
		return
	if not is_instance_valid(_dialogic) or Dialogic.current_timeline == null:
		return
	if escape_action.is_empty() or not InputMap.has_action(escape_action):
		return
	if event.is_action_pressed(escape_action):
		_escape_exit_pending = true
		get_viewport().set_input_as_handled()
		dialogue_cancel_requested.emit()
		Dialogic.end_timeline(true)


## 兼容旧 NPC：NPC 启动对话时把自身 BubbleAnchor 设为找不到角色锚点时的回退位置。
func set_dialogue_anchor(anchor: Node2D) -> void:
	_fallback_anchor = anchor
	_connect_dialogic()


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
	_escape_exit_pending = false
	_clear_bubbles()
	_apply_inspector_sizes()
	_set_choice_list_active(false)
	root.show()


func _on_timeline_ended() -> void:
	_escape_exit_pending = false
	_set_choice_list_active(false)
	root.hide()
	_clear_bubbles()
	_fallback_anchor = null


func _on_about_to_show_text(info: Dictionary) -> void:
	root.show()
	if bool(info.get("append", false)) and is_instance_valid(_active_bubble):
		_active_bubble.append_text_snapshot(str(info.get("text", "")))
		return

	if is_instance_valid(_active_bubble):
		_active_bubble.freeze_as_history()

	var bubble := bubble_scene.instantiate() as NPCDialogueBubble
	if bubble == null:
		push_error("DialogueBubble 场景根节点必须使用 NPCDialogueBubble 脚本。")
		return

	bubble_stack.add_child(bubble)
	bubble.setup(info, min_bubble_width, max_bubble_width)
	bubble.set_meta(&"speaker_anchor", _resolve_speaker_anchor(info.get("character")))
	_active_bubble = bubble
	_bubbles.append(bubble)

	while _bubbles.size() > max_visible_bubbles:
		_remove_oldest_bubble()

	call_deferred("_place_new_bubble", bubble, true)


func _on_question_shown(info: Dictionary) -> void:
	_set_choice_list_active(not bool(info.get("invalid", false)))


func _on_choice_selected(_info: Dictionary) -> void:
	_set_choice_list_active(false)


func _set_choice_list_active(active: bool) -> void:
	if not is_instance_valid(choice_list):
		return
	if is_instance_valid(_choice_tween):
		_choice_tween.kill()
	choice_list.mouse_filter = Control.MOUSE_FILTER_IGNORE
	if not active:
		choice_list.modulate.a = 0.0
		return
	choice_list.modulate.a = 0.0
	_choice_tween = create_tween()
	_choice_tween.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	_choice_tween.tween_property(choice_list, "modulate:a", 1.0, 0.14)


func _place_new_bubble(bubble: NPCDialogueBubble, animated := false) -> void:
	await get_tree().process_frame
	if not is_instance_valid(bubble):
		return

	_bubbles = _bubbles.filter(func(item: Control) -> bool: return is_instance_valid(item))
	var height := maxf(bubble.get_combined_minimum_size().y, bubble.size.y)

	var anchor: Node2D = null
	if bubble.has_meta(&"speaker_anchor"):
		anchor = bubble.get_meta(&"speaker_anchor") as Node2D
	var placement := _get_anchor_direction(anchor)
	var anchor_position := _get_anchor_screen_position(anchor)
	var width := bubble.get_bubble_width()
	var viewport_size := get_viewport().get_visible_rect().size
	var tail_side := NPCDialogueBubble.TailSide.LEFT
	var target_x := anchor_position.x - 34.0
	if placement == DialogueSpeakerAnchor.BubbleDirection.EXTEND_LEFT:
		tail_side = NPCDialogueBubble.TailSide.RIGHT
		target_x = anchor_position.x - width + 34.0
	elif placement == DialogueSpeakerAnchor.BubbleDirection.AUTO and anchor_position.x > viewport_size.x * 0.5:
		tail_side = NPCDialogueBubble.TailSide.RIGHT
		target_x = anchor_position.x - width + 34.0

	bubble.set_tail_side(tail_side)
	target_x = clampf(target_x, screen_margin, viewport_size.x - screen_margin - width)
	var target_y := anchor_position.y + bubble_offset.y - height
	target_y = clampf(target_y, screen_margin, viewport_size.y - screen_margin - height)
	var target := Vector2(target_x, target_y)
	_bubble_targets[bubble] = target

	# 从最新历史向最旧历史逐条排到新气泡上方。
	# min(old_y, candidate_y) 是单向约束：角色换边、移动或文本变短都不会使旧气泡下移。
	var history_cursor_y := target.y
	for index in range(_bubbles.size() - 2, -1, -1):
		var old_bubble := _bubbles[index]
		var old_height := maxf(old_bubble.get_combined_minimum_size().y, old_bubble.size.y)
		var old_target: Vector2 = _bubble_targets.get(old_bubble, old_bubble.position)
		var candidate_y := history_cursor_y - bubble_spacing - old_height
		old_target.y = minf(old_target.y, candidate_y)
		_bubble_targets[old_bubble] = old_target
		history_cursor_y = old_target.y

	if is_instance_valid(_layout_tween):
		_layout_tween.kill()
	_layout_tween = create_tween().set_parallel(true)
	_layout_tween.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	for item in _bubbles:
		var item_target: Vector2 = _bubble_targets.get(item, item.position)
		if item == bubble and animated:
			# 新气泡从略低处向上浮入，整个生命周期不会向下移动。
			item.position = item_target + Vector2(0.0, 22.0)
			item.modulate.a = 0.0
			_layout_tween.tween_property(item, "modulate:a", 1.0, bubble_tween_time)
		_layout_tween.tween_property(item, "position", item_target, bubble_tween_time)

func _resolve_speaker_anchor(character: Variant) -> Node2D:
	for candidate in get_tree().get_nodes_in_group(SPEAKER_ANCHOR_GROUP):
		if candidate is DialogueSpeakerAnchor and candidate.matches_character(character):
			return candidate as Node2D
	return _fallback_anchor


func _get_anchor_screen_position(anchor: Node2D) -> Vector2:
	var viewport_size := get_viewport().get_visible_rect().size
	if not is_instance_valid(anchor):
		return Vector2(viewport_size.x * 0.5, viewport_size.y * 0.62)
	if anchor is DialogueSpeakerAnchor:
		return (anchor as DialogueSpeakerAnchor).get_screen_position()
	return anchor.get_global_transform_with_canvas().origin


func _get_anchor_direction(anchor: Node2D) -> int:
	if anchor is DialogueSpeakerAnchor:
		return (anchor as DialogueSpeakerAnchor).bubble_direction
	return DialogueSpeakerAnchor.BubbleDirection.AUTO


func _apply_inspector_sizes() -> void:
	if not is_instance_valid(choice_list):
		return
	var choice_width := maxf(min_bubble_width, max_bubble_width)
	choice_list.custom_minimum_size.x = choice_width
	choice_list.offset_left = -choice_width * 0.5
	choice_list.offset_right = choice_width * 0.5
	for child in choice_list.get_children():
		if child is Control:
			(child as Control).custom_minimum_size.x = choice_width


func _remove_oldest_bubble() -> void:
	if _bubbles.is_empty():
		return
	var oldest: Control = _bubbles.pop_front()
	_bubble_targets.erase(oldest)
	if is_instance_valid(oldest):
		oldest.queue_free()


func _clear_bubbles() -> void:
	if is_instance_valid(_layout_tween):
		_layout_tween.kill()
	for bubble in _bubbles:
		if is_instance_valid(bubble):
			if bubble.has_method("freeze_as_history"):
				bubble.freeze_as_history()
			bubble.queue_free()
	_bubbles.clear()
	_bubble_targets.clear()
	_active_bubble = null
