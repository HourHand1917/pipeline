extends RefCounted
class_name Stats

## Runtime Buff container shared by characters and board cells.
##
## The user-provided Buff base resource remains unchanged.  Concrete Buff
## scripts opt into phase decay through get_decay_phase() and may expose
## mounted CombatEffectData arrays for EffectResolver.

var buffs: Array[BuffInstance] = []
var _pending_applications: Array[Dictionary] = []


func add_buff(buff_resource: Buff, stacks: int = 1) -> void:
	if buff_resource == null or stacks <= 0:
		return

	var existing := _find_instance(buff_resource.id)
	if existing != null:
		if buff_resource.stackable:
			existing.add_stacks(stacks)
		else:
			existing.stacks = mini(maxi(1, stacks), buff_resource.max_stacks)
		existing.remaining_duration = buff_resource.duration
		buff_resource.on_apply(self, existing.stacks)
		if buffs.has(existing):
			_queue_application(buff_resource, existing.stacks)
		return

	var instance := BuffInstance.new(buff_resource, stacks)
	buffs.append(instance)
	buff_resource.on_apply(self, instance.stacks)
	if buffs.has(instance):
		_queue_application(buff_resource, instance.stacks)


func remove_buff(buff_id: String) -> void:
	for index in range(buffs.size() - 1, -1, -1):
		var instance := buffs[index]
		if instance == null or instance.buff == null or instance.buff.id != buff_id:
			continue
		instance.buff.on_remove(self)
		buffs.remove_at(index)


func has_buff(buff_id: String) -> bool:
	return _find_instance(buff_id) != null


func get_buff_stacks(buff_id: String) -> int:
	var instance := _find_instance(buff_id)
	return instance.stacks if instance != null else 0


func get_buff_duration(buff_id: String) -> int:
	var instance := _find_instance(buff_id)
	return instance.remaining_duration if instance != null else 0


func get_buff_instances() -> Array[BuffInstance]:
	return buffs.duplicate()


func drain_pending_applications() -> Array[Dictionary]:
	var result: Array[Dictionary] = _pending_applications.duplicate(true)
	_pending_applications.clear()
	return result


func tick_turn_start() -> void:
	_tick_phase(&"turn_start")


func tick_turn_end() -> void:
	_tick_phase(&"turn_end")


func can_light(cell: CellRuntime) -> bool:
	for instance in buffs.duplicate():
		if instance != null and instance.buff != null:
			if not instance.buff.can_light(cell):
				return false
	return true


func before_light(cell: CellRuntime) -> int:
	var extra_cost := 0
	for instance in buffs.duplicate():
		if instance != null and instance.buff != null:
			extra_cost += maxi(0, instance.buff.on_before_light(cell, instance.stacks))
	return extra_cost


func after_light(cell: CellRuntime) -> void:
	for instance in buffs.duplicate():
		if instance != null and instance.buff != null:
			instance.buff.on_after_light(cell, instance.stacks)


func clear_buffs() -> void:
	var snapshot: Array[BuffInstance] = buffs.duplicate()
	for instance in snapshot:
		if instance != null and instance.buff != null:
			instance.buff.on_remove(self)
	buffs.clear()
	_pending_applications.clear()


func clear_negative_buffs() -> int:
	var removed := 0
	var snapshot: Array[BuffInstance] = buffs.duplicate()
	for instance in snapshot:
		if instance == null or instance.buff == null:
			continue
		if instance.buff.polarity != Buff.BuffPolarity.NEGATIVE:
			continue
		remove_buff(instance.buff.id)
		removed += 1
	return removed


func has_negative_buff() -> bool:
	for instance in buffs:
		if instance != null and instance.buff != null:
			if instance.buff.polarity == Buff.BuffPolarity.NEGATIVE:
				return true
	return false


func _tick_phase(phase: StringName) -> void:
	var snapshot: Array[BuffInstance] = buffs.duplicate()
	for instance in snapshot:
		if instance == null or instance.buff == null or not buffs.has(instance):
			continue

		if phase == &"turn_start":
			instance.buff.on_turn_start(self, instance.stacks)
		else:
			instance.buff.on_turn_end(self, instance.stacks)
		if not buffs.has(instance):
			continue

		var decay_phase := StringName()
		if instance.buff.has_method("get_decay_phase"):
			decay_phase = instance.buff.call("get_decay_phase") as StringName
		if decay_phase != phase:
			continue

		var decay_stacks := false
		if instance.buff.has_method("decay_uses_stacks"):
			decay_stacks = bool(instance.buff.call("decay_uses_stacks"))
		if decay_stacks:
			instance.stacks = maxi(0, instance.stacks - 1)
			if instance.stacks <= 0:
				remove_buff(instance.buff.id)
		elif instance.remaining_duration > 0:
			instance.remaining_duration -= 1
			if instance.remaining_duration <= 0:
				remove_buff(instance.buff.id)


func _find_instance(buff_id: String) -> BuffInstance:
	for instance in buffs:
		if instance != null and instance.buff != null and instance.buff.id == buff_id:
			return instance
	return null


func _queue_application(buff_resource: Buff, stacks: int) -> void:
	_pending_applications.append({
		"buff": buff_resource,
		"stacks": stacks,
	})
