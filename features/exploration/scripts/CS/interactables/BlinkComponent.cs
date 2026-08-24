using Godot;

/// <summary>
/// 交互物视觉效果组件。挂到 InteractableBase 子节点即可获得范围闪烁 + 悬停高亮。
/// 通过 @export 注入依赖，不靠 get_node 爬树。
/// 暴露 IsHovered 给 InteractableBase 判断是否允许点击。
/// 后续改效果只需替换此组件，不影响互动逻辑。
/// </summary>
[GlobalClass]
public partial class BlinkComponent : Node
{
	/// <summary>鼠标是否在 ClickZone 内</summary>
	public bool IsHovered { get; private set; }

	[Export] private Node2D _sprite;
	[Export] private Area2D _clickZone;
	[Export] private Texture2D _openTexture;

	private Color _baseColor;
	private Texture2D _closedTexture;
	private bool _isPlayerInRange;
	private Tween _blinkTween;
	private bool _enabled = true;

	public bool IsPlayerInRange
	{
		get => _isPlayerInRange;
		set
		{
			if (_isPlayerInRange == value) return;
			_isPlayerInRange = value;
			SetDoorTexture(_isPlayerInRange);
			if (_isPlayerInRange) OnHoverChanged();
			else StopBlink();
		}
	}

	public bool Enabled
	{
		get => _enabled;
		set
		{
			_enabled = value;
			if (!_enabled) { _blinkTween?.Kill(); _sprite.Modulate = _baseColor; }
			else if (_isPlayerInRange) OnHoverChanged();
		}
	}

	public override void _Ready()
	{
		if (_sprite == null || _clickZone == null)
		{
			GD.PushWarning($"{Name}: BlinkComponent 缺少 Sprite 或 ClickZone 配置。");
			return;
		}
		_baseColor = _sprite.Modulate;
		if (_sprite is Sprite2D sprite2D)
			_closedTexture = sprite2D.Texture;
		_clickZone.MouseEntered += () => { IsHovered = true; OnHoverChanged(); };
		_clickZone.MouseExited += () => { IsHovered = false; OnHoverChanged(); };
	}

	private void OnHoverChanged()
	{
		if (!_isPlayerInRange || !_enabled) return;

		if (IsHovered)
		{
			_blinkTween?.Kill();
			_sprite.Modulate = new Color(_baseColor.R * 1.5f, _baseColor.G * 1.5f, _baseColor.B * 1.5f, 1.0f);
		}
		else
		{
			StartBlink();
		}
	}

	private void StartBlink()
	{
		if (!_enabled) return;
		_blinkTween?.Kill();
		_blinkTween = CreateTween();
		_blinkTween.SetLoops(0);
		var dim = new Color(_baseColor.R * 0.5f, _baseColor.G * 0.5f, _baseColor.B * 0.5f, 1.0f);
		_blinkTween.TweenProperty(_sprite, "modulate", dim, 0.6f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
		_blinkTween.TweenProperty(_sprite, "modulate", _baseColor, 0.6f)
			.SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
	}

	private void StopBlink()
	{
		_blinkTween?.Kill();
		_sprite.Modulate = _baseColor;
	}

	private void SetDoorTexture(bool open)
	{
		if (_sprite is not Sprite2D sprite2D) return;
		if (open && _openTexture != null)
			sprite2D.Texture = _openTexture;
		else if (!open && _closedTexture != null)
			sprite2D.Texture = _closedTexture;
	}
}
