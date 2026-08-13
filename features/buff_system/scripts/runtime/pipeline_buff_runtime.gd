extends Node
class_name PipelineBuffRuntime

signal buff_applied(host: PipelineBuffHost, buff: PipelineBuffData, stacks: int, duration: int)
signal buff_removed(host: PipelineBuffHost, buff_id: StringName)
signal buff_ticked(host: PipelineBuffHost, buff: PipelineBuffData, stacks: int, phase: int)
signal damage_packet_resolved(packet: PipelineDamagePacket)
signal damage_requested(host: PipelineBuffHost, amount: int, source_buff: PipelineBuffData)
signal heal_requested(host: PipelineBuffHost, amount: int, source_buff: PipelineBuffData)
signal shield_requested(host: PipelineBuffHost, amount: int, source_buff: PipelineBuffData)
signal cell_gate_changed(cell: CellRuntime)
signal card_cooldown_changed(card_runtime: RefCounted, remaining: int)
signal cleansed(host: PipelineBuffHost, removed_count: int)

@export var catalog: PipelineBuffCatalog


func apply_buff(
	host: PipelineBuffHost,
	buff: PipelineBuffData,
	stacks: int = 1,
	source_host: PipelineBuffHost = null
) -> bool:
	if host == null or buff == null or stacks <= 0:
		return false
	var stats := host.ensure_stats()
	if stats == null:
		return false

	var instance := _find_instance(stats, buff.id)
	if instance == null:
		instance = BuffInstance.new(buff, stacks)
		stats.buffs.append(instance)
	else:
		_apply_stack_rule(instance, buff, stacks)

	var context := PipelineBuffContext.for_host(host)
	context.source_host = source_host
	buff.on_apply(stats, instance.stacks)
	buff.on_runtime_apply(context, instance.stacks)
	if buff.shield_on_apply_per_stack > 0:
		_request_shield(host, buff.shield_on_apply_per_stack * instance.stacks, buff)
	if buff.card_cooldown_delta_on_apply != 0 and host.board_manager != null:
		change_all_card_cooldowns(host.board_manager, buff.card_cooldown_delta_on_apply)
	buff_applied.emit(host, buff, instance.stacks, instance.remaining_duration)
	return true


func apply_buff_by_id(
	host: PipelineBuffHost,
	buff_id: StringName,
	stacks: int = 1,
	source_host: PipelineBuffHost = null
) -> bool:
	if catalog == null:
		return false
	return apply_buff(host, catalog.get_buff(buff_id), stacks, source_host)


func apply_buff_to_cell(
	cell: CellRuntime,
	buff: PipelineBuffData,
	stacks: int = 1,
	board: Node = null
) -> bool:
	if cell == null or buff == null or stacks <= 0:
		return false
	var instance := _find_instance(cell.stats, buff.id)
	if instance == null:
		instance = BuffInstance.new(buff, stacks)
		cell.stats.buffs.append(instance)
	else:
		_apply_stack_rule(instance, buff, stacks)
	var context := PipelineBuffContext.for_cell(cell, board)
	buff.on_apply(cell.stats, instance.stacks)
	buff.on_runtime_apply(context, instance.stacks)
	cell_gate_changed.emit(cell)
	return true


func remove_buff(host: PipelineBuffHost, buff_id: StringName) -> bool:
	if host == null:
		return false
	var stats := host.get_stats()
	if stats == null:
		return false
	var removed := _remove_from_stats(stats, buff_id, PipelineBuffContext.for_host(host))
	if removed:
		buff_removed.emit(host, buff_id)
	return removed


func remove_buff_from_cell(cell: CellRuntime, buff_id: StringName, board: Node = null) -> bool:
	if cell == null:
		return false
	var removed := _remove_from_stats(
		cell.stats,
		buff_id,
		PipelineBuffContext.for_cell(cell, board)
	)
	if removed:
		cell_gate_changed.emit(cell)
	return removed


func has_buff(host: PipelineBuffHost, buff_id: StringName) -> bool:
	return host != null and _find_instance(host.get_stats(), buff_id) != null


func get_stacks(host: PipelineBuffHost, buff_id: StringName) -> int:
	if host == null:
		return 0
	var instance := _find_instance(host.get_stats(), buff_id)
	return instance.stacks if instance != null else 0


func get_remaining_duration(host: PipelineBuffHost, buff_id: StringName) -> int:
	if host == null:
		return 0
	var instance := _find_instance(host.get_stats(), buff_id)
	return instance.remaining_duration if instance != null else 0


func tick_host(host: PipelineBuffHost, phase: PipelineBuffData.DurationPhase) -> void:
	if host == null:
		return
	var stats := host.get_stats()
	if stats == null:
		return
	var snapshot: Array[BuffInstance] = stats.buffs.duplicate()
	for instance in snapshot:
		if instance == null or not (instance.buff is PipelineBuffData):
			continue
		var buff := instance.buff as PipelineBuffData
		if buff.duration_phase != phase:
			continue
		var context := PipelineBuffContext.for_host(host)
		context.phase = phase
		buff.on_runtime_tick(context, instance.stacks, phase)
		_apply_periodic_values(host, buff, instance.stacks)
		buff_ticked.emit(host, buff, instance.stacks, phase)
		if instance.remaining_duration > 0:
			instance.remaining_duration -= 1
			if instance.remaining_duration <= 0:
				remove_buff(host, buff.id)


