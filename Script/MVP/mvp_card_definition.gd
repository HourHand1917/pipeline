class_name MvpCardDefinition
extends Resource

@export_group("基础信息")
@export var card_id: StringName = &"card"
@export var display_name: String = "卡牌"
@export_multiline var description: String = ""
@export var color: Color = Color("46c8e8")

@export_group("构筑形状")
@export var shape: Array[Vector2i] = [Vector2i.ZERO]

@export_group("战斗效果")
@export_enum("damage", "shield", "energy", "heal", "knockback", "teleport_through", "advance_attack", "poison", "cleanse", "strength") var effect_type: String = "damage"
@export var power: int = 1
@export_range(0, 9, 1) var move_amount: int = 0
@export_range(0, 99, 1) var status_amount: int = 0
@export_range(0, 9, 1) var status_turns: int = 0
@export var min_range: int = 0
@export var max_range: int = 99
@export_range(0, 9, 1) var cooldown_turns: int = 0

