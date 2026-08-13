extends Resource
class_name DangerAreaProfile

enum Shape {
	NONE,
	FORWARD_RANGE,
	FIXED_PARITY,
	CHARGE_PUSH_PATH,
	DESTINATION,
	GLOBAL_WARNING,
}

@export_group("Shape")
@export var shape: Shape = Shape.NONE
@export_range(0, 99, 1, "or_greater") var min_range: int = 0
@export_range(0, 99, 1, "or_greater") var max_range: int = 0
@export_range(0, 1, 1) var parity: int = 0
@export_range(0, 99, 1, "or_greater") var movement_amount: int = 0
@export var movement_toward_player: bool = true

@export_group("Telegraph")
## 0 = this enemy turn; 1 = a prepared attack on the following turn.
@export_range(0, 9, 1, "or_greater") var telegraph_turn_offset: int = 0
@export_range(0, 9, 1, "or_greater") var severity: int = 1
@export var color: Color = Color(0.95, 0.22, 0.16, 0.72)
@export_multiline var global_warning: String = ""

