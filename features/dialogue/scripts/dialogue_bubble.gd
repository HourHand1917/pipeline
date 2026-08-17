class_name NPCDialogueBubble
extends PanelContainer

@onready var speaker_name: Label = %SpeakerName
@onready var dialog_text: RichTextLabel = %DialogText
@onready var bubble_tail: Polygon2D = %BubbleTail


func _ready() -> void:
	_update_tail_position()


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		_update_tail_position()


func setup(info: Dictionary) -> void:
	var character: Variant = info.get("character")
	if character != null:
		speaker_name.text = character.get_display_name_translated()
		speaker_name.self_modulate = character.color
	else:
		speaker_name.text = ""
		speaker_name.self_modulate = Color.WHITE


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
		bubble_tail.position = Vector2(34.0, size.y - 2.0)
