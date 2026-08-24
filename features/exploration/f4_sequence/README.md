# F4 Core-00 两阶段流程

本目录封装 F4 Boss 的两阶段演出与战斗衔接。场景入口仍是：

`res://features/exploration/scenes/f4/f4.tscn`

## 当前流程

1. 玩家进入 `Core00Encounter` 的触发区域。
2. 播放已去除中文字幕的一阶段入场视频：
   `assets/core00_phase1_intro_no_subtitles.ogv`。
3. 播放通用战斗音乐，进入 Core-00 一阶段战斗。
4. 一阶段胜利并返回 F4 后，强制播放 `Code00Dialogue` 对话；对话期间音乐音量降至 `DialogueMusicVolume`。
5. 对话结束后恢复正常音乐音量，直接进入 Core-00 二阶段战斗。
6. 二阶段胜利后先写入原有击败持久化，再进入独立片尾场景。

## 二阶段入场动画

当前没有二阶段入场动画，因此 `PhaseTwoLeftAnimation` 与
`PhaseTwoRightAnimation` 保持为空，流程不会播放占位演出，也不会阻塞二阶段战斗。

以后拿到二阶段入场动画时：

1. 把动画加入 F4 场景中左右 `AnimationPlayer`。
2. 在 `Core00Encounter` Inspector 中填写
   `PhaseTwoLeftAnimation` / `PhaseTwoRightAnimation`。
3. 保持动画名为空即可随时禁用该演出。

## 战斗动画

二阶段本体战斗动画已由 `res://anime/profiles/core00_body.tres` 配置。
原始帧位于 `res://anime_assets/frames/core_body/`，包含待机、前进、后退、
攻击和受伤；狙击、枪托、脉冲与禁用共用攻击动作，闪身护盾使用“攻击后退”序列。

## 音乐

当前 F4 使用场景已经配置的通用战斗音乐。以后取得专用音乐时，只需把新的
`AudioStream` 拖到 `Core00Encounter/BattleMusic`，无需修改流程脚本。

## 验证

运行：

`res://features/exploration/f4_sequence/f4_sequence_contract_test.tscn`

通过标志：`F4_SEQUENCE_CONTRACT_TEST_PASS`。

## 二阶段结局

二阶段胜利后不会从战斗场景直接跳到片尾。战斗先返回 F4 的
`f4_core_return`，然后 `Core00Encounter` 强制播放 Inspector 中
`PhaseTwoDefeatTimeline` 配置的 `Code_00战后.dtl`。对话期间音乐降低到
`DialogueMusicVolume`，结束后恢复正常音量；只有对话完整结束后才打开片尾。

`PhaseTwoOutroPersistenceId` 独立记录该段对话已经完成，重新加载 F4 时不会重复播放。
如果 timeline 丢失或无法启动，流程会报错并阻止进入片尾，避免静默跳过剧情。

对话结束后的场景由 `Core00Encounter/PhaseTwoVictoryScenePath` 配置，默认是：

`res://features/exploration/f4_sequence/ending/f4_ending_screen.tscn`

片尾先黑屏滚动字幕，字幕内容、速度均可直接在 `F4EndingScreen` Inspector 修改；默认全部
滚完后等待 3 秒，再淡入 `海报模版.png` 制作的项目海报。海报中间团队文字、退出提示、
等待时间和淡入时间也都是 Inspector 字段。海报完全显示后，任意键、鼠标按钮或手柄按钮
都会退出游戏；这样不会误吃掉上一场战斗残留输入。

单独验证：

`res://features/exploration/f4_sequence/ending/f4_ending_screen_test.tscn`

通过标志：`F4_ENDING_SCREEN_TEST_PASS`。
