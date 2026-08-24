# PIPELINE 战斗动画测试场景

本目录是一套独立、非侵入式的战斗动画模块。它只读取原战斗运行状态并播放画面，不修改生命、伤害、AI、卡牌、移动或回合数据，也不修改原战斗文件、UI 布局、锚点和鼠标过滤。真实动画显示时会临时替换该角色原本的文字字形。

生产场景只需要拖入这一个封装场景：

`res://anime/scenes/battle_animation_display.tscn`

把它放在战斗场景根节点下，位置保持 `(0, 0)` 即可。它会向上查找战斗运行节点，不需要设置 NodePath，不修改输入、分辨率、Viewport 或 4:3 画幅。

## 最快使用

1. 在目标战斗场景中实例化 `battle_animation_display.tscn`。
2. 将实例放在场景根节点下，位置设为 `(0, 0)`。
3. 直接运行；动画模块会自动识别玩家、敌人和各自所在格子。

如果需要单独测试 BattleRules，可打开 `animation_battle_rule_test.tscn`，在根节点的 **Drag To Configure → Battle Rules** 中拖入任意 `GameRules` 资源。

动画 Hub 已预装 7 个 Profile，不需要再拖玩家或敌人节点。它会根据 BattleRules 生成的 `EnemyId` / `Role` 自动选择：

- Rubber 玩家
- Boom
- Rocky
- Sharkk
- Core-00 True 手
- Core-00 False 手
- Core-00 本体

完整操作说明见 [BATTLE_RULE_TEST_GUIDE.md](BATTLE_RULE_TEST_GUIDE.md)。

## 已配好的五个示例

| 战斗 | 可直接运行的场景 |
|---|---|
| Boom | `res://anime/scenes/battle_rule_presets/boom_animation_test.tscn` |
| Rocky + Boom | `res://anime/scenes/battle_rule_presets/rocky_boom_animation_test.tscn` |
| Sharkk | `res://anime/scenes/battle_rule_presets/sharkk_animation_test.tscn` |
| Core-00 双手 | `res://anime/scenes/battle_rule_presets/core00_hands_animation_test.tscn` |
| Core-00 本体 | `res://anime/scenes/battle_rule_presets/core00_body_animation_test.tscn` |

## 动画如何定位

动画不再放在全屏 Overlay 上，也不修改任何 Control 的大小、锚点或鼠标过滤。

每名角色的动画节点挂在当前 `TrackSlot/Vbox` 的本地视觉层，并完全由 `occupant` 的本地矩形计算位置：

```text
角色运行节点
  → 查找 OccupantRef 相同的 TrackSlot
  → 在该格子的 Vbox 下创建 BattleAnimationAnchor
  → 位置 = occupant 本地位置 + 底部中心 + Profile 偏移
  → AnimatedSprite2D 只使用锚点局部坐标
```

因此窗口缩放、Container 重排和地图格数变化都不会把角色动画甩到其他 UI 上；Node2D/AnimatedSprite2D 也不会拦截地面点击。

## 当前真实素材

| 角色 | 已导入动画 | 帧数 | 缺帧时行为 |
|---|---|---:|---|
| Rubber | 待机、前进、后退；攻击/受伤/强化占位别名 | 源帧 53 / 27 / 23 | 攻击复用前进、受伤复用后退、强化复用加速待机 |
| Boom | 待机、攻击、受伤、死亡 | 47 / 41 / 30 / 30 | 前进使用待机画面配合换格 |
| Core 双手 | 入场、待机、受伤、治疗、弹指、重拳、死亡 | 532 总帧 | True/False 手分别匹配 Profile |
| Rocky | 待机、前进、后退、中/近攻击、防御、受伤、死亡 | 源帧 230，生成 297 | 行为 ID 全部映射 |
| Sharkk | 待机、前进、后退、攻击/冲刺、受伤/眩晕 | 源帧 111，生成 183 | 扬尘后撤串联“攻击→后退” |
| Core 本体 | 待机、前进、后退、攻击、受伤（语义别名覆盖全部动作） | 源帧 136，生成 305 | 负面效果用攻击，闪身/后撤串联“攻击→后退”，死亡暂复用受伤 |

