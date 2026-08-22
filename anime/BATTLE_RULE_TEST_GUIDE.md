# BattleRules 动画战斗场景配置说明

## 0. 在正式战斗中直接使用

只需要把下面这个场景拖到正式战斗场景的根节点：

`res://anime/scenes/battle_animation_display.tscn`

保持位置 `(0, 0)`，无需连接信号或拖入玩家、敌人、BattleManager。封装场景会自动向上寻找运行时，只读取角色位置和已发生的战斗事件，不修改输入、战斗数值、AI、Viewport 或项目现有的 `1440×1080` 4:3 分辨率。

玩家动画规则已经配置为：

- 原素材朝向整体水平翻转。
- HP 或护盾下降播放 `hurt`。
- 攻击牌播放 `attack`。
- 非攻击牌播放 `buff`。

战斗镜头也包含在同一个封装场景中：

- 玩家位于地图中段时，轨道水平移动，使玩家保持在镜头中央。
- 玩家接近第 1 格或最后一格时，镜头在地图边缘停止。
- 8、9 格等短于可视窗口的地图会整体居中，不进行无意义滚动。
- 镜头只移动 `DistanceTrack`，不会移动状态栏、卡牌区、电视 UI 或按钮。
- 镜头只处理 X 轴，不改变项目现有 `1440×1080`、4:3 分辨率。

如果只需要镜头、不需要动画，也可以单独拖入：

`res://anime/scenes/battle_track_camera.tscn`

### 在 Inspector 调整朝向与镜头

选中拖入的 `BattleAnimationDisplay` 根节点：

- **Animation Facing → Enemies Always Face Player**：开启后，所有敌人动画始终面向玩家。
- **Player Initial Facing**：玩家初始显示朝向，可选 `Right` / `Left`。
- **Enemy Initial Facing**：敌人尚未找到玩家或双方同格前的初始显示朝向。
- **Horizontal Camera → Camera Enabled**：是否启用横向镜头。
- **Smooth Camera**：是否平滑跟随；默认关闭，以保证移动后玩家立即回到镜头中央。
- **Camera Follow Speed**：平滑跟随速度。

这些选项只改变动画精灵的 `FlipH` 和轨道显示偏移，不会回写
`PlayerBattle.Facing`、`EnemyBattle.Facing`、AI 或移动数据。

下面的 BattleRules 场景只是独立测试例，不是正式场景的必需依赖。

## 1. 直接运行现成示例

在 Godot 文件系统面板中进入：

`res://anime/scenes/battle_rule_presets/`

双击任意场景，再点击“运行当前场景”：

- `boom_animation_test.tscn`
- `rocky_boom_animation_test.tscn`
- `sharkk_animation_test.tscn`
- `core00_hands_animation_test.tscn`
- `core00_body_animation_test.tscn`

这些场景会直接进入战斗。可以点击相邻空地移动、点亮卡牌、出牌和结束回合；它们不会打开 Campaign 构筑页。

## 2. 用任意 BattleRules 配置一场战斗

1. 在 Godot 中打开 `res://anime/scenes/animation_battle_rule_test.tscn`。
2. 建议先“场景 → 另存为”，把副本仍保存在 `res://anime/` 内。
3. 选中根节点 `AnimationBattleRuleTest`。
4. 在 Inspector 展开 **Drag To Configure**。
5. 把目标 `.tres` 拖到 **Battle Rules**。
6. 运行当前场景。

仅这一项决定地图、敌人、HP、距离、规则和 AI。玩家与敌人动画机由 `BattleAnimationHub` 按 `EnemyId` / `Role` 自动匹配，不需要手工拖节点引用。

`Fallback Test Card` 只在存档里完全没有构筑时放入一张测试左轮，保证新场景仍能操作。它不改变已有存档构筑。

## 3. 节点结构

测试场景完整保留正式 BattleScene 的核心结构：

```text
AnimationBattleRuleTest
├─ BoardManager
├─ EffectResolver
├─ BattleManager
├─ BattleScreen          # 原场景实例，布局与输入不改
└─ BattleAnimationDisplay
   ├─ BattleAnimationHub # 只观察战斗并播放动画
   └─ BattleTrackCamera  # 只水平移动战斗轨道
```

运行时，每名角色会自动得到一台动画机。动画机根据 `MapPosition` 找到对应 `TrackSlot`，然后在该格子的 `Vbox` 下挂一个纯 Node2D 锚点，位置只由 `occupant` 的本地矩形计算。

## 4. 调整角色大小和位置

不要改 BattleScreen、TrackSlot 或窗口锚点。打开 `res://anime/profiles/` 中对应 Profile，只调：

- `target_visual_height`：角色显示高度。
- `visual_offset`：相对格子底部中心的偏移。
- `source_faces_right`：原素材默认是否朝右。
- `allow_horizontal_flip`：是否根据战斗朝向自动镜像。
- `move_tween_duration`：兼容旧 Profile 的保留字段；固定锚点模式不会让角色跨 UI 飞行。

因为坐标是 TrackSlot 局部坐标，修改窗口大小不会把角色甩到背包、构筑页或其他 UI 上。

## 5. 替换或补充动画

1. 将透明 PNG 序列放入 `res://anime_assets/frames/<角色>/<动作>/`。
2. 文件名使用递增数字，如 `0001.png`、`0002.png`。
3. 同一动作所有帧必须保持相同画布、角色原点和留白。
4. 如动作目录尚未登记，编辑 `res://anime/import_manifest.json`。
5. 运行 `res://anime/tools/build_spriteframes.gd`。
6. 确认对应 `res://anime/generated/*_frames.tres` 和 Profile 的 `sprite_frames` 引用。

动作 ID 与动画名的完整对照在 `res://anime/ACTION_MAPPING.md`。

当前 Rocky、Sharkk、Core 本体没有源动画帧，所以测试时保留原字形；这不是错误。它们的动画机和动作映射已经存在，补帧后会自动接管显示。

## 6. 点击移动检查

进入玩家回合后：

1. 点击玩家相邻且没有敌人的轨道格。
2. 玩家位置应变到该格。
3. 能量按 BattleRules 的移动费用扣除。
4. 玩家动画应随角色一起切换到新格，不覆盖或改变原格子的鼠标输入。

动画模块没有任何全屏 Control、CanvasLayer 或 Overlay，也不连接鼠标和键盘输入，因此不会拦截点击。

## 7. 常见问题

- **打开后仍是构筑页**：你运行的是旧 campaign 示例。请运行 `animation_battle_rule_test.tscn` 或 `battle_rule_presets/` 下的场景。
- **角色还是文字图标**：该 Profile 没有真实序列帧，模块正在安全回退。
- **动画太大或位置偏**：只调整 Profile 的 `target_visual_height` 和 `visual_offset`。
- **新敌人没有动画机**：把它的 `EnemyId` 或 `Role` 加到一个 anime Profile；不要改 BattleRules 或战斗脚本。
- **新动作只播待机**：在对应 Profile 的 `action_animations` 中补 Action ID 映射。
