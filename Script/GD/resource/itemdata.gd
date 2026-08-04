extends Resource
class_name ItemData

@export_group("Identity")
@export var id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""
@export var icon: Texture2D

@export_group("Effects")
@export var effects: Array[CombatEffectData] = []

@export_group("Usage")
@export var consume_on_use: bool = true