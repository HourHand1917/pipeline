using Godot;
using System.Collections.Generic;

/// <summary>
/// 能力成长面板（工作台第三页）。7 个升级按钮由你在编辑器里手动摆放。
/// 按钮背景贴图也在编辑器里设（theme_override_styles/normal），脚本只负责：
///   - 把每个按钮映射到对应的升级
///   - 悬浮 tooltip（效果/花费/等级/前置）
///   - 三态：可点→正常，不可点→变暗，悬浮→变亮，已点→常亮禁用
/// 顶部显示等级（lv: xx）和水龙头（icon + 数字）。
/// </summary>
[GlobalClass]
public partial class GrowthPanel : Control
{
	[Export] private Button _hp1Button;
	[Export] private Button _hp2Button;
	[Export] private Button _str1Button;
	[Export] private Button _str2Button;
	[Export] private Button _board1Button;
	[Export] private Button _board2Button;
	[Export] private Button _energyButton;
	[Export] private Label _levelLabel;
	[Export] private Label _faucetNumber;

	private readonly Dictionary<Button, GrowthManager.Upgrade> _buttons = new();
	private readonly Dictionary<Button, Texture2D> _baseTex = new();

	private AudioManager _audio;

	public override void _Ready()
	{
		_audio = GetNodeOrNull<AudioManager>("/root/AudioManager");

		Register(_hp1Button, "hp_1");
		Register(_hp2Button, "hp_2");
		Register(_str1Button, "str_1");
		Register(_str2Button, "str_2");
		Register(_board1Button, "board_1");
		Register(_board2Button, "board_2");
		Register(_energyButton, "energy");

		Refresh();
		GrowthManager.Instance.Changed += Refresh;
		DataManager.Instance.CurrencyChanged += Refresh;
		DataManager.Instance.LevelChanged += OnLevelChanged;
	}

	public override void _ExitTree()
	{
		if (GrowthManager.Instance != null)
			GrowthManager.Instance.Changed -= Refresh;
		DataManager.Instance.CurrencyChanged -= Refresh;
		DataManager.Instance.LevelChanged -= OnLevelChanged;
	}

	private void OnLevelChanged(int newLevel) => Refresh();

	private void Register(Button btn, string id)
	{
		if (btn == null) return;
		var upgrade = GrowthManager.GetUpgrade(id);
		if (upgrade == null) return;

		_buttons[btn] = upgrade;
		_baseTex[btn] = GetBaseTexture(btn);

		_audio?.AttachUiSounds(btn);

		btn.Pressed += () =>
		{
			if (GrowthManager.Instance.Buy(upgrade))
				_audio?.PlayUiSuccess();
			else
				_audio?.PlayUiInvalid();
		};

		AttachTooltip(btn, upgrade);
	}

	private static Texture2D GetBaseTexture(Button btn)
	{
		return (btn.GetThemeStylebox("normal") as StyleBoxTexture)?.Texture;
	}

	private void AttachTooltip(Button btn, GrowthManager.Upgrade u)
	{
		var details = new Godot.Collections.Dictionary<string, string>
		{
			{ "水龙头", $"${u.Cost}" },
			{ "需要等级", $"lv{u.RequireLevel}" },
		};
		if (!string.IsNullOrEmpty(u.Prerequisite))
			details["前置"] = GrowthManager.GetUpgrade(u.Prerequisite)?.Name ?? u.Prerequisite;

		TooltipService.Instance.ShowFor(btn, new TooltipData
		{
			Title = u.Name,
			Description = u.Description,
			Details = details,
		});
	}

	private void Refresh()
	{
		if (_levelLabel != null)
			_levelLabel.Text = $"lv: {DataManager.Instance.Lv}";
		if (_faucetNumber != null)
			_faucetNumber.Text = $"{GrowthManager.Instance.Faucet}";

		foreach (var (btn, upgrade) in _buttons)
		{
			var tex = _baseTex.TryGetValue(btn, out var t) ? t : null;
			bool purchased = GrowthManager.Instance.IsPurchased(upgrade.Id);

			if (purchased)
			{
				btn.Disabled = true;
				var yellow = new Color(1f, 0.85f, 0.2f);
				ApplyStyles(btn, tex, yellow, yellow, yellow);
			}
			else if (GrowthManager.Instance.CanBuy(upgrade))
			{
				btn.Disabled = false;
				ApplyStyles(btn, tex, Gray(1.0f), Gray(1.35f), Gray(0.45f));
			}
			else
			{
				btn.Disabled = true;
				ApplyStyles(btn, tex, Gray(0.45f), Gray(0.45f), Gray(0.45f));
			}
		}
	}

	private static Color Gray(float m) => new Color(m, m, m);

	private static void ApplyStyles(Button btn, Texture2D tex, Color normal, Color hover, Color disabled)
	{
		if (tex == null) return;
		btn.AddThemeStyleboxOverride("normal", MakeStyle(tex, normal));
		btn.AddThemeStyleboxOverride("hover", MakeStyle(tex, hover));
		btn.AddThemeStyleboxOverride("disabled", MakeStyle(tex, disabled));
	}

	private static StyleBoxTexture MakeStyle(Texture2D tex, Color m)
	{
		var s = new StyleBoxTexture { Texture = tex, ModulateColor = m };
		s.ContentMarginLeft = 24;
		s.ContentMarginRight = 24;
		s.ContentMarginTop = 16;
		s.ContentMarginBottom = 16;
		return s;
	}
}
