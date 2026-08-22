# 正视地图选择 UI

这一目录是用户提供的“正视地图选关界面”拆分包的独立运行层。它只负责显示、悬浮提示和选择动效；旅行、ESC 退出、玩家移动锁、HUD 恢复、Gate 与 `MapManager` 仍由原有 `MapSelectUI` 负责。

## 当前交互

- 单击地图牌：只改变当前选择，不会立刻切换场景。
- 单击右下角箭头：调用原有确认逻辑，随后由 `MapManager.TravelTo(mapId, spawnId)` 出发。
- 单击右上角叉号或按 `Esc`：调用原有关闭逻辑。
- 悬浮地图牌：复用项目全局 `TooltipService` 显示标题、说明、类型、风险、奖励和状态；主面板不常驻显示重复文字。
- 打开时：面板淡入并轻微缩放，五张地图牌按顺序错峰出现。
- 悬浮/选中/按下：地图牌与右侧图片按钮有轻微缩放、状态换图和按压反馈。

## 默认路线映射

| 图片槽位 | 现有目的地 | 状态 |
|---|---|---|
| Boss | `f4 / f4entry` | 可选择 |
| Casino | 无 | 尚未开放，可悬浮 |
| Market | `f2_1 / f2_1left` | 可选择 |
| Pipe | `f3_0 / f3_0left` | 可选择 |
| Wasteland | 无 | 尚未开放，可悬浮 |

映射按 `MapId` 查找，不依赖 `default_home_destinations.tres` 中的数组顺序。以后开放赌场或荒地时，在 `FrontMapPresenter`/场景里给对应槽位填写真实 `DestinationMapId` 并关闭 `Locked`，同时把目的地资源加入原有配置即可。

## 4:3 画幅

拆分素材的设计画布保持 `1920x1080`，没有拉伸，也没有修改项目分辨率。运行在项目的 `1440x1080` 画幅时：

- `scale = viewportHeight / 1080 = 1`
- `x = (1440 - 1920) / 2 = -240`
- `y = 0`

左右只裁掉透明和外围区域，主体板与两个按钮仍完整显示。其他高度会按同一规则等比适配。

## 文件职责

- `assets/`：原拆分包的 15 张运行时 PNG。
- `scenes/front_view_visual_layer.tscn`：可独立实例化的纯视觉层。
- `scripts/FrontMapPresenter.cs`：把图片槽映射到原有配置，并绑定 Tooltip/打开动效。
- `scripts/FrontMapChoice.cs`：地图牌选中、悬浮、按压及锁定反馈。
- `scripts/PaintedImageButton.cs`：右侧确认/关闭按钮的图片状态和动效。
- `tests/front_map_ui_smoke.tscn`：映射、锁定、Tooltip、4:3、鼠标穿透和“选择不旅行”的自动检查。

所有 `TextureRect` 都是 `MouseFilter = Ignore`；输入只由其透明 `Button` 父节点接收。

## 自动检查

```powershell
Godot_v4.6.1-stable_mono_win64_console.exe --headless --audio-driver Dummy --path <项目目录> --scene res://features/exploration/map_selection/front_view_ui/tests/front_map_ui_smoke.tscn
Godot_v4.6.1-stable_mono_win64_console.exe --headless --audio-driver Dummy --path <项目目录> --scene res://features/exploration/map_selection/tests/map_selection_smoke.tscn
```
