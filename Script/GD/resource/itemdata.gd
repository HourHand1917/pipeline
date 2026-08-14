extends Resource
class_name ItemData

enum SpecialEffect {
	NONE,
	RESET_ALL_CARD_COOLDOWNS,
	CLEANSE_ALL_DEBUFFS,
	ADD_STRENGTH_THIS_TURN,
	FREE_CROSS_MOVE,
	RESET_SELECTED_CARD_COOLDOWN,
	CANCEL_LOCKED_INTENT,
}

@export_group("Identity")
@export var id: StringName = &""
@export var display_name: String = ""
@export_multiline var description: String = ""
@export var icon: Texture2D

@export_group("Effects")
@export var effects: Array[CombatEffectData] = []

@export_group("Special Effects")
@export var special_effects: Array[SpecialEffect] = []
@export var special_amount: int = 0
@export_multiline var runtime_contract: String = ""

@export_group("Usage")
@export var consume_on_use: bool = true
@export_range(0, 999, 1, "or_greater") var shop_price: int = 0
@export_range(0, 99, 1, "or_greater") var mvp_stock: int = 1
