class_name BattleAnimationProfile
extends Resource

@export_group("Identity")
@export var profile_id: StringName
@export var is_player := false
@export var enemy_ids: PackedStringArray = PackedStringArray()
@export var roles: PackedStringArray = PackedStringArray()

@export_group("Sprite Frames")
@export var sprite_frames: SpriteFrames
@export var idle_animation: StringName = &"idle"
@export var entry_animation: StringName
@export var move_forward_animation: StringName = &"move_forward"
@export var move_backward_animation: StringName = &"move_backward"
@export var hurt_animation: StringName = &"hurt"
@export var death_animation: StringName = &"death"

@export_group("Mappings")
@export var action_animations: Dictionary = {}
@export var effect_animations: Dictionary = {
	"damage": &"attack",
	"move_toward_opponent": &"move_forward",
	"move_away_from_opponent": &"move_backward",
	"heal": &"heal",
	"shield": &"defend",
	"energy": &"idle",
	"apply_buff": &"idle",
	"remove_buff": &"idle"
}

@export_group("Animation Audio")
## Frame-accurate audio cues. Trigger frames are zero-based.
@export var audio_cues: Array[BattleAnimationAudioCue] = []

@export_group("Layout")
## Final on-screen height. The machine derives scale from the imported frame,
## so changing import size_limit never changes apparent character size.
@export_range(32.0, 512.0, 1.0) var target_visual_height := 136.0
@export var visual_scale := Vector2.ONE
@export var visual_offset := Vector2(0.0, 0.0)
@export var source_faces_right := true
@export var allow_horizontal_flip := true
@export var z_index := 20
@export_range(0.0, 1.0, 0.01) var move_tween_duration := 0.18


func matches_actor(actor: Node, player: Node) -> bool:
	if is_player:
		return actor == player
	if actor == null or actor == player:
		return false
	var enemy_id := str(_read_property(actor, [&"EnemyId", &"enemy_id"], ""))
	var role := str(_read_property(actor, [&"Role", &"role"], ""))
	return enemy_ids.has(enemy_id) or roles.has(role)


func has_visual_frames() -> bool:
	if sprite_frames == null:
		return false
	for animation_name: StringName in sprite_frames.get_animation_names():
		if sprite_frames.get_frame_count(animation_name) > 0:
			return true
	return false


func has_animation(animation_name: StringName) -> bool:
	return not animation_name.is_empty() \
		and sprite_frames != null \
		and sprite_frames.has_animation(animation_name) \
		and sprite_frames.get_frame_count(animation_name) > 0


func resolve_action(action_id: StringName) -> StringName:
	var mapped := StringName(str(action_animations.get(action_id, "")))
	if has_animation(mapped):
		return mapped
	if _looks_like(action_id, ["advance", "forward"]):
		return fallback_animation(move_forward_animation)
	if _looks_like(action_id, ["retreat", "back", "away"]):
		return fallback_animation(move_backward_animation)
	if _looks_like(action_id, ["attack", "punch", "gunstock", "pulse", "sniper", "beam", "charge"]):
		return fallback_animation(&"attack")
	if _looks_like(action_id, ["heal", "repair"]):
		return fallback_animation(&"heal")
	if _looks_like(action_id, ["guard", "defend", "shield"]):
		return fallback_animation(&"defend")
	return fallback_animation(idle_animation)


func resolve_effect(effect_type: StringName) -> StringName:
	var mapped := StringName(str(effect_animations.get(effect_type, "")))
	return fallback_animation(mapped)


func has_animation_audio() -> bool:
	for cue: BattleAnimationAudioCue in audio_cues:
		if cue != null and cue.stream != null:
			return true
	return false


func fallback_animation(preferred: StringName) -> StringName:
	if has_animation(preferred):
		return preferred
	if has_animation(idle_animation):
		return idle_animation
	if sprite_frames != null:
		for animation_name: StringName in sprite_frames.get_animation_names():
			if sprite_frames.get_frame_count(animation_name) > 0:
				return animation_name
	return &""


func _looks_like(value: StringName, needles: Array[String]) -> bool:
	var lowered := str(value).to_lower()
	for needle: String in needles:
		if lowered.contains(needle):
			return true
	return false


func _read_property(object: Object, candidates: Array[StringName], fallback: Variant) -> Variant:
	if object == null:
		return fallback
	var available := {}
	for descriptor in object.get_property_list():
		available[StringName(descriptor.get("name", ""))] = true
	for candidate in candidates:
		if available.has(candidate):
			return object.get(candidate)
	return fallback
