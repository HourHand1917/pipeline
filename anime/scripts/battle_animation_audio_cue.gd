class_name BattleAnimationAudioCue
extends Resource

## Animation name in the owning BattleAnimationProfile.
@export var animation_name: StringName
## Zero-based AnimatedSprite2D frame that emits this sound.
@export_range(0, 999, 1) var trigger_frame := 0
@export var stream: AudioStream
@export_range(-60.0, 12.0, 0.1) var volume_db := 0.0
@export_range(0.25, 4.0, 0.01) var pitch_scale := 1.0
@export var bus: StringName = &"SFX"


func is_valid_for(animation: StringName, frame: int) -> bool:
	return stream != null and animation_name == animation and trigger_frame == frame
