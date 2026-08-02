using Godot;

/// <summary>
/// 闪烁组件。挂到 InteractableBase 子节点即可获得范围闪烁 + 悬停高亮行为。
/// 不挂 = 不闪烁。遵循 Composition over Inheritance。
/// </summary>
[GlobalClass]
public partial class BlinkComponent : Node
{
    private Sprite2D sprite;
    private Color baseColor;
    private bool isHovered;
    private bool isPlayerInRange;
    private Tween blinkTween;

    public bool IsPlayerInRange
    {
        get => isPlayerInRange;
        set
        {
            if (isPlayerInRange == value) return;
            isPlayerInRange = value;
            if (isPlayerInRange) StartBlinking();
            else StopBlinking();
        }
    }

    public void Setup(Sprite2D targetSprite, Area2D clickZone)
    {
        sprite = targetSprite;
        baseColor = sprite.Modulate;
        clickZone.MouseEntered += () => { isHovered = true; OnHoverChanged(); };
        clickZone.MouseExited += () => { isHovered = false; OnHoverChanged(); };
    }

    private void OnHoverChanged()
    {
        if (isHovered && isPlayerInRange)
        {
            blinkTween?.Kill();
            sprite.Modulate = new Color(baseColor.R * 1.5f, baseColor.G * 1.5f, baseColor.B * 1.5f, 1.0f);
        }
        else if (!isHovered && isPlayerInRange)
        {
            StartBlinking();
        }
    }

    private void StartBlinking()
    {
        blinkTween?.Kill();
        blinkTween = CreateTween();
        blinkTween.SetLoops(0);
        var dim = new Color(baseColor.R * 0.5f, baseColor.G * 0.5f, baseColor.B * 0.5f, 1.0f);
        blinkTween.TweenProperty(sprite, "modulate", dim, 0.6f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        blinkTween.TweenProperty(sprite, "modulate", baseColor, 0.6f)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
    }

    private void StopBlinking()
    {
        blinkTween?.Kill();
        sprite.Modulate = baseColor;
    }
}
