extends Resource
class_name PlayerData

@export_group("Identity")
@export var id: StringName = &"player"
@export var display_name: String = "rubber"
@export var subtitle: String = "PLAYER"
@export var glyph: String = "旅"
@export var tint: Color = Color("#f4cf61")

@export_group("Combat")
@export_range(1, 999, 1, "or_greater") var max_hp: int = 20
@export_range(0, 999, 1, "or_greater") var initial_shield: int = 0