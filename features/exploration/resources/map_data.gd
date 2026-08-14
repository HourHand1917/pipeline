class_name MapData
extends Resource

## 地图唯一 id，如 "home"、"forest"
@export var id: StringName = &""

## 显示名
@export var display_name: String = ""

## 地图场景路径
@export var scene_path: String = ""

## MapPanel 里显示的地图块贴图
@export var map_icon: Texture2D

## 在 MapPanel 网格里的位置
@export var grid_position: Vector2i = Vector2i.ZERO

## 进入此地图的默认生成点
@export var default_spawn_id: StringName = &""
