using Godot;
using System.Collections.Generic;

public partial class BattleScreen : Control
{
    [Signal] public delegate void LightCellRequestedEventHandler(Vector2I position);
    [Signal] public delegate void PlayCardRequestedEventHandler(int instanceId);
    [Signal] public delegate void EndTurnRequestedEventHandler();
    [Signal] public delegate void BackToBuildRequestedEventHandler();

    private BoardManager boardManager;
    private BattleManager battleManager;

    [Export] private GridContainer battlegrid;
    [Export] private VBoxContainer activationbox;
    [Export] private RichTextLabel loglabel;
    [Export] private Label statuslabel;
    [Export] private Button endturnbutton;
    [Export] private Button backbutton;

    private List<Button> battleButtons = new();
    private Vector2I[] buttonPositions = new Vector2I[12];

    public void Setup(BoardManager board, BattleManager battle)
    {
        boardManager = board;
        battleManager = battle;

        foreach (Node child in battlegrid.GetChildren())
        {
            if (child is Button btn)
                battleButtons.Add(btn);
        }

        for (int i = 0; i < 12; i++)
            buttonPositions[i] = new Vector2I(i % 4, i / 4);

        for (int i = 0; i < battleButtons.Count; i++)
        {
            int index = i;
            battleButtons[i].Pressed += () => OnBattleCellPressed(index);
        }

        endturnbutton.Pressed += () => EmitSignal(SignalName.EndTurnRequested);
        backbutton.Pressed += () => EmitSignal(SignalName.BackToBuildRequested);
    }

    public void RefreshAll(int gridWidth, int gridHeight)
    {
        RefreshBoard(gridWidth, gridHeight);
        RefreshStatus();
        RefreshActivationButtons();
    }

    public void AppendLog(string text)
    {
        if (loglabel == null) return;
        loglabel.AppendText(text + "\n");
        loglabel.ScrollToLine(Mathf.Max(0, loglabel.GetLineCount() - 1));
    }

    private void RefreshBoard(int gridWidth, int gridHeight)
    {
        for (int i = 0; i < 12; i++)
        {
            var pos = buttonPositions[i];
            var btn = battleButtons[i];

            if (pos.X >= gridWidth || pos.Y >= gridHeight)
            {
                btn.Text = "";
                btn.Disabled = true;
                continue;
            }

            var cell = boardManager.GetCell(pos);
            var runtime = boardManager.GetCardByCell(pos);
            btn.Modulate = Colors.White;
            btn.Text = "";

            if (runtime == null) { btn.Disabled = true; continue; }

            btn.Disabled = battleManager.CurrentPhase != BattleManager.Phase.PlayerLighting;

            var data = runtime.Get("data").As<GodotObject>();
            bool isLit = (bool)cell.Get("is_lit");
            int cooldown = (int)runtime.Get("cooldown_remaining");
            btn.Text = $"{data.Get("icon_text")}\n{data.Get("display_name")}\n{(isLit ? "点亮" : "未点亮")}";

            if (isLit) btn.Modulate = new Color(1.0f, 1.0f, 0.65f);
            if (cooldown > 0)
            {
                btn.Modulate = new Color(0.55f, 0.55f, 0.55f);
                btn.Text += $"\n冷却{cooldown}";
            }
        }
    }

    private void RefreshStatus()
    {
        if (statuslabel == null) return;
        statuslabel.Text = string.Format(
            "回合 {0}　玩家 HP {1}/{2}　能量 {3}　护盾 {4}　｜　假人 HP {5}/{6}　护盾 {7}",
            battleManager.RoundNumber, battleManager.PlayerHp, battleManager.PlayerMaxHp,
            battleManager.PlayerEnergy, battleManager.PlayerShield,
            battleManager.EnemyHp, battleManager.EnemyMaxHp, battleManager.EnemyShield);
    }

    private void RefreshActivationButtons()
    {
        if (activationbox == null) return;
        foreach (Node child in activationbox.GetChildren())
            child.QueueFree();

        foreach (var runtime in boardManager.runtime_cards)
        {
            if (!boardManager.CheckCardReady(runtime)) continue;
            var data = runtime.Get("data").As<GodotObject>();
            var button = new Button();
            button.Text = $"发动｜{data.Get("display_name")}";
            int id = (int)runtime.Get("instance_id");
            button.Pressed += () => EmitSignal(SignalName.PlayCardRequested, id);
            activationbox.AddChild(button);
        }
    }

    private void OnBattleCellPressed(int index)
    {
        var pos = buttonPositions[index];
        EmitSignal(SignalName.LightCellRequested, pos);
    }
}