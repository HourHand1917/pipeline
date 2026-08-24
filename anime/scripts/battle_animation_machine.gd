class_name BattleAnimationMachine
extends Node

signal animation_requested(actor: Node, animation_name: StringName)

const PRIORITY_IDLE := 0
const PRIORITY_MOVE := 20
const PRIORITY_ACTION := 40
const PRIORITY_HURT := 70
const PRIORITY_DEATH := 100
const GLYPH_OWNER_META := &"anime_glyph_owner"
const FRAME_AUDIO_META := &"anime_frame_audio"

var actor: Node
var profile: BattleAnimationProfile
var battle_manager: Node
var anchor: Node2D
var sprite: AnimatedSprite2D
var audio_player: AudioStreamPlayer
var visual_overlay: Node2D
var current_slot: Variant
var last_position := 0
var last_hp := 0
var last_shield := 0
var last_hurt_frame := -1
var cached_action_id: StringName
var last_action_token := ""
var current_priority := PRIORITY_IDLE
var queued_animation: StringName
var queued_priority := PRIORITY_IDLE
var queued_action_sequence: Array[StringName] = []
var death_started := false
var death_finished := false
var actor_owner_id := 0
var _move_tween: Tween
var runtime_facing := 0
var _played_audio_cues := {}
var current_audio_animation: StringName
var current_audio_cue_token := ""
var visual_scale_multiplier := 1.0


func bind(combatant: Node, animation_profile: BattleAnimationProfile, manager: Node) -> void:
	actor = combatant
	actor_owner_id = combatant.get_instance_id()
	profile = animation_profile
	battle_manager = manager
	name = "Animation_%s" % _actor_key()
	last_position = int(_read_property(actor, [&"MapPosition", &"map_position"], 0))
	last_hp = int(_read_property(actor, [&"CurrentHp", &"current_hp"], 0))
	last_shield = int(_read_property(actor, [&"Shield", &"shield"], 0))
	_create_visual_nodes()
	if profile.has_animation_audio():
		actor.set_meta(FRAME_AUDIO_META, actor_owner_id)
	play_idle(true)


func sync_runtime_state(map_position: int, facing: int, current_hp: int, current_shield: int) -> void:
	# Position changes are still animated by the actor signal.  This method is
	# the typed C# bridge's authoritative initial/facing sync.
	if last_position == 0:
		last_position = map_position
	runtime_facing = facing
	if last_hp == 0 and current_hp > 0:
		last_hp = current_hp
	if last_shield == 0 and current_shield > 0:
		last_shield = current_shield
	sync_facing()


func set_visual_scale_multiplier(multiplier: float) -> void:
	visual_scale_multiplier = maxf(0.01, multiplier)
	if sprite != null and is_instance_valid(sprite):
		sprite.scale = _effective_scale() * visual_scale_multiplier
		sprite.position = _ground_preserving_sprite_offset()


func notify_health_changed(current: int, maximum: int) -> void:
	_on_health_changed(current, maximum)


func notify_shield_changed(current: int) -> void:
	_on_shield_changed(current)


func notify_position_changed(new_position: int) -> void:
	_on_position_changed(new_position)


func notify_intent_changed(action: Variant) -> void:
	_on_intent_changed(action)


func notify_died() -> void:
	_on_died()


