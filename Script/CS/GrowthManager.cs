using Godot;
using System.Collections.Generic;

/// <summary>
/// 能力成长（Autoload）。管理 7 项永久升级的购买状态与永久增益。
///
/// 结构：3 个一级技能（lv1，无前置）+ 4 个二级技能（lv2，需先点亮前置）。
///   hp_1 → hp_2
///   str_1 → str_2
///   board_1 → board_2（并列）→ energy
///
/// 花费水龙头（Faucet）购买。已购买状态存内存（Autoload 跨场景保留）。
/// 增益通过查询方法提供给战斗侧：
///   GetMaxHpBonus()     → 血上限加成（25 / 55）
///   GetStrengthStacks() → 入场力量 buff 层数（1 / 2）
///   GetBoardSize()      → 棋盘尺寸（3x3 / 4x3），未点返回 null
///   GetEnergyBonus()    → 每回合能量加成（0 / 1）
/// </summary>
[GlobalClass]
public partial class GrowthManager : Node
{
	public enum EffectType { MaxHp, Strength, BoardSize, EnergyPerTurn }

	public class Upgrade
	{
		public string Id;
		public string Name;
		public string Description;
		public int Cost;
		public int Level;              // 1 = 一级（无前置），2 = 二级（有前置）
		public string Prerequisite;    // 前置 id，一级为 ""
		public EffectType Effect;
		public int Value;              // MaxHp / Strength / EnergyPerTurn 用
		public Vector2I Board;         // BoardSize 用

		public override string ToString() => Id;
	}

	[Signal] public delegate void ChangedEventHandler();

	public static GrowthManager Instance { get; private set; }

	public static readonly Upgrade[] Upgrades =
	{
		new Upgrade { Id = "hp_1",     Name = "生命提升I",    Description = "提升 25 点血上限。",              Cost = 10, Level = 1, Prerequisite = "",        Effect = EffectType.MaxHp,        Value = 25 },
		new Upgrade { Id = "hp_2",     Name = "生命提升II",   Description = "再提升 30 点血上限。",            Cost = 25, Level = 2, Prerequisite = "hp_1",    Effect = EffectType.MaxHp,        Value = 30 },
		new Upgrade { Id = "str_1",    Name = "力量提升I",    Description = "进入战斗时获得 1 点力量。",        Cost = 15, Level = 1, Prerequisite = "",        Effect = EffectType.Strength,     Value = 1 },
		new Upgrade { Id = "str_2",    Name = "力量提升II",   Description = "进入战斗时再获得 1 点力量。",      Cost = 30, Level = 2, Prerequisite = "str_1",   Effect = EffectType.Strength,     Value = 1 },
		new Upgrade { Id = "board_1",  Name = "棋盘扩容I",    Description = "棋盘变为 3×3。",                  Cost = 20, Level = 1, Prerequisite = "",        Effect = EffectType.BoardSize,    Board = new Vector2I(3, 3) },
		new Upgrade { Id = "board_2",  Name = "棋盘扩容II",   Description = "棋盘变为 4×3。",                  Cost = 40, Level = 2, Prerequisite = "board_1", Effect = EffectType.BoardSize,    Board = new Vector2I(4, 3) },
		new Upgrade { Id = "energy",   Name = "能量提升",     Description = "每回合能量 +1。",                  Cost = 40, Level = 2, Prerequisite = "board_1", Effect = EffectType.EnergyPerTurn, Value = 1 },
	};

	private static readonly Dictionary<string, Upgrade> _byId = BuildIndex();
	private readonly HashSet<string> _purchased = new();

	public override void _Ready()
	{
		if (Instance != null) { GD.PushError("GrowthManager 重复实例化"); return; }
		Instance = this;
	}

	private static Dictionary<string, Upgrade> BuildIndex()
	{
		var d = new Dictionary<string, Upgrade>();
		foreach (var u in Upgrades) d[u.Id] = u;
		return d;
	}

	public static Upgrade GetUpgrade(string id) => _byId.TryGetValue(id, out var u) ? u : null;

	// ================================================================
	//  查询
	// ================================================================

	public bool IsPurchased(string id) => _purchased.Contains(id);

	public int Faucet => DataManager.Instance?.Faucet ?? 0;

	/// <summary>前置是否已满足（一级技能恒为 true）。</summary>
	public bool PrerequisiteMet(Upgrade u) =>
		u == null || string.IsNullOrEmpty(u.Prerequisite) || _purchased.Contains(u.Prerequisite);

	public bool CanBuy(Upgrade u) =>
		u != null && !_purchased.Contains(u.Id) && PrerequisiteMet(u) && Faucet >= u.Cost;

	public bool Buy(Upgrade u)
	{
		if (!CanBuy(u)) return false;

		DataManager.Instance.ModifyCurrency(DataManager.CurrencyType.Faucet, -u.Cost);
		_purchased.Add(u.Id);

		// 血上限：立即生效（探索 HUD 同步显示），战斗侧再通过 GetMaxHpBonus 叠加到入场血上限。
		if (u.Effect == EffectType.MaxHp)
			DataManager.Instance.SetMaxHp(DataManager.Instance.MaxPlayerHp + u.Value);

		EmitSignal(SignalName.Changed);
		GD.Print($"能力成长：点亮 {u.Name}");
		return true;
	}

	// ================================================================
	//  永久增益查询（战斗侧调用）
	// ================================================================

	public int GetMaxHpBonus()
	{
		int sum = 0;
		foreach (var u in Upgrades)
			if (u.Effect == EffectType.MaxHp && _purchased.Contains(u.Id)) sum += u.Value;
		return sum;
	}

	public int GetStrengthStacks()
	{
		int sum = 0;
		foreach (var u in Upgrades)
			if (u.Effect == EffectType.Strength && _purchased.Contains(u.Id)) sum += u.Value;
		return sum;
	}

	/// <summary>当前应使用的棋盘尺寸；未点扩容返回 null（用默认 3×2）。</summary>
	public Vector2I? GetBoardSize()
	{
		Vector2I best = default;
		bool found = false;
		foreach (var u in Upgrades)
		{
			if (u.Effect == EffectType.BoardSize && _purchased.Contains(u.Id))
			{
				if (!found || u.Board.X * u.Board.Y > best.X * best.Y) best = u.Board;
				found = true;
			}
		}
		return found ? best : (Vector2I?)null;
	}

	public int GetEnergyBonus()
	{
		int sum = 0;
		foreach (var u in Upgrades)
			if (u.Effect == EffectType.EnergyPerTurn && _purchased.Contains(u.Id)) sum += u.Value;
		return sum;
	}
}
