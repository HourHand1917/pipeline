class_name MvpThemeFactory
extends RefCounted

const TEXT_PRIMARY := Color("e9f8ff")
const TEXT_MUTED := Color("91aebc")
const CYAN := Color("55e6ff")
const CYAN_BRIGHT := Color("8ef2ff")
const PANEL := Color("0c1822")
const PANEL_LIGHT := Color("122632")
const BORDER := Color("2a7184")

static func create_theme() -> Theme:
	var theme := Theme.new()
	theme.default_font_size = 18

	theme.set_color("font_color", "Label", TEXT_PRIMARY)
	theme.set_color("font_shadow_color", "Label", Color(0, 0, 0, 0.72))
	theme.set_constant("shadow_offset_x", "Label", 1)
	theme.set_constant("shadow_offset_y", "Label", 2)
	theme.set_color("font_color", "RichTextLabel", TEXT_PRIMARY)
	theme.set_color("default_color", "RichTextLabel", TEXT_PRIMARY)

	theme.set_font_size("font_size", "Button", 18)
	theme.set_color("font_color", "Button", TEXT_PRIMARY)
	theme.set_color("font_hover_color", "Button", Color.WHITE)
	theme.set_color("font_pressed_color", "Button", CYAN_BRIGHT)
	theme.set_color("font_disabled_color", "Button", Color(0.42, 0.5, 0.54, 0.72))
	theme.set_stylebox("normal", "Button", _button_style(Color("132a36"), Color("2a6d80"), 1))
	theme.set_stylebox("hover", "Button", _button_style(Color("194050"), CYAN, 2))
	theme.set_stylebox("pressed", "Button", _button_style(Color("0b202b"), CYAN_BRIGHT, 2))
	theme.set_stylebox("focus", "Button", _button_style(Color(0, 0, 0, 0), CYAN_BRIGHT, 2))
	theme.set_stylebox("disabled", "Button", _button_style(Color("111920"), Color("293740"), 1))

	theme.set_font_size("font_size", "OptionButton", 18)
	theme.set_color("font_color", "OptionButton", TEXT_PRIMARY)
	theme.set_stylebox("normal", "OptionButton", _button_style(Color("132a36"), BORDER, 1))
	theme.set_stylebox("hover", "OptionButton", _button_style(Color("194050"), CYAN, 2))
	theme.set_stylebox("pressed", "OptionButton", _button_style(Color("0b202b"), CYAN_BRIGHT, 2))
	theme.set_stylebox("focus", "OptionButton", _button_style(Color(0, 0, 0, 0), CYAN_BRIGHT, 2))

	theme.set_stylebox("panel", "Panel", _panel_style(PANEL, BORDER, 14, 1))
	theme.set_stylebox("panel", "PanelContainer", _panel_style(PANEL, BORDER, 14, 1))
	theme.set_stylebox("normal", "LineEdit", _button_style(Color("0d1c25"), BORDER, 1))
	theme.set_color("font_color", "LineEdit", TEXT_PRIMARY)
	theme.set_color("caret_color", "LineEdit", CYAN)
	theme.set_color("font_uneditable_color", "LineEdit", TEXT_MUTED)

	theme.set_stylebox("normal", "HSeparator", _separator_style())
	theme.set_stylebox("normal", "VSeparator", _separator_style())
	theme.set_color("font_color", "TooltipLabel", TEXT_PRIMARY)
	theme.set_stylebox("panel", "TooltipPanel", _panel_style(Color("07131b"), CYAN, 8, 1))
	return theme

static func panel_style(
	color: Color = PANEL,
	border_color: Color = BORDER,
	radius: int = 14,
	border_width: int = 1,
	padding: int = 22
) -> StyleBoxFlat:
	var style := _panel_style(color, border_color, radius, border_width)
	style.content_margin_left = padding
	style.content_margin_right = padding
	style.content_margin_top = padding
	style.content_margin_bottom = padding
	return style

static func _button_style(color: Color, border_color: Color, border_width: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.border_color = border_color
	style.set_border_width_all(border_width)
	style.set_corner_radius_all(9)
	style.content_margin_left = 18
	style.content_margin_right = 18
	style.content_margin_top = 11
	style.content_margin_bottom = 11
	style.shadow_color = Color(0, 0, 0, 0.38)
	style.shadow_size = 5
	style.shadow_offset = Vector2(0, 3)
	return style

static func _panel_style(color: Color, border_color: Color, radius: int, border_width: int) -> StyleBoxFlat:
	var style := StyleBoxFlat.new()
	style.bg_color = color
	style.border_color = border_color
	style.set_border_width_all(border_width)
	style.set_corner_radius_all(radius)
	style.shadow_color = Color(0, 0, 0, 0.58)
	style.shadow_size = 12
	style.shadow_offset = Vector2(0, 6)
	return style

static func _separator_style() -> StyleBoxLine:
	var style := StyleBoxLine.new()
	style.color = Color(0.22, 0.72, 0.82, 0.5)
	style.thickness = 1
	return style
