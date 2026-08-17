extends Resource
class_name Buff

enum BuffPolarity { POSITIVE, NEGATIVE }

@export_group("Buff Identity")
@export var id: String = ""
@export var buff_name: String = ""
@export var polarity: BuffPolarity
@export_multiline var description: String = ""

@export_group("Buff Behavior")
@export var duration: int = 1          # 持续回合，0 = 永久
@export var stackable: bool = true
@export var max_stacks: int = 99

@export_group("Buff Visuals")
@export var icon: Texture


# ============ 生命周期（子类重写） ============

func on_apply(stats: Stats, stacks: int) -> void:
	# Buff 被施加时调用
	pass

func on_remove(stats: Stats) -> void:
	# Buff 被移除时调用
	pass

func on_turn_start(stats: Stats, stacks: int) -> void:
	# 回合开始时调用（挂载对象回合）
	pass

func on_turn_end(stats: Stats, stacks: int) -> void:
	# 回合结束时调用
	pass

func on_before_light(cell: CellRuntime, stacks: int) -> int:
	# 点亮格子前调用，返回额外能量消耗
	return 0

func on_after_light(cell: CellRuntime, stacks: int) -> void:
	# 点亮格子后调用
	pass

func can_light(cell: CellRuntime) -> bool:
	# 返回 false 阻止点亮
	return true
