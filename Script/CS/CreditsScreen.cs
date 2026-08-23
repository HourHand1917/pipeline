using Godot;

/// <summary>
/// 制作人员名单界面。显示制作团队，点击关闭按钮回到主菜单。
/// </summary>
[GlobalClass]
public partial class CreditsScreen : Control
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
