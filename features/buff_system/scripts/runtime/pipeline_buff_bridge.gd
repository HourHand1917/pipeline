extends Node
class_name PipelineBuffBridge

## Generic no-framework-edit bridge. Drag current battle nodes into these
## fields, or call configure() after instancing this scene.

@export var runtime: PipelineBuffRuntime
@export var player_host: PipelineBuffHost
@export var enemy_hosts: Array[PipelineBuffHost] = []
@export var board_manager: Node


func _ready() -> void:
	if runtime == null:
		runtime = get_node_or_null("PipelineBuffRuntime") as PipelineBuffRuntime
	if player_host != null:
		player_host.board_manager = board_manager
		player_host.ensure_stats()
	for host in enemy_hosts:
		if host != null:
			host.board_manager = board_manager
			host.ensure_stats()


func configure(
	player_actor: Node,
	enemy_actors: Array[Node],
	board: Node
) -> void:
	board_manager = board
	if player_host != null:
		player_host.bind(player_actor, board)
	for index in range(mini(enemy_hosts.size(), enemy_actors.size())):
		enemy_hosts[index].bind(enemy_actors[index], board)


func apply_to_player(buff: PipelineBuffData, stacks: int = 1) -> bool:
	return runtime != null and runtime.apply_buff(player_host, buff, stacks)


func apply_to_enemy(index: int, buff: PipelineBuffData, stacks: int = 1) -> bool:
	return (
		runtime != null
		and index >= 0
		and index < enemy_hosts.size()
		and runtime.apply_buff(enemy_hosts[index], buff, stacks, player_host)
	)
