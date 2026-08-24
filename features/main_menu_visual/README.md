# 主菜单动态表现层

入口场景：`res://features/main_menu_visual/scenes/main_menu_visual.tscn`

- `Base`：底图开场循环。
- `Rubber`：Rubber 工作场景循环，开场后淡入。
- `Desktop`：透明桌面前景循环。
- 所有序列均为 1440×1080；场景本身不修改项目分辨率。
- 该模块不处理按钮、存档、读档、制作人员或退出逻辑，这些继续由原 `MainMenu.cs` 负责。

可在 Inspector 调整三个目录、三组 FPS、开场停留时间和交叉淡入时间。
