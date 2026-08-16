@tool
class_name NPCDialogueChoiceBubble
extends DialogicNode_ChoiceButton

@onready var choice_number: Label = %ChoiceNumber
@onready var state_marker: ColorRect = %StateMarker

var _appearance_tween: Tween


func _notification(what: int) -> void:
	if what == NOTIFICATION_RESIZED:
		pivot_offset = size * 0.5


func _load_info(choice_info: Dictionary) -> void:
	super(choice_info)

	var button_number := int(choice_info.get("button_index", 0))
	choice_number.text = "%02d" % button_number
	tooltip_text = str(choice_info.get("text", ""))
	state_marker.color = Color("5cc7c0") if not disabled else Color("55514a")

	if Engine.is_editor_hint() or not visible:
		return

	if is_instance_valid(_appearance_tween):
		_appearance_tween.kill()
	modulate.a = 0.0
	scale = Vector2(0.985, 0.985)
	_appearance_tween = create_tween().set_parallel(true)
	_appearance_tween.set_trans(Tween.TRANS_QUAD).set_ease(Tween.EASE_OUT)
	_appearance_tween.tween_property(self, "modulate:a", 1.0, 0.16).set_delay(0.025 * max(button_number - 1, 0))
	_appearance_tween.tween_property(self, "scale", Vector2.ONE, 0.16).set_delay(0.025 * max(button_number - 1, 0))
