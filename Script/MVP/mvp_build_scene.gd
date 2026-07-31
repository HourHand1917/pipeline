class_name MvpBuildScene
extends "res://Script/MVP/mvp_scene_base.gd"

const BoardModel = preload("res://Script/MVP/mvp_board_model.gd")

@export_group("构筑调试 - Inspector 可调")
@export var backdrop_texture: Texture2D
@export var allow_board_level_buttons: bool = true
@export var empty_cell_color: Color = Color("132c36")
@export var selected_cell_color: Color = Color("47d9f2")

var board: RefCounted
var selected_card_id: StringName = &""
var rotation_steps: int = 0
var grid: GridContainer
var inventory_box: VBoxContainer
var info_label: Label
var size_label: Label
var grid_buttons: Array[Button] = []

func _ready() -> void:
	set_full_rect(self)
	_build_ui()
	_reset_model()

func _build_ui() -> void:
	var background := ColorRect.new()
	set_full_rect(background)
	background.color = Color("05090e")
	add_child(background)
	add_screen_frame(self)
	if backdrop_texture != null:
		var texture := TextureRect.new()
		set_full_rect(texture)
		texture.texture = backdrop_texture
		texture.expand_mode = TextureRect.EXPAND_IGNORE_SIZE
		texture.stretch_mode = TextureRect.STRETCH_KEEP_ASPECT_CENTERED
		texture.modulate = Color(1, 1, 1, 0.22)
		add_child(texture)
	var main := HBoxContainer.new()
	main.position = Vector2(42, 34)
	main.size = Vector2(1480, 1010)
	main.add_theme_constant_override("separation", 22)
	add_child(main)
	var inventory_panel := make_panel()
	inventory_panel.custom_minimum_size.x = 310
	main.add_child(inventory_panel)
	var inventory_root := VBoxContainer.new()
	inventory_root.add_theme_constant_override("separation", 10)
	inventory_panel.add_child(inventory_root)
	inventory_root.add_child(make_title("背包卡牌", 26))
	var inventory_scroll := ScrollContainer.new()
	inventory_scroll.size_flags_vertical = Control.SIZE_EXPAND_FILL
	inventory_root.add_child(inventory_scroll)
	inventory_box = VBoxContainer.new()
	inventory_box.add_theme_constant_override("separation", 10)
	inventory_box.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	inventory_scroll.add_child(inventory_box)
	var center_panel := make_panel(Color(0.035, 0.08, 0.095, 0.95))
	center_panel.size_flags_horizontal = Control.SIZE_EXPAND_FILL
	main.add_child(center_panel)
	var center := VBoxContainer.new()
	center.alignment = BoxContainer.ALIGNMENT_CENTER
	center.add_theme_constant_override("separation", 14)
	center_panel.add_child(center)
	center.add_child(make_title("卡牌构筑", 40))
	size_label = Label.new()
	size_label.horizontal_alignment = HORIZONTAL_ALIGNMENT_CENTER
	size_label.add_theme_font_size_override("font_size", 20)
	center.add_child(size_label)
	grid = GridContainer.new()
	grid.custom_minimum_size = Vector2(630, 610)
	grid.add_theme_constant_override("h_separation", 8)
	grid.add_theme_constant_override("v_separation", 8)
	center.add_child(grid)
	var actions := HBoxContainer.new()
	actions.alignment = BoxContainer.ALIGNMENT_CENTER
	actions.add_theme_constant_override("separation", 10)
	center.add_child(actions)
	actions.add_child(make_button("旋转", _rotate, Vector2(110, 46)))
	actions.add_child(make_button("移除全部", _clear, Vector2(130, 46)))
	actions.add_child(make_button("恢复默认", _default_build, Vector2(130, 46)))
	actions.add_child(make_button("确认保存", _confirm, Vector2(140, 46)))
	actions.add_child(make_button("返回", _back, Vector2(110, 46)))
	var info_panel := make_panel()
	info_panel.custom_minimum_size.x = 310
	main.add_child(info_panel)
	var info_box := VBoxContainer.new()
	info_box.add_theme_constant_override("separation", 12)
	info_panel.add_child(info_box)
	info_box.add_child(make_title("构筑信息", 24))
	info_label = Label.new()
	info_label.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
	info_label.custom_minimum_size.y = 360
	info_box.add_child(info_label)
	if allow_board_level_buttons:
		for level in range(3):
			var chosen := level
			var sizes := [Vector2i(3, 2), Vector2i(3, 3), Vector2i(4, 3)]
			info_box.add_child(make_button("调试尺寸 %d×%d" % [sizes[level].x, sizes[level].y], func(): _set_level(chosen), Vector2(250, 42)))
		var note := Label.new()
		note.text = "右侧调试尺寸会直接修改局内升级等级；所有卡牌参数可在 Resource/MVP/cards 中由 Inspector 调整。"
		note.autowrap_mode = TextServer.AUTOWRAP_WORD_SMART
		info_box.add_child(note)

