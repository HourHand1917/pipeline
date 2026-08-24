# 全局暂停界面

`pause_menu.tscn` 已注册为 Autoload。除主菜单、制作人员名单、旧结束界面和
Core-00 片尾界面外，不需要逐个修改场景：游戏中按 `Esc` 即可打开，第二次按
`Esc` 或点击右上角叉号关闭。

界面沿用提供的分层原画，开场按预览动画从左下沿弧线进入并回弹。原来的两条
音量设置已改为三条真实可交互控制：

- 环境声：`Ambient` 音频总线
- 音乐：`Music` 音频总线
- 音效：`SFX` 音频总线

滑杆支持鼠标点击/拖拽以及键盘方向键。设置保存在
`user://pause_audio_settings.cfg`。最下方按钮解除暂停并返回主菜单。

环境声播放器现在使用独立的 `Ambient` 总线；新增环境声时仍调用
`AudioManager.PlayAmbientWithFade()`，无需手动选择总线。

自动化验证场景：

`res://features/pause_ui/tests/pause_menu_smoke.tscn`