func tick_cell(
	cell: CellRuntime,
	phase: PipelineBuffData.DurationPhase,
	board: Node = null
) -> void:
	if cell == null:
		return
	var snapshot: Array[BuffInstance] = cell.stats.buffs.duplicate()
	for instance in snapshot:
		if instance == null or not (instance.buff is PipelineBuffData):
			continue
		var buff := instance.buff as PipelineBuffData
		if buff.duration_phase != phase:
			continue
		var context := PipelineBuffContext.for_cell(cell, board)
		context.phase = phase
		buff.on_runtime_tick(context, instance.stacks, phase)
		if instance.remaining_duration > 0:
			instance.remaining_duration -= 1
			if instance.remaining_duration <= 0:
				remove_buff_from_cell(cell, buff.id, board)


func resolve_damage_packet(packet: PipelineDamagePacket) -> PipelineDamagePacket:
	if packet == null:
		return null
	var value := float(packet.base_amount)
	value = _apply_outgoing_modifiers(value, packet.source_host)
	value = _apply_incoming_modifiers(value, packet.target_host)
	packet.final_amount = maxi(0, roundi(value))

	if packet.target_host != null and _would_be_lethal(packet.target_host, packet.final_amount):
		var death_saver := _first_lethal_guard(packet.target_host.get_stats())
		if death_saver != null:
			packet.final_amount = maxi(0, packet.target_host.current_hp() - 1)
			packet.metadata["prevented_lethal_by"] = death_saver.id
			if death_saver.consume_on_prevent_lethal:
				remove_buff(packet.target_host, death_saver.id)

	if packet.cancelled:
		packet.final_amount = 0
	damage_packet_resolved.emit(packet)
	return packet


func deal_damage(
	source_host: PipelineBuffHost,
	target_host: PipelineBuffHost,
	base_amount: int,
	tags: PackedStringArray = []
) -> PipelineDamagePacket:
	var packet := PipelineDamagePacket.new(source_host, target_host, base_amount, tags)
	resolve_damage_packet(packet)
	if target_host != null and packet.final_amount > 0:
		if not target_host.apply_damage(packet.final_amount):
			damage_requested.emit(target_host, packet.final_amount, null)
	return packet


func can_light_cell(cell: CellRuntime) -> bool:
	if cell == null:
		return false
	for instance in cell.stats.buffs:
		if instance == null or instance.buff == null:
			continue
		if instance.buff.id == "disabled":
			return false
		if instance.buff is PipelineBuffData:
			var buff := instance.buff as PipelineBuffData
			if buff.blocks_cell_lighting:
				return false
	return true


func extra_light_cost(cell: CellRuntime) -> int:
	if cell == null:
		return 0
	var result := 0
	for instance in cell.stats.buffs:
		if instance == null or instance.buff == null:
			continue
		if instance.buff.id == "dust" and not (instance.buff is PipelineBuffData):
			result += instance.stacks
		elif instance.buff is PipelineBuffData:
			var buff := instance.buff as PipelineBuffData
			result += buff.extra_light_cost_per_stack * instance.stacks
	return maxi(0, result)


func can_use_card(host: PipelineBuffHost, _card_runtime: RefCounted = null) -> bool:
	if host == null:
		return true
	var stats := host.get_stats()
	if stats == null:
		return true
	for instance in stats.buffs:
		if instance != null and instance.buff is PipelineBuffData:
			if (instance.buff as PipelineBuffData).blocks_card_use:
				return false
	return true


func should_skip_action(host: PipelineBuffHost, consume: bool = true) -> bool:
	if host == null:
		return false
	var stats := host.get_stats()
	if stats == null:
		return false
	for instance in stats.buffs:
		if instance != null and instance.buff is PipelineBuffData:
			var buff := instance.buff as PipelineBuffData
			if buff.skip_next_action:
				if consume:
					remove_buff(host, buff.id)
				return true
	return false


func cleanse_all_negative(host: PipelineBuffHost, board: Node = null) -> int:
	var removed := 0
	if host != null and host.get_stats() != null:
		removed += _cleanse_stats(host.get_stats(), PipelineBuffContext.for_host(host))
	if board != null:
		for cell in _board_cells(board):
			removed += _cleanse_stats(
				cell.stats,
				PipelineBuffContext.for_cell(cell, board)
			)
			cell_gate_changed.emit(cell)
	if host != null:
		cleansed.emit(host, removed)
	return removed


func reset_all_card_cooldowns(board: Node, host: PipelineBuffHost = null) -> int:
	if board == null:
		return 0
	var changed := 0
	for card in _runtime_cards(board):
		if int(card.get("cooldown_remaining")) != 0:
			card.set("cooldown_remaining", 0)
			changed += 1
			card_cooldown_changed.emit(card, 0)
	if host != null:
		remove_buff(host, &"card_jammed")
	_emit_board_changed(board)
	return changed