模块已为所有角色配置完整动作 ID 映射和动画机。Core 二阶段本体的五套新源序列已全部导入；完整映射见 `res://anime/ACTION_MAPPING.md`。

## 添加序列帧

帧目录：

```text
res://anime_assets/frames/<角色>/<动作>/0001.png
res://anime_assets/frames/<角色>/<动作>/0002.png
```

要求：透明 PNG、数字递增文件名、同一动作所有帧使用完全相同的画布和角色原点。不要逐帧紧裁，否则播放会抖动。

需要新增动作时，先编辑 `res://anime/import_manifest.json`，再运行：

```powershell
& "D:\Godot\Pipeline2\tools\godot-4.6.1-mono\editor-20260813\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" `
  --headless --path "D:\Godot\Pipeline2\pipeline" `
  --script res://anime/tools/build_spriteframes.gd
```

当前生成结果（1260 张源 PNG；语义动画会复用对应源序列）：

```text
ANIME_BUILD_PASS actors=6 clips=48 frames=1671
```

## Profile 常用字段

| 字段 | 用途 |
|---|---|
| `profile_id` | Profile 唯一 ID |
| `is_player` | 是否匹配玩家 |
| `enemy_ids` / `roles` | 自动匹配 BattleRules 生成的敌人 |
| `sprite_frames` | 该角色的 SpriteFrames |
| `action_animations` | 敌人 Action ID / 玩家卡牌 ID 到动画名的映射 |
| `audio_cues` | 动画名、触发帧与音频资源组成的逐帧音效表 |
| `target_visual_height` | 战斗格内显示高度 |
| `visual_offset` | 相对 occupant 底部中心的局部偏移 |
| `source_faces_right` | 源图默认朝向 |
| `allow_horizontal_flip` | 是否随战斗 Facing 自动镜像 |
| `move_tween_duration` | 保留兼容字段；固定锚点模式不移动锚点 |

## 动画帧音效

每个音效是一个 `BattleAnimationAudioCue` 资源，位于 `res://anime/audio/`。
在 Inspector 中配置动画名、从 0 开始的触发帧、AudioStream、音量、音高和总线，
再拖入角色 Profile 的 `audio_cues` 即可。动画机在该序列帧出现时播放音效；同一段
动画同一个 Cue 只播放一次，连续受击时会先把动画归零再重播，因此不会漏掉受击声。
每个角色动画机持有且仅持有一个 `AudioStreamPlayer`，继续使用 Cue 配置的 `SFX`
总线。不同角色可以同时发声；同一角色切换/重播动画时会先停止旧音效，动作自然结束、
离开格子或动画机释放时也会立即停止并清除音频。因此较长的素材不会串到下一动作，
同一个角色的攻击、受击、死亡等音效也不会互相叠加。

Boom 当前配置：

| 动画 | Trigger Frame | 画面帧 | 音频 |
|---|---:|---:|---|
| `attack` | 12 | 第 13 帧 | `COM_Enemy_Attack.ogg` |
| `hurt` | 4 | 第 5 帧 | `COM_Enemy_Hit.ogg` |
| `death` | 5 | 第 6 帧 | `COM_Enemy_Death.ogg` |

配置了帧音效的敌人不再同时播放旧的即时音效，避免重音；尚未配置 Cue 的敌人仍走
原有即时音效作为兼容回退。

Rocky、Sharkk 与 Core-00 一阶段也已按 `D:/Godot/Pipeline2/music` 的现有素材配置：

- Rocky：待机/移动/后撤、远近攻击、防御、受伤、死亡。
- Sharkk：攻击、冲刺、受伤、眩晕与死亡；扬尘后撤按“攻击 → 后退”播放。
- Core-00 双手：左右入场、弹指、治疗响指、重拳、受伤、死亡。

Sharkk 原始序列默认朝左，因此 `sharkk.tres/source_faces_right=false`；动画机会继续根据
敌我逻辑格位置动态镜像，保证 Sharkk 始终面向玩家，不会修改战斗层的 Facing 数据。