func attach_to_slot(slot: Node, overlay: Node2D, _animate_move := true) -> void:
	_ensure_visual_nodes()
	if anchor == null or not is_instance_valid(anchor) \
			or sprite == null or not is_instance_valid(sprite):
		return
	if slot == null or not is_instance_valid(slot):
		detach_from_slot()
		return
	var occupant := slot.get_node_or_null("Vbox/occupant") as Control
	if occupant == null:
		detach_from_slot()
		return
	if overlay == null or not is_instance_valid(overlay):
		detach_from_slot()
		return
	visual_overlay = overlay
	# Validate the cached native instance before comparing it with the new slot.
	# A previous encounter may already have freed the cached TrackSlot.
	if is_instance_valid(current_slot) and slot == current_slot:
		if anchor.get_parent() != visual_overlay:
			anchor.reparent(visual_overlay, false)
		_hide_slot_glyph(current_slot)
		sprite.visible = profile.has_visual_frames() and not death_finished
		_layout_sprite(false)
		return
	_restore_slot_glyph(current_slot)
	current_slot = slot
	# The slot provides coordinates only. The animation itself lives in the
	# shared BattleScreen overlay and can extend outside this cell freely.
	if anchor.get_parent() != visual_overlay:
		anchor.reparent(visual_overlay, false)
	_hide_slot_glyph(current_slot)
	sprite.visible = profile.has_visual_frames() and not death_finished
	# A combatant is always fixed to its current logical cell. Movement is
	# expressed by the sequence itself, never by flying across unrelated UI.
	_layout_sprite(false)


func detach_from_slot() -> void:
	_restore_slot_glyph(current_slot)
	current_slot = null
	_stop_animation_audio()
	if sprite != null and is_instance_valid(sprite):
		sprite.visible = false


func sync_facing() -> void:
	if sprite == null or not is_instance_valid(sprite) \
			or profile == null or not profile.allow_horizontal_flip:
		return
	var faces_negative := runtime_facing == 1
	sprite.flip_h = faces_negative if profile.source_faces_right else not faces_negative


func play_action(action_id: StringName, action_token: int) -> void:
	if death_started or action_id.is_empty():
		return
	var token := "%s:%s:%s" % [action_token, _actor_key(), action_id]
	if token == last_action_token:
		return
	last_action_token = token
	queued_action_sequence.clear()
	var sequence := profile.resolve_action_sequence(action_id)
	if sequence.is_empty():
		return
	for index: int in range(1, sequence.size()):
		queued_action_sequence.append(sequence[index])
	request_animation(sequence[0], PRIORITY_ACTION, true)


func play_effect(effect_type: StringName) -> void:
	if death_started:
		return
	request_animation(profile.resolve_effect(effect_type), PRIORITY_ACTION, false)


func request_animation(animation_name: StringName, priority: int, restart := false) -> void:
	if profile == null:
		return
	_ensure_visual_nodes()
	if sprite == null or not is_instance_valid(sprite):
		return
	var resolved := profile.fallback_animation(animation_name)
	if resolved.is_empty():
		return
	# Missing action art falls back to a looping idle.  Treat that as idle
	# priority so a placeholder can never lock the machine above movement.
	if resolved == profile.fallback_animation(profile.idle_animation) \
			and profile.sprite_frames != null \
			and profile.sprite_frames.get_animation_loop(resolved):
		priority = PRIORITY_IDLE
	if current_priority > priority and sprite.is_playing():
		if priority >= queued_priority:
			queued_animation = resolved
			queued_priority = priority
		return
	if not restart and sprite.animation == resolved and sprite.is_playing():
		return
	# Frame cues belong to one actor and one currently playing animation.  Stop
	# the previous cue before replacing or rewinding the clip so a long sample
	# can never bleed into this actor's next action.
	_stop_animation_audio()
	current_priority = priority
	_played_audio_cues.clear()
	# AnimatedSprite2D.play() does not rewind an animation that is already
	# playing under the same name.  Explicitly rewind so rapid consecutive
	# hits/actions replay both the sequence and its frame-synchronised cue.
	if restart:
		sprite.stop()
		sprite.frame = 0
		sprite.frame_progress = 0.0
	sprite.play(resolved)
	_play_audio_for_current_frame()
	emit_signal(&"animation_requested", actor, resolved)


func play_idle(restart := false) -> void:
	if death_started:
		return
	request_animation(profile.idle_animation, PRIORITY_IDLE, restart)


func play_entry() -> void:
	if profile.has_animation(profile.entry_animation):
		request_animation(profile.entry_animation, PRIORITY_ACTION, true)


func should_remain_visible() -> bool:
	return not death_finished


