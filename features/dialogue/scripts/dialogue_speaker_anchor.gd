@tool
class_name DialogueSpeakerAnchor
extends Marker2D

## 将这个节点作为角色的子节点，并把对应的 Dialogic Character 拖进来。
## NPCDialogueCanvas 会自动发现它，不需要填写 NodePath 或写注册代码。

const ANCHOR_GROUP := &"npc_dialogue_speaker_anchor"

enum BubbleDirection {
	AUTO,
	EXTEND_RIGHT,
	EXTEND_LEFT,
}

@export_category("拖拽配置")
@export var dialogic_character: DialogicCharacter:
	set(value):
		dialogic_character = value
		update_configuration_warnings()
@export_enum("自动:0", "向右展开:1", "向左展开:2") var bubble_direction: int = BubbleDirection.AUTO
@export var screen_offset := Vector2.ZERO
@export var use_for_narration := false


func _enter_tree() -> void:
	add_to_group(ANCHOR_GROUP)


func matches_character(candidate: Variant) -> bool:
	if candidate == null:
		return use_for_narration
	if dialogic_character == null:
		return false
	if candidate == dialogic_character:
		return true

	var candidate_resource := candidate as Resource
	if candidate_resource != null:
		var own_path := dialogic_character.resource_path
		var candidate_path := candidate_resource.resource_path
		if not own_path.is_empty() and own_path == candidate_path:
			return true

	if candidate.has_method("get_identifier") and dialogic_character.has_method("get_identifier"):
		return str(candidate.call("get_identifier")) == str(dialogic_character.call("get_identifier"))
	return false


func get_screen_position() -> Vector2:
	return get_global_transform_with_canvas().origin + screen_offset


func _get_configuration_warnings() -> PackedStringArray:
	var warnings := PackedStringArray()
	if dialogic_character == null and not use_for_narration:
		warnings.append("请拖入这个角色对应的 Dialogic Character（.dch）。")
	return warnings