func change_all_card_cooldowns(board: Node, delta: int) -> int:
	if board == null or delta == 0:
		return 0
	var changed := 0
	for card in _runtime_cards(board):
		var remaining := maxi(0, int(card.get("cooldown_remaining")) + delta)
		card.set("cooldown_remaining", remaining)
		changed += 1
		card_cooldown_changed.emit(card, remaining)
	_emit_board_changed(board)
	return changed


func _apply_stack_rule(instance: BuffInstance, buff: PipelineBuffData, amount: int) -> void:
	match buff.stack_mode:
		PipelineBuffData.StackMode.ADD:
			instance.stacks = mini(buff.max_stacks, instance.stacks + amount)
			instance.remaining_duration = maxi(instance.remaining_duration, buff.duration)
		PipelineBuffData.StackMode.REFRESH:
			instance.stacks = mini(buff.max_stacks, maxi(instance.stacks, amount))
			instance.remaining_duration = buff.duration
		PipelineBuffData.StackMode.REPLACE:
			instance.stacks = mini(buff.max_stacks, amount)
			instance.remaining_duration = buff.duration


func _find_instance(stats: Stats, buff_id: StringName) -> BuffInstance:
	if stats == null:
		return null
	for instance in stats.buffs:
		if instance != null and instance.buff != null and instance.buff.id == String(buff_id):
			return instance
	return null


func _remove_from_stats(
	stats: Stats,
	buff_id: StringName,
	context: PipelineBuffContext
) -> bool:
	var instance := _find_instance(stats, buff_id)
	if instance == null:
		return false
	var stacks := instance.stacks
	instance.buff.on_remove(stats)
	if instance.buff is PipelineBuffData:
		(instance.buff as PipelineBuffData).on_runtime_remove(context, stacks)
	stats.buffs.erase(instance)
	return true


func _cleanse_stats(stats: Stats, context: PipelineBuffContext) -> int:
	var count := 0
	var snapshot: Array[BuffInstance] = stats.buffs.duplicate()
	for instance in snapshot:
		if instance == null or instance.buff == null:
			continue
		if instance.buff.polarity != Buff.BuffPolarity.NEGATIVE:
			continue
		if instance.buff is PipelineBuffData:
			if not (instance.buff as PipelineBuffData).removable_by_cleanse:
				continue
		if _remove_from_stats(stats, instance.buff.id, context):
			count += 1
	return count


func _apply_periodic_values(host: PipelineBuffHost, buff: PipelineBuffData, stacks: int) -> void:
	if buff.tick_damage_per_stack > 0:
		var amount := buff.tick_damage_per_stack * stacks
		if not host.apply_damage(amount):
			damage_requested.emit(host, amount, buff)
	if buff.tick_heal_per_stack > 0:
		var amount := buff.tick_heal_per_stack * stacks
		if not host.apply_heal(amount):
			heal_requested.emit(host, amount, buff)


func _request_shield(host: PipelineBuffHost, amount: int, buff: PipelineBuffData) -> void:
	if not host.apply_shield(amount):
		shield_requested.emit(host, amount, buff)


func _apply_outgoing_modifiers(value: float, host: PipelineBuffHost) -> float:
	if host == null or host.get_stats() == null:
		return value
	for instance in host.get_stats().buffs:
		if instance != null and instance.buff is PipelineBuffData:
			var buff := instance.buff as PipelineBuffData
			value += buff.outgoing_damage_flat_per_stack * instance.stacks
			value *= buff.outgoing_damage_multiplier
	return value


func _apply_incoming_modifiers(value: float, host: PipelineBuffHost) -> float:
	if host == null or host.get_stats() == null:
		return value
	for instance in host.get_stats().buffs:
		if instance != null and instance.buff is PipelineBuffData:
			var buff := instance.buff as PipelineBuffData
			value += buff.incoming_damage_flat_per_stack * instance.stacks
			value *= buff.incoming_damage_multiplier
	return value


func _would_be_lethal(host: PipelineBuffHost, damage: int) -> bool:
	var hp := host.current_hp()
	return hp > 0 and damage >= hp


func _first_lethal_guard(stats: Stats) -> PipelineBuffData:
	if stats == null:
		return null
	for instance in stats.buffs:
		if instance != null and instance.buff is PipelineBuffData:
			var buff := instance.buff as PipelineBuffData
			if buff.prevents_lethal_damage:
				return buff
	return null


func _runtime_cards(board: Node) -> Array:
	var value: Variant = board.get("runtime_cards")
	return value as Array if value is Array else []


func _board_cells(board: Node) -> Array[CellRuntime]:
	var result: Array[CellRuntime] = []
	var columns := int(board.get("columns"))
	var rows := int(board.get("rows"))
	if not board.has_method("GetCell"):
		return result
	for y in range(rows):
		for x in range(columns):
			var value: Variant = board.call("GetCell", Vector2i(x, y))
			if value is CellRuntime:
				result.append(value as CellRuntime)
	return result


func _emit_board_changed(board: Node) -> void:
	if board.has_signal("BoardChanged"):
		board.emit_signal("BoardChanged")
