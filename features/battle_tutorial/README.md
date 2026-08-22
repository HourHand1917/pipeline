# Boom 战斗引导

正式入口：`res://Scenes/game_scene/boom_battle_scene.tscn`  
可单独运行的演示：`res://features/battle_tutorial/scenes/boom_battle_tutorial_demo.tscn`

引导严格使用原战斗控件，并按真实成功状态推进：

1. 把同一卡牌占用的格子全部点亮。
2. 再次点击已完全点亮的卡牌并成功使用。
3. 点击轨道空格并成功移动。
4. 点击电视上箭头，切换到敌人面板。
5. 点击 Boom 脚下，查看血量、Buff 和行动意图。
6. 点击原战斗界面的结束回合按钮，让敌人开始行动。

## 配置

把 `boom_battle_tutorial.tscn` 拖到战斗场景根节点即可。组件会自动寻找
`BattleManager`、`BattleScreen`、`BoardManager` 和运行时 Boom，不需要填写 NodePath。

场景的 `Configuration` 已指向：

`res://features/battle_tutorial/configs/boom_tutorial_default.tres`

在 Inspector 点击该资源即可由策划直接配置，无需修改代码：

- 是否启用、是否仅允许单只 Boom 战触发。
- 六个步骤各自的标题和说明文字。
- 聚光外扩、背景暗度、说明框宽高、间距和屏幕边距。
- 遮罩、描边、步骤标题、正文和说明框颜色。

如需不同关卡使用不同文案，复制这份 `.tres`，修改后拖回教程节点的
`Configuration` 即可。

四块输入遮罩只挡住目标外区域；聚光洞内仍是原来的卡牌按钮、TrackSlot
和 PlayerTV 按钮。动画镜头移动或窗口缩放时，目标框会逐帧重新贴合。
视觉描边可以按 `Target Padding` 外扩，但真实输入洞始终严格等于目标控件范围，
不会误放行相邻格子。教程会优先选择能量/护盾等稳定可执行卡；如需使用攻击卡，
还会验证当前射程且确保不会在教学中提前击杀 Boom。

## 动画音效

音效触发器位于 `res://anime/audio/`。每个 `BattleAnimationAudioCue` 配置：

- `Animation Name`：Profile 中的动画名。
- `Trigger Frame`：从 0 开始的触发帧。
- `Stream`：音频资源。
- `Volume Db / Pitch Scale / Bus`：播放参数。

把 Cue 拖入角色 `BattleAnimationProfile.audio_cues` 即可。Boom 已配置：攻击第
13 帧、受伤第 5 帧、死亡第 6 帧（Inspector 的 `Trigger Frame` 从 0 计数，
对应数值分别为 12、4、5）。拥有帧音效的角色会自动关闭旧的即时音效，
没有配置动画音效的敌人仍保留原有即时音效作为兼容回退。

## 自动验证

```powershell
Godot_v4.6.1-stable_mono_win64_console.exe --headless --path D:\Godot\Pipeline2\pipeline --scene res://features/battle_tutorial/tests/boom_battle_tutorial_smoke.tscn
```

测试通过 `Viewport.PushInput` 点击真实控件，完整走完六个步骤；通过标记：

```text
BOOM_BATTLE_TUTORIAL_SMOKE_PASS checks=19 steps=6
```