func _create_visual_nodes() -> void:
	anchor = Node2D.new()
	anchor.name = "BattleAnimationAnchor"
	add_child(anchor)
	sprite = AnimatedSprite2D.new()
	sprite.name = "BattleAnimatedSprite"
	sprite.centered = true
	sprite.sprite_frames = profile.sprite_frames
	sprite.scale = _effective_scale() * visual_scale_multiplier
	sprite.z_index = profile.z_index
	sprite.visible = false
	anchor.add_child(sprite)
	sprite.animation_finished.connect(_on_animation_finished)
	sprite.frame_changed.connect(_on_frame_changed)
	audio_player = AudioStreamPlayer.new()
	audio_player.name = "BattleAnimationAudio"
	add_child(audio_player)
	audio_player.finished.connect(_on_audio_player_finished)


func _on_frame_changed() -> void:
	_play_audio_for_current_frame()


func _play_audio_for_current_frame() -> void:
	if profile == null or sprite == null or not is_instance_valid(sprite) \
			or audio_player == null or not is_instance_valid(audio_player):
		return
	for index: int in range(profile.audio_cues.size()):
		var cue := profile.audio_cues[index]
		if cue == null or not cue.is_valid_for(sprite.animation, sprite.frame):
			continue
		var token := "%s:%s:%s" % [sprite.animation, sprite.frame, index]
		if _played_audio_cues.has(token):
			continue
		_played_audio_cues[token] = true
		_play_owned_cue(cue, token)


func _play_owned_cue(cue: BattleAnimationAudioCue, token: String) -> void:
	var requested_bus := str(cue.bus)
	var resolved_bus: StringName = cue.bus if AudioServer.get_bus_index(requested_bus) >= 0 else &"Master"
	# One dedicated player per machine is the ownership boundary: different
	# combatants may sound together, but this combatant can own only one action
	# sound.  Replacing the stream also handles profiles that author more than
	# one cue in a single animation without overlap.
	_stop_animation_audio()
	audio_player.stream = cue.stream
	audio_player.volume_db = cue.volume_db
	audio_player.pitch_scale = cue.pitch_scale
	audio_player.bus = resolved_bus
	current_audio_animation = sprite.animation
	current_audio_cue_token = token
	audio_player.play()


func _stop_animation_audio() -> void:
	current_audio_animation = &""
	current_audio_cue_token = ""
	if audio_player == null or not is_instance_valid(audio_player):
		return
	if audio_player.playing:
		audio_player.stop()
	# Releasing the stream is deliberate: it makes stale-action ownership
	# observable in tests and prevents a later bare play() from reviving it.
	audio_player.stream = null


func _on_audio_player_finished() -> void:
	# The sample may be shorter than the animation. Relinquish ownership when it
	# ends naturally; the animation may later trigger another authored cue.
	current_audio_animation = &""
	current_audio_cue_token = ""
	if audio_player != null and is_instance_valid(audio_player):
		audio_player.stream = null


func _ensure_visual_nodes() -> void:
	if anchor != null and is_instance_valid(anchor) \
			and not anchor.is_queued_for_deletion() \
			and sprite != null and is_instance_valid(sprite) \
			and not sprite.is_queued_for_deletion() \
			and audio_player != null and is_instance_valid(audio_player) \
			and not audio_player.is_queued_for_deletion():
		return
	if anchor != null and is_instance_valid(anchor) and not anchor.is_queued_for_deletion():
		anchor.free()
	if audio_player != null and is_instance_valid(audio_player) \
			and not audio_player.is_queued_for_deletion():
		_stop_animation_audio()
		audio_player.free()
	anchor = null
	sprite = null
	audio_player = null
	current_priority = PRIORITY_IDLE
	queued_animation = &""
	queued_priority = PRIORITY_IDLE
	queued_action_sequence.clear()
	_create_visual_nodes()
	if death_started:
		request_animation(profile.death_animation, PRIORITY_DEATH, true)
	else:
		play_idle(true)


func _on_health_changed(current: int, _maximum: int) -> void:
	if current <= 0:
		last_hp = current
		return
	if current < last_hp:
		_play_hurt_once()
	last_hp = current


