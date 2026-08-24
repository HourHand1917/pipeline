using Godot;

/// <summary>
/// 回血机器：靠近闪烁，点击互动后把玩家血量回满，并弹出「血量已回满！」提示淡出。
/// 可重复使用，不持久化。
/// </summary>
[GlobalClass]
public partial class HealMachineInteractable : InteractableBase
{
    private const string HealMessage = "血量已回满！";

    private CanvasLayer _toastLayer;

    public override void HandleInteract()
    {
        DataManager.Instance?.FullHeal();
        ShowToast(HealMessage);
    }

    /// <summary>在屏幕上方弹出一段提示文字，停留片刻后淡出并自毁。</summary>
    private void ShowToast(string text)
    {
        // 清掉上一条还没消失的提示，避免连续点击叠加
        if (_toastLayer != null && GodotObject.IsInstanceValid(_toastLayer))
            _toastLayer.QueueFree();

        var layer = new CanvasLayer { Layer = 100 };
        _toastLayer = layer;
        AddChild(layer);

        var label = new Label
        {
            Text = text,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };
        label.AddThemeFontSizeOverride("font_size", 42);
        label.AddThemeColorOverride("font_color", new Color(1f, 1f, 1f));

        layer.AddChild(label);

        var viewport = GetViewport().GetVisibleRect().Size;
        var size = new Vector2(360f, 88f);
        label.Position = new Vector2((viewport.X - size.X) * 0.5f, viewport.Y * 0.18f);
        label.Size = size;

        // 停留 → 淡出 → 自毁
        var tween = CreateTween();
        tween.TweenInterval(0.8f);
        tween.TweenProperty(label, "modulate:a", 0f, 0.6f);
        tween.TweenCallback(Callable.From(() =>
        {
            if (GodotObject.IsInstanceValid(layer))
                layer.QueueFree();
        }));
    }
}
