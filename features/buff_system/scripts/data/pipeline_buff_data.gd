extends Buff
class_name PipelineBuffData

## Additive, inspector-friendly buff definition. It inherits the project's Buff
## resource so it can still be assigned to CombatEffectData.buff.

enum Scope {
	CHARACTER,
	CELL,
	CARD,
	ENCOUNTER,
}

enum DurationPhase {
	MANUAL,
	TURN_START,
	TURN_END,
}

enum StackMode {
	ADD,
	REFRESH,
	REPLACE,
}

@export_group("Pipeline Rules")
@export var scope: Scope = Scope.CHARACTER
@export var duration_phase: DurationPhase = DurationPhase.TURN_END
@export var stack_mode: StackMode = StackMode.REFRESH
@export var removable_by_cleanse: bool = true
@export var gameplay_tags: PackedStringArray = []

@export_group("Damage / Healing")
@export var outgoing_damage_flat_per_stack: int = 0
@export var incoming_damage_flat_per_stack: int = 0
@export_range(0.0, 4.0, 0.05) var outgoing_damage_multiplier: float = 1.0
@export_range(0.0, 4.0, 0.05) var incoming_damage_multiplier: float = 1.0
@export var tick_damage_per_stack: int = 0
@export var tick_heal_per_stack: int = 0
@export var shield_on_apply_per_stack: int = 0
@export var prevents_lethal_damage: bool = false
@export var consume_on_prevent_lethal: bool = true

@export_group("Board / Card Gates")
@export var extra_light_cost_per_stack: int = 0
@export var blocks_cell_lighting: bool = false
@export var blocks_card_use: bool = false
@export var card_cooldown_delta_on_apply: int = 0
@export var skip_next_action: bool = false


func has_gameplay_tag(tag: StringName) -> bool:
	return gameplay_tags.has(String(tag))


## Runtime hooks are intentionally context-based. Custom buff scripts may
## override these without changing PipelineBuffRuntime.
func on_runtime_apply(_context: PipelineBuffContext, _stacks: int) -> void:
	pass


func on_runtime_remove(_context: PipelineBuffContext, _stacks: int) -> void:
	pass


func on_runtime_tick(
	_context: PipelineBuffContext,
	_stacks: int,
	_phase: DurationPhase
) -> void:
	pass
