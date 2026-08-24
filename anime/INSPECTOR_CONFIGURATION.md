# 战斗动画显示：Inspector 配置

生产入口仍然只有一个：

`res://anime/scenes/battle_animation_display.tscn`

把它拖到战斗场景根节点下，并保持实例自身位置为 `(0, 0)`。所有配置都在实例根节点 `BattleAnimationDisplay` 上完成；它只改变显示，不会写入生命、伤害、AI、卡牌、移动、朝向或输入。

## Battle Track Layout

- `Track Offset`：整体移动战斗格子轨道和角色锚点。X 控制左右，Y 控制上下。
- `Track Bottom Clearance Pixels`：轨道底边相对探索背景裁切底边的上抬量。默认 `88`，让 VBox 底部贴住画面中的地面并避开下方电视 UI；可在窗口继续微调。
- 格子只提供角色脚底坐标。实际动画统一显示在 `BattleScreen/BattleCombatantOverlay` 独立层，因此可以越出格子，不会被 VBox/格子裁切，也不会参与鼠标命中。
- 角色脚底仍跟随对应格子的地线；动画播放期间不会自行上下漂移。

## Actor Visual Scale

- `Player Scale Multiplier`：所有玩家动画的统一倍率。
- `Enemy Scale Multiplier`：所有敌人动画的统一倍率。
- 默认值都是 `1.0`。建议微调范围 `0.75–1.5`。

缩放只作用于动画 Sprite，不改变格子、碰撞、鼠标点击范围或战斗数值。

## Animation Facing

- `Enemies Always Face Player`：敌人根据玩家左右位置自动面向玩家。
- `Force Enemy Facing`：开启后忽略自动朝向，只使用下面的强制方向。
- `Forced Enemy Facing`：强制为 `Left` 或 `Right`。
- `Player Initial Facing` / `Enemy Initial Facing`：角色尚未移动时的初始显示方向。

优先级：`Force Enemy Facing` > `Enemies Always Face Player` > 敌人运行时朝向/初始朝向。

这些均为纯视觉朝向，不会改写 `PlayerBattle.Facing` 或 `EnemyBattle.Facing`。

## Horizontal Camera

- 原水平跟随配置保持不变，只移动轨道，不移动其他 UI。
- 项目画幅仍为 1440×1080（4:3），本模块不会修改分辨率。

## 验证

```powershell
dotnet build Pipeline.sln --no-restore

& "D:\Godot\Pipeline2\tools\godot-4.6.1-mono\editor-20260813\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" `
  --headless --path "D:\Godot\Pipeline2\pipeline" `
  --scene res://anime/tests/battle_presentation_contract_test.tscn
```

通过标志：

`BATTLE_PRESENTATION_CONTRACT_PASS ... inspector=configurable`