## 探索背景与角色脚线

`BattleBackdropPresenter` 把进入战斗前由 `BattleDirector` 截取的探索背景显示在战斗上方
舞台。背景保持静止；长地图只滚动逻辑轨道，所以角色看起来在同一背景空间中移动。
舞台下边界同时作为角色脚线，`stageVbox` 使用底对齐；危险/预警文字是 TrackSlot 根下的
独立覆盖层，不参与角色 VBox 布局，显示时不会把玩家或敌人顶起来。背景舞台以下全部由
鼠标穿透的纯黑 `ColorRect` 填充，项目分辨率仍为 1440×1080（4:3）。

格子 VBox 底边默认上抬 `88 px`，贴住探索背景画面内的地面并避开下方电视 UI；该值可在
`BattleAnimationDisplay/TrackBottomClearancePixels` 中调整。独立层中的角色动画锚点会随格子坐标一起
上移，脚底继续使用 occupant 底线，因此玩家与敌人始终是“站”在场地上，而不是单独漂浮。
探索背景默认不透明度为 30%，可在 `BattleBackdropPresenter/BackdropOpacity` 中继续微调；
该透明度只作用于背景，不影响角色、格子文字和下方战斗 UI。

## 自动测试

```powershell
dotnet build Pipeline.sln --no-restore

& "D:\Godot\Pipeline2\tools\godot-4.6.1-mono\editor-20260813\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "D:\Godot\Pipeline2\pipeline" --script res://anime/tests/animation_resource_test.gd

& "D:\Godot\Pipeline2\tools\godot-4.6.1-mono\editor-20260813\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "D:\Godot\Pipeline2\pipeline" --script res://anime/tests/audio_lifecycle_test.gd

& "D:\Godot\Pipeline2\tools\godot-4.6.1-mono\editor-20260813\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "D:\Godot\Pipeline2\pipeline" --scene res://anime/tests/animation_battle_scenes_smoke.tscn

& "D:\Godot\Pipeline2\tools\godot-4.6.1-mono\editor-20260813\Godot_v4.6.1-stable_mono_win64\Godot_v4.6.1-stable_mono_win64_console.exe" --headless --path "D:\Godot\Pipeline2\pipeline" --scene res://anime/tests/animation_runtime_smoke.tscn
```

当前通过标记：

```text
ANIMATION_RESOURCE_TEST_PASS checks=621 source_frames=1260 generated_frames=1671 profiles=7
ANIMATION_BATTLE_SCENES_SMOKE_PASS checks=305 scenes=5 turns=5 completed=5
ANIMATION_RUNTIME_SMOKE_PASS checks=34 waves=5 profiles=7 independent_overlay=1
BATTLE_PRESENTATION_CONTRACT_PASS checks=24 backdrop=exploration_size lower=black ground=locked inspector=configurable
AUDIO_LIFECYCLE_TEST_PASS: actor-owned cues stop on replace, finish, detach and free
```

五场测试使用 `Viewport.PushInput` 向屏幕坐标发送真实鼠标移动、按下和松开，走 `TrackSlot.GuiInput → BattleScreen.MoveToCellRequested → BattleManager.TryMoveToCell`，再以玩家位置和能量变化作为硬断言；测试还检查格子全部装饰 Control 均为鼠标穿透，并检查每个玩家/敌人动画的格子局部锚点。

## 非侵入式限制

模块只读取位置和已发生的战斗信号：

- 玩家或敌人换格：动画显示到对应格子。
- 玩家 HP 或护盾下降：播放 `hurt`。
- 成功结算攻击牌：播放 `attack`。
- 成功结算非攻击牌：播放 `buff`。
- 玩家原始素材天然朝左，Profile 已设置 `source_faces_right = false`，所有玩家动画方向相对旧结果统一翻转，同时仍会随 Facing 正常镜像。

旧战斗逻辑没有可等待的 `ActionStarted / Impact / AnimationFinished` 接口，所以本模块不会为了动画延迟伤害结算，也不会修改原来的战斗节奏。
