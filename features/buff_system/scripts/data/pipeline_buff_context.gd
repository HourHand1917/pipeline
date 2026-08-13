extends RefCounted
class_name PipelineBuffContext

## The shared payload passed through buff hooks. Every field is optional so the
## same context can represent a character, board cell, card, or encounter.

var host: PipelineBuffHost
var stats: Stats
var actor: Node
var board_manager: Node
var cell: CellRuntime
var card_runtime: RefCounted
var source_host: PipelineBuffHost
var phase: PipelineBuffData.DurationPhase = PipelineBuffData.DurationPhase.MANUAL
var tags: PackedStringArray = []
var metadata: Dictionary = {}


static func for_host(target_host: PipelineBuffHost) -> PipelineBuffContext:
	var context := PipelineBuffContext.new()
	context.host = target_host
	if target_host != null:
		context.stats = target_host.get_stats()
		context.actor = target_host.actor
		context.board_manager = target_host.board_manager
	return context


static func for_cell(target_cell: CellRuntime, board: Node = null) -> PipelineBuffContext:
	var context := PipelineBuffContext.new()
	context.cell = target_cell
	context.board_manager = board
	if target_cell != null:
		context.stats = target_cell.stats
	return context