func _on_shield_changed(current: int) -> void:
	if current < last_shield:
		_play_hurt_once()
	last_shield = current


func _play_hurt_once() -> void:
	var frame := Engine.get_process_frames()
	if last_hurt_frame == frame:
		return
	last_hurt_frame = frame
	request_animation(profile.hurt_animation, PRIORITY_HURT, true)


func _on_position_changed(new_position: int) -> void:
	var delta := new_position - last_position
	last_position = new_position
	if delta == 0 or death_started:
		return
	var facing_direction := 1 if runtime_facing == 0 else -1
	var forward: bool = (delta > 0 and facing_direction > 0) or (delta < 0 and facing_direction < 0)
	var movement := profile.move_forward_animation if forward else profile.move_backward_animation
	request_animation(movement, PRIORITY_MOVE, true)


func _on_intent_changed(action: Variant) -> void:
	if action != null:
		cached_action_id = StringName(str(action.get("id")))
		return
	if death_started or cached_action_id.is_empty() or battle_manager == null:
		cached_action_id = &""
		return
	if int(_read_property(battle_manager, [&"CurrentPhase", &"current_phase"], -1)) == 2:
		play_action(cached_action_id, int(_read_property(battle_manager, [&"RoundNumber", &"round_number"], 0)))
	cached_action_id = &""


func _on_died() -> void:
	death_started = true
	queued_animation = &""
	queued_priority = PRIORITY_IDLE
	queued_action_sequence.clear()
	if not profile.has_animation(profile.death_animation):
		_stop_animation_audio()
		death_finished = true
		if sprite != null and is_instance_valid(sprite):
			sprite.visible = false
		_restore_slot_glyph(current_slot)
		return
	request_animation(profile.death_animation, PRIORITY_DEATH, true)


func _on_animation_finished() -> void:
	# Even when the source sample is longer than the authored frames, the sound
	# is part of this animation and must end at the same lifecycle boundary.
	_stop_animation_audio()
	if death_started and sprite.animation == profile.fallback_animation(profile.death_animation):
		death_finished = true
		sprite.visible = false
		_restore_slot_glyph(current_slot)
		return
	if not queued_action_sequence.is_empty():
		var next_action_animation: StringName = queued_action_sequence.pop_front()
		# A compound action may also emit PositionChanged for its retreat effect.
		# Do not replay the same backward clip a second time after the sequence.
		if queued_animation == next_action_animation:
			queued_animation = &""
			queued_priority = PRIORITY_IDLE
		current_priority = PRIORITY_IDLE
		request_animation(next_action_animation, PRIORITY_ACTION, true)
		return
	if not queued_animation.is_empty():
		var next := queued_animation
		var next_priority := queued_priority
		queued_animation = &""
		queued_priority = PRIORITY_IDLE
		current_priority = PRIORITY_IDLE
		request_animation(next, next_priority, true)
		return
	current_priority = PRIORITY_IDLE
	play_idle(true)


func _layout_sprite(_animate_move := false) -> void:
	if current_slot == null or not is_instance_valid(current_slot) \
			or anchor == null or not is_instance_valid(anchor) \
			or sprite == null or not is_instance_valid(sprite):
		return
	var occupant := current_slot.get_node_or_null("Vbox/occupant") as Control
	if occupant == null:
		return
	if visual_overlay == null or not is_instance_valid(visual_overlay):
		return
	if anchor.get_parent() != visual_overlay:
		anchor.reparent(visual_overlay, false)
	# Convert the authored foot point from the Control canvas into the shared
	# Node2D overlay. Never mix GlobalPosition with canvas transforms: doing so
	# made visuals drift when the 4:3 viewport or horizontal track camera moved.
	var occupant_width := occupant.size.x
	if occupant_width <= 1.0:
		occupant_width = maxf(1.0, float(current_slot.custom_minimum_size.x))
	var foot_local := Vector2(occupant_width * 0.5, occupant.size.y)
	var target_canvas := occupant.get_global_transform_with_canvas() * foot_local
	target_canvas += profile.visual_offset
	var target := visual_overlay.get_global_transform_with_canvas().affine_inverse() * target_canvas
	if _move_tween != null and _move_tween.is_valid():
		_move_tween.kill()
	anchor.position = target
	sprite.position = _ground_preserving_sprite_offset()
	sprite.scale = _effective_scale() * visual_scale_multiplier
	sprite.z_index = profile.z_index
	sync_facing()


