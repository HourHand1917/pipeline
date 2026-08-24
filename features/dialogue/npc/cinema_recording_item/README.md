# 电影院录音对话物品

## 使用

1. 将 `scenes/cinema_recording_item.tscn` 拖入探索场景。
2. 选中子节点 `Sprite`，把 `Texture` 替换为物品图片。
3. 按图片大小调整 `Sprite/Scale` 与 `ClickZone/ClickShape/Size`。
4. 玩家进入 `DetectionRange` 后，鼠标悬停并左键点击即可播放完整录音。

该预制体已经配置好 Coral、Lichen、录音旁白、气泡锚点、闪烁、碰撞区和完整 Timeline，不需要修改 `project.godot`。

## 可调参数

- `DetectionRange`：交互距离。
- `ClickShape`：鼠标点击区域。
- 根节点 `DialogueBubbleOffset`：气泡相对物品的位置。
- 根节点 `PersistenceId`：复制多个实例时请设为不同值。

依赖项目现有的 Dialogic、`PackagedFriendlyNPC.cs` 和 `BlinkComponent.cs`。
