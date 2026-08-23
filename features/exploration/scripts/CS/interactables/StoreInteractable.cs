using Godot;

/// <summary>
/// 商店交互物。玩家等级达标（RequiredLevel）后「开门」，可点击打开 ShopUI。
/// 关门时：显示关门 Sprite、闪烁禁用、不可互动；开门时：显示开门 Sprite、闪烁启用。
/// </summary>
[GlobalClass]
public partial class StoreInteractable : InteractableBase
{
    [Export] public ShopUI ShopUI { get; set; }
    [Export] public ExplorationHUD Hud { get; set; }
    /// <summary>开门时显示的 Sprite（商店（开）.png）。</summary>
    [Export] public Node2D OpenSprite { get; set; }
    /// <summary>开店所需玩家等级。</summary>
    [Export] public int RequiredLevel { get; set; } = 2;

    private bool _isOpen;

    public override void _Ready()
    {
        base._Ready();

        if (ShopUI != null)
            ShopUI.Closed += () => Hud?.ShowHUD();

        DataManager.Instance.LevelChanged += OnLevelChanged;
        UpdateStoreState();
    }

    public override void _ExitTree()
    {
        DataManager.Instance.LevelChanged -= OnLevelChanged;
        base._ExitTree();
    }

    private void OnLevelChanged(int newLevel) => UpdateStoreState();

    private void UpdateStoreState()
    {
        _isOpen = DataManager.Instance.Lv >= RequiredLevel;

        // 两个 Sprite 切换显隐（关闭 Sprite 即 InteractableBase 的 sprite）
        if (OpenSprite != null)
            OpenSprite.Visible = _isOpen;
        if (sprite != null)
            sprite.Visible = !_isOpen;

        SetBlinkEnabled(_isOpen);
    }

    public override void HandleInteract()
    {
        if (!_isOpen) return; // 关门时不可互动
        Hud?.HideHUD();
        ShopUI?.Open();
    }
}