## TrackSlot nodes are rebuilt between encounters. Keep this argument untyped:
## a typed Node parameter rejects an Object whose native instance was already
## freed before this function gets a chance to call is_instance_valid().
func _hide_slot_glyph(slot: Variant) -> void:
	if slot == null or not is_instance_valid(slot) or profile == null or not profile.has_visual_frames():
		return
	if actor_owner_id != 0:
		slot.set_meta(GLYPH_OWNER_META, actor_owner_id)
	var glyph := slot.get_node_or_null("Vbox/occupant/glyphlabel") as CanvasItem
	if glyph != null:
		glyph.visible = false


func _restore_slot_glyph(slot: Variant) -> void:
	if slot == null or not is_instance_valid(slot):
		return
	if slot.has_meta(GLYPH_OWNER_META):
		var owner_id := int(slot.get_meta(GLYPH_OWNER_META, 0))
		if actor_owner_id != 0 and owner_id != actor_owner_id:
			return
		slot.remove_meta(GLYPH_OWNER_META)
	var glyph := slot.get_node_or_null("Vbox/occupant/glyphlabel") as CanvasItem
	if glyph != null:
		glyph.visible = true


func _actor_key() -> String:
	if actor == null:
		return "missing"
	var enemy_id := str(_read_property(actor, [&"EnemyId", &"enemy_id"], ""))
	if not enemy_id.is_empty():
		return "%s_%s" % [enemy_id, actor.get_instance_id()]
	return "player_%s" % actor.get_instance_id()


func _effective_scale() -> Vector2:
	if profile == null or profile.sprite_frames == null or profile.target_visual_height <= 0.0:
		return profile.visual_scale if profile != null else Vector2.ONE
	var animation_name := sprite.animation if sprite != null else profile.idle_animation
	if not profile.sprite_frames.has_animation(animation_name) \
			or profile.sprite_frames.get_frame_count(animation_name) == 0:
		animation_name = profile.fallback_animation(profile.idle_animation)
	if animation_name.is_empty() or profile.sprite_frames.get_frame_count(animation_name) == 0:
		return profile.visual_scale
	var texture := profile.sprite_frames.get_frame_texture(animation_name, 0)
	if texture == null or texture.get_height() <= 0:
		return profile.visual_scale
	var factor := profile.target_visual_height / float(texture.get_height())
	return profile.visual_scale * factor


func _ground_preserving_sprite_offset() -> Vector2:
	if profile == null or is_equal_approx(visual_scale_multiplier, 1.0):
		return Vector2.ZERO
	# Profiles are authored with their base frame bottom on the TrackSlot foot
	# line. Scaling a centered sprite would otherwise push half of the added
	# height below that line. Lift by exactly that amount; X remains untouched.
	var base_height := absf(profile.target_visual_height * profile.visual_scale.y)
	if base_height <= 0.0:
		return Vector2.ZERO
	return Vector2(0.0, -base_height * (visual_scale_multiplier - 1.0) * 0.5)


func _exit_tree() -> void:
	_stop_animation_audio()
	_restore_slot_glyph(current_slot)
	if actor != null and is_instance_valid(actor) and actor.has_meta(FRAME_AUDIO_META) \
			and int(actor.get_meta(FRAME_AUDIO_META, 0)) == actor_owner_id:
		actor.remove_meta(FRAME_AUDIO_META)
	if _move_tween != null and _move_tween.is_valid():
		_move_tween.kill()
	if anchor != null and is_instance_valid(anchor) and not anchor.is_queued_for_deletion():
		anchor.queue_free()


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
