class_name NPCDialogueBubble
extends PanelContainer

enum TailSide {
	LEFT,
	RIGHT,
}

@onready var speaker_name: Label = %SpeakerName
@onready var dialog_text: RichTextLabel = %DialogText
@onready var bubble_tail: Polygon2D = %BubbleTail

var _tail_side := TailSide.LEFT


func _ready() -> void:
	_update_tail_position()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_update_tail_position()


func setup(info: Dictionary, minimum_width := 220.0, maximum_width := 520.0) -> void:
	var character: Variant = info.get("character")
	if character != null:
		speaker_name.text = character.get_display_name_translated()
		speaker_name.self_modulate = character.color
	else:
		speaker_name.text = ""
		speaker_name.self_modulate = Color.WHITE

	var desired_width := _measure_bubble_width(str(info.get("text", "")), minimum_width, maximum_width)
	custom_minimum_size.x = desired_width
	size.x = desired_width


func set_tail_side(side: TailSide) -> void:
	_tail_side = side
	_update_tail_position()


func get_bubble_width() -> float:
	return maxf(custom_minimum_size.x, size.x)


func freeze_as_history() -> void:
	if dialog_text:
		dialog_text.set("enabled", false)
		dialog_text.remove_from_group("dialogic_dialog_text")
		dialog_text.set_process(false)
	if speaker_name:
		speaker_name.remove_from_group("dialogic_name_label")
	self_modulate = Color(0.88, 0.88, 0.88, 0.96)


func get_dialog_text_node() -> RichTextLabel:
	return dialog_text


func _update_tail_position() -> void:
	if is_instance_valid(bubble_tail):
		if _tail_side == TailSide.RIGHT:
			bubble_tail.position = Vector2(size.x - 34.0, size.y - 2.0)
			bubble_tail.polygon = PackedVector2Array([
				Vector2(0, 0), Vector2(-22, 0), Vector2(-7, 14)
			])
		else:
			bubble_tail.position = Vector2(34.0, size.y - 2.0)
			bubble_tail.polygon = PackedVector2Array([
				Vector2(0, 0), Vector2(22, 0), Vector2(7, 14)
			])


func _measure_bubble_width(text: String, minimum_width: float, maximum_width: float) -> float:
	var safe_minimum := minf(minimum_width, maximum_width)
	var safe_maximum := maxf(minimum_width, maximum_width)
	var widest_line := 0.0
	var font := ThemeDB.fallback_font
	for line in text.split("\n"):
		widest_line = maxf(
			widest_line,
			font.get_string_size(str(line), HORIZONTAL_ALIGNMENT_LEFT, -1.0, 25).x
		)
	return clampf(widest_line + 52.0, safe_minimum, safe_maximum)
