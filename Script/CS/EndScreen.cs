using Godot;

/// <summary>
/// 结束界面。显示结束画面，点击关闭按钮回到主菜单。
/// </summary>
[GlobalClass]
public partial class EndScreen : Control
{
    [Export] private Button _closeButton;
    
    [ExportGroup("音乐")]
    [Export] private AudioStream backgroundMusic;
    [Export] private float musicFadeDuration = 2.0f;

    private AudioManager _audioManager;

    public override void _Ready()
    {
        if (_closeButton != null)
            _closeButton.Pressed += OnClosePressed;

        // 获取 AudioManager 并播放背景音乐
        _audioManager = GetNodeOrNull<AudioManager>("/root/AudioManager");
        
        if (backgroundMusic != null && _audioManager != null)
        {
            _audioManager.PlayMusicWithFade(backgroundMusic, musicFadeDuration);
        }
    }

    private void OnClosePressed()
    {
        // 播放 UI 点击音效
        _audioManager?.PlayUiClick();
        
        SceneTransition.Instance.ChangeScene("res://Scenes/game_scene/main_menu.tscn");
    }
}