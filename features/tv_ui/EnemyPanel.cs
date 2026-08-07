using Godot;
using Godot.Collections;

/// <summary>
/// TV 版敌人信息面板。嵌入 PlayerTV.PanelStack。
/// set_mode(0=Exploration) → Visible=false，set_mode(1=Battle) → 正常显示。
/// </summary>
[GlobalClass]
public partial class EnemyPanel : Control
{
    [Export] private TextureRect _enemyIcon;
    [Export] private TextureProgressBar _healthBar;
    [Export] private Label _healthLabel;
    [Export] private TextureProgressBar _defenceBar;
    [Export] private Label _defenceLabel;
    [Export] private GridContainer _buffGrid;
    [Export] private Label _messageLabel;

    public override void _Ready()
    {
        Clear();
    }

    /// <summary>0 = Exploration, 1 = Battle</summary>
    public void SetMode(int mode)
    {
        Visible = mode == 1;
    }

    /// <summary>
    /// 更新敌人数据。
    /// data: { "icon", "hp", "max_hp", "shield", "max_shield",
    ///        "buffs": Array[Dictionary], "intent": string }
    /// </summary>
    public void UpdateEnemy(Godot.Collections.Dictionary data)
    {
        if (data == null) return;

        // 图标
        if (_enemyIcon != null && data.TryGetValue("icon", out var icon))
            _enemyIcon.Texture = icon.As<Texture2D>();

        // 血量
        if (data.TryGetValue("hp", out var hp) && data.TryGetValue("max_hp", out var maxHp))
        {
            int h = hp.AsInt32(), mh = maxHp.AsInt32();
            if (_healthBar != null) { _healthBar.MaxValue = mh; _healthBar.Value = h; }
            if (_healthLabel != null) _healthLabel.Text = $"{h}/{mh}";
        }

        // 护盾
        if (data.TryGetValue("shield", out var sh) && data.TryGetValue("max_shield", out var msh))
        {
            int s = sh.AsInt32(), ms = msh.AsInt32();
            if (_defenceBar != null) { _defenceBar.MaxValue = ms; _defenceBar.Value = s; }
            if (_defenceLabel != null) _defenceLabel.Text = $"{s}/{ms}";
        }

        // Buff
        if (_buffGrid != null && data.TryGetValue("buffs", out var buffs))
        {
            foreach (Node child in _buffGrid.GetChildren())
                child.QueueFree();

            var buffArray = buffs.As<Array<Godot.Collections.Dictionary>>();
            if (buffArray != null)
            {
                foreach (var buff in buffArray)
                {
                    var buffIcon = new TextureRect();
                    buffIcon.Texture = buff.TryGetValue("icon", out var bi) ? bi.As<Texture2D>() : null;
                    buffIcon.CustomMinimumSize = new Vector2(24, 24);
                    buffIcon.StretchMode = TextureRect.StretchModeEnum.KeepAspectCentered;
                    _buffGrid.AddChild(buffIcon);
                }
            }
        }

        // 意图
        if (_messageLabel != null && data.TryGetValue("intent", out var intent))
            _messageLabel.Text = intent.AsString();
    }

    public void Clear()
    {
        if (_healthBar != null) _healthBar.Value = 0;
        if (_defenceBar != null) _defenceBar.Value = 0;
        if (_healthLabel != null) _healthLabel.Text = "";
        if (_defenceLabel != null) _defenceLabel.Text = "";
        if (_messageLabel != null) _messageLabel.Text = "";
        if (_buffGrid != null)
            foreach (Node child in _buffGrid.GetChildren())
                child.QueueFree();
    }
}
