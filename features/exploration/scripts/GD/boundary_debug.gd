@tool
extends Node2D

var _manager = null

func _ready():
	_manager = get_parent()
	set_process(true)

func _process(_delta):
	if not Engine.is_editor_hint(): return
	if _manager == null: return
	queue_redraw()

func _draw():
	if _manager == null or not Engine.is_editor_hint(): return
	var red = Color(1, 0.3, 0.3, 0.5)
	draw_line(Vector2(_manager.PlayerLeft, -10000), Vector2(_manager.PlayerLeft, 10000), red, 10)
	draw_line(Vector2(_manager.PlayerRight, -10000), Vector2(_manager.PlayerRight, 10000), red, 10)
	var blue = Color(0.3, 0.5, 1, 0.4)
	draw_line(Vector2(_manager.CamLeft, -10000), Vector2(_manager.CamLeft, 10000), blue, 10)
	draw_line(Vector2(_manager.CamRight, -10000), Vector2(_manager.CamRight, 10000), blue, 10)
