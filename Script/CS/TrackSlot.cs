using Godot;

[GlobalClass]
public partial class TrackSlot : PanelContainer
{
	[Signal] public delegate void SlotClickedEventHandler(int cellNumber, GodotObject combatant);
	[Signal] public delegate void MoveRequestedEventHandler(int cellNumber);

	[Export] private Control occupant;
	[Export] private AnimatedSprite2D animatedSprite;
	[Export] private Label glyphLabel;
	[Export] private Label slotLabel;
	[Export] private Label dangerLabel;

	public int CellNumber { get; private set; }
	public GodotObject OccupantRef { get; private set; }

	private static readonly Color DangerTint = new("#d94b45");
	private static readonly Color WarningTint = new("#d5a53a");
	private bool _isDanger;
	private bool _isFutureDanger;
	private StyleBoxFlat _normalStyle;
	private StyleBoxFlat _dangerStyle;
	private StyleBoxFlat _warningStyle;

	public void Configure(int cellNum, string glyph, Color tint, GodotObject combatant)
	{
		CellNumber = cellNum;
		OccupantRef = combatant;
		slotLabel.Text = $"{cellNum}";

		if (string.IsNullOrEmpty(glyph))
		{
			glyphLabel.Text = "";
			occupant.Modulate = Colors.White;
			OccupantRef = null;
		}
		else
		{
			glyphLabel.Text = glyph;
			glyphLabel.Modulate = tint;
			occupant.Modulate = new Color(tint.R, tint.G, tint.B, 0.3f);
		}

		ApplyPredictionVisual();
	}

	public void SetDangerState(bool current, bool future)
	{
		_isDanger = current;
		_isFutureDanger = future;
		ApplyPredictionVisual();
	}

	private void ApplyPredictionVisual()
	{
		EnsureStyles();
		slotLabel.Text = $"{CellNumber}";
		slotLabel.Modulate = new Color("#a8bdc1");
		if (dangerLabel != null)
			dangerLabel.Visible = _isDanger || _isFutureDanger;

		if (_isDanger)
		{
			AddThemeStyleboxOverride("panel", _dangerStyle);
			if (dangerLabel != null)
			{
				dangerLabel.Text = "⚠ 危险";
				dangerLabel.Modulate = Colors.White;
			}
			TooltipText = "危险：敌人下回合会攻击这里";
		}
		else if (_isFutureDanger)
		{
			AddThemeStyleboxOverride("panel", _warningStyle);
			if (dangerLabel != null)
			{
				dangerLabel.Text = "◇ 预警";
				dangerLabel.Modulate = new Color("#ffe8a0");
			}
			TooltipText = "预警：敌人正在准备覆盖这里";
		}
		else
		{
			AddThemeStyleboxOverride("panel", _normalStyle);
			if (dangerLabel != null)
				dangerLabel.Text = "";
			TooltipText = "";
		}
		SelfModulate = Colors.White;
	}

	private void EnsureStyles()
	{
		_normalStyle ??= MakeStyle(new Color("#101b20cc"), new Color("#34464c"), 1);
		_dangerStyle ??= MakeStyle(new Color("#7b211fe8"), DangerTint, 4);
		_warningStyle ??= MakeStyle(new Color("#594615e8"), WarningTint, 3);
	}

	private static StyleBoxFlat MakeStyle(Color background, Color border, int width)
	{
		return new StyleBoxFlat
		{
			BgColor = background,
			BorderColor = border,
			BorderWidthLeft = width,
			BorderWidthTop = width,
			BorderWidthRight = width,
			BorderWidthBottom = width,
			CornerRadiusTopLeft = 7,
			CornerRadiusTopRight = 7,
			CornerRadiusBottomLeft = 7,
			CornerRadiusBottomRight = 7,
			ContentMarginLeft = 5,
			ContentMarginTop = 5,
			ContentMarginRight = 5,
			ContentMarginBottom = 5,
		};
	}

	public override void _Ready()
	{
		GuiInput += OnGuiInput;
	}

	private void OnGuiInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mb && mb.Pressed && mb.ButtonIndex == MouseButton.Left)
		{
			if (OccupantRef == null)
			{
				// 空格子 → 请求移动
				EmitSignal(SignalName.MoveRequested, CellNumber);
			}
			else
			{
				// 有角色 → 显示信息
				EmitSignal(SignalName.SlotClicked, CellNumber, OccupantRef);
			}
		}
	}

	public void PlayAnimation(string animName)
	{
		animatedSprite?.Play(animName);
	}
}
