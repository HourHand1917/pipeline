using Godot;

/// <summary>
/// 结束界面。显示结束画面，点击关闭按钮回到主菜单。
/// </summary>
[GlobalClass]
public partial class EndScreen : Control
{
    [Export] private Button _closeButton;

    public override void _Ready()
    {
        if (_closeButton != null)
            _closeButton.Pressed += OnClosePressed;
    }

    private void OnClosePressed()
    {
        SceneTransition.Instance.ChangeScene("res://Scenes/game_scene/main_menu.tscn");
    }
}