func _reset_model() -> void:
	var board_dimensions: Vector2i = run_state.board_size()
	board = BoardModel.new(board_dimensions.x, board_dimensions.y, card_catalog, run_state.saved_build)
	_rebuild_inventory()
	_rebuild_grid()
	_refresh()

func _rebuild_inventory() -> void:
	for child in inventory_box.get_children():
		child.queue_free()
	for card in card_catalog.cards:
		var count := int(run_state.card_counts.get(String(card.card_id), 0))
		var button := make_button("%s × %d\n%s" % [card.display_name, count, card.description], func(): _select_card(card.card_id), Vector2(280, 74))
		button.modulate = card.color
		inventory_box.add_child(button)

func _rebuild_grid() -> void:
	for child in grid.get_children():
		child.queue_free()
	grid_buttons.clear()
	grid.columns = board.width
	for index in range(board.width * board.height):
		var cell := Vector2i(index % board.width, index / board.width)
		var button := Button.new()
		button.custom_minimum_size = Vector2(145, 145)
		button.add_theme_font_size_override("font_size", 18)
		button.pressed.connect(func(): _cell_pressed(cell))
		grid.add_child(button)
		grid_buttons.append(button)

func _select_card(card_id: StringName) -> void:
	selected_card_id = card_id
	rotation_steps = 0
	_refresh()

func _rotate() -> void:
	rotation_steps = posmod(rotation_steps + 1, 4)
	_refresh()

func _cell_pressed(cell: Vector2i) -> void:
	if board.remove_at(cell):
		_refresh()
		return
	if selected_card_id == &"":
		info_label.text = "先从左侧选择一张卡。点击已占用格可以移除整张卡。"
		return
	if _remaining_count(selected_card_id) <= 0:
		info_label.text = "这张卡已经全部放入构筑。"
		return
	if not board.add_entry(selected_card_id, cell, rotation_steps):
		info_label.text = "无法放置：形状越界或与其他卡牌重叠。"
		return
	_refresh()

func _remaining_count(card_id: StringName) -> int:
	var placed := 0
	for entry in board.entries:
		if StringName(entry.get("card_id", "")) == card_id:
			placed += 1
	return int(run_state.card_counts.get(String(card_id), 0)) - placed

func _clear() -> void:
	board.entries.clear()
	_refresh()

func _default_build() -> void:
	var defaults := [
		{"card_id": "revolver", "anchor": [0, 0], "rotation": 0},
		{"card_id": "shield", "anchor": [2, 0], "rotation": 0},
		{"card_id": "battery", "anchor": [0, 1], "rotation": 0},
		{"card_id": "medkit", "anchor": [1, 1], "rotation": 0},
	]
	run_state.set_build(defaults)
	_reset_model()

func _confirm() -> void:
	if board.entries.is_empty():
		info_label.text = "至少放入一张卡牌才能保存。"
		return
	run_state.set_build(board.entries)
	run_state.save_game()
	info_label.text = "构筑已保存。进入战斗时会按当前布局重建。"

func _back() -> void:
	if String(scene_context.get("return_to", "map")) == "exploration":
		send_command("exploration", {"stage": int(scene_context.get("stage", run_state.map_progress))})
	else:
		send_command("map")

func _set_level(level: int) -> void:
	run_state.board_level = clampi(level, 0, 2)
	run_state.state_changed.emit()
	_reset_model()

func _refresh() -> void:
	var occupied: Dictionary = board.occupied_cells()
	for index in range(grid_buttons.size()):
		var cell := Vector2i(index % board.width, index / board.width)
		var button := grid_buttons[index]
		button.text = "%d,%d" % [cell.x, cell.y]
		button.modulate = empty_cell_color
		if occupied.has(cell):
			var entry_index := int(occupied[cell])
			var entry: Dictionary = board.entries[entry_index]
			var card: Resource = card_catalog.get_card(StringName(entry.get("card_id", "")))
			if card != null:
				button.text = card.display_name
				button.modulate = card.color
	var board_dimensions: Vector2i = run_state.board_size()
	size_label.text = "当前尺寸 %d×%d　已放置 %d 张" % [board_dimensions.x, board_dimensions.y, board.entries.size()]
	if selected_card_id != &"":
		var card: Resource = card_catalog.get_card(selected_card_id)
		info_label.text = "已选择：%s\n旋转：%d × 90°\n剩余可放：%d\n\n点击空格放置；点击已占用格移除整张卡。" % [
			card.display_name if card != null else String(selected_card_id),
			rotation_steps, _remaining_count(selected_card_id)
		]
	else:
		info_label.text = "从左侧选择卡牌，再点击构筑格放置。\n\n卡牌占格、旋转、效果和射程都来自可编辑 Resource。"
