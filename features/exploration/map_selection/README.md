# Home 大门地图选择

这是一个独立、Inspector 可配置的「家中捷径」模块。点击 `home` 场景的大门后，显示一组已知目的地；它不是路线树。玩家先选择一项，再按「确认出发」才会调用现有 `MapManager.TravelTo(mapId, spawnId)`。

## 策划配置

1. 在 `resources/destinations/` 复制一份目的地 `.tres`。
2. 在 Inspector 中填写：
   - `Title` / `Description`：界面文字。
   - `Category`：探索、补给、事件、挑战或首领。
   - `Reward Hint` / `Risk`：奖励提示和风险。
   - `Map Id`：必须存在于 `res://features/exploration/resources/map_registry.tres`。
   - `Spawn Id`：必须是目标场景内某个 `SpawnPoint.SpawnId`。
   - `Unlocked`：关闭后该目的地仍显示，但不能确认。
   - `Locked Hint`：锁定原因。
3. 打开 `resources/default_home_destinations.tres`，把新资源拖到 `Destinations` 数组。
4. 不需要修改 `GateInteractable`、`MapManager` 或 `home` 的其他节点。

默认已经配置：

- F2：`f2_1 / f2_1left`
- F3：`f3_0 / f3_0left`
- F4：`f4 / f4entry`

## 运行行为

- 打开时锁定玩家移动，并用全屏 Modal 截断后方鼠标输入。
- 目的地资源会在显示前验证地图注册与出生点；配置错误时显示具体原因并禁用确认。
- 选择目的地不会立刻切图，必须再次确认。
- `Esc` 或右上角关闭按钮会恢复玩家移动，并通过原有 `Closed` 信号让 `GateInteractable` 恢复 HUD。
- 确认后立即防双击；转场期间移动锁交给现有 `MapManager`。

## 美术与画幅

底图位于 `art/home_destination_selector.png`，为项目原创的厚涂、大色块、低细节版本；文件已机械缩放到项目原生 `1440×1080`，保持 4:3，不修改 `project.godot` 分辨率。

## API 兼容

`MapSelectUI.Open()`、`MapSelectUI.Close()` 与 `MapSelectUI.Closed` 保持不变。`home.tscn` 仍然把 `GateInteractable.MapSelectUI` 指向 `UILayer/MapSelectUI`。

## 自动检查

运行：

```powershell
Godot_v4.6.1-stable_mono_win64_console.exe --headless --audio-driver Dummy --path <项目目录> --scene res://features/exploration/map_selection/tests/map_selection_smoke.tscn
```

测试会检查默认三个目的地、每个目标地图/出生点、独立 UI 场景、Modal 鼠标拦截，以及 `Open / Close / Closed` 生命周期。
