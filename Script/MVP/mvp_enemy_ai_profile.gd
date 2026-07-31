class_name MvpEnemyAiProfile
extends Resource

@export_group("身份")
@export var ai_id: StringName = &"chaser"
@export var display_name: String = "追击者"
@export_multiline var debug_description: String = "进入攻击距离后攻击，否则接近玩家。"
@export var color: Color = Color("d85c68")

@export_group("基础数值")
@export_range(1, 999, 1) var max_hp: int = 24
@export_range(0, 99, 1) var base_damage: int = 5
@export_range(0, 99, 1) var starting_shield: int = 0

@export_group("AI 逻辑")
@export_enum("chaser", "sniper", "poisoner", "skirmisher", "guardian") var behavior: String = "chaser"
@export_range(0, 20, 1) var attack_min_range: int = 0
@export_range(0, 20, 1) var attack_max_range: int = 2
@export_range(0, 20, 1) var preferred_min_range: int = 1
@export_range(0, 20, 1) var preferred_max_range: int = 2
@export_range(0, 9, 1) var move_amount: int = 1

@export_group("特殊行动")
@export_range(0, 99, 1) var poison_amount: int = 0
@export_range(0, 9, 1) var poison_turns: int = 0
@export_range(0, 99, 1) var shield_amount: int = 0
@export_range(1, 9, 1) var special_every_n_turns: int = 2

