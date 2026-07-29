using Godot;
using Godot.Collections;
using System.Collections.Generic;

public partial class BattleScreen : Control
{
    // ============ 信号 ============
    [Signal] public delegate void LightCellRequestedEventHandler(Vector2I position);
    [Signal] public delegate void PlayCardRequestedEventHandler(int instanceId);
    [Signal] public delegate void MoveRequestedEventHandler(int action);
    [Signal] public delegate void EndTurnRequestedEventHandler();
    [Signal] public delegate void BackToBuildRequestedEventHandler();

    // ============ 外部引用 ============
    private BoardManager boardManager;
    private BattleManager battleManager;

    // ============ 编辑器 Export ============
    [Export] private GridContainer battlegrid;
    [Export] private VBoxContainer activationbox;
    [Export] private VBoxContainer readylist;
    [Export] private RichTextLabel loglabel;
    [Export] private Label statuslabel;
    [Export] private Label energylabel;
    [Export] private Label distancelabel;
    [Export] private Label roundlabel;
    [Export] private Label phaselabel;
    [Export] private Label readylabel;
    [Export] private Button movebackbutton;
    [Export] private Button moveforwardbutton;
    [Export] private Button endturnbutton;
    [Export] private Button backbutton;

    // ============ 内部数据 ============
    private List<Button> battleButtons = new();
    private Vector2I[] buttonPositions = new Vector2I[12];

    // ================================================================
    //  Setup
    // ================================================================

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

        movebackbutton.Pressed += () => EmitSignal(SignalName.MoveRequested, (int)BattleManager.MoveAction.Backward);
        moveforwardbutton.Pressed += () => EmitSignal(SignalName.MoveRequested, (int)BattleManager.MoveAction.Forward);
        endturnbutton.Pressed += () => EmitSignal(SignalName.EndTurnRequested);
        backbutton.Pressed += () => EmitSignal(SignalName.BackToBuildRequested);
    }

    // ================================================================
    //  公共接口
    // ================================================================

    public void RefreshAll(int gridWidth, int gridHeight)
    {
        if (boardManager == null || battleManager == null) return;
        RefreshBoard(gridWidth, gridHeight);
        RefreshStatus();
        RefreshActivationButtons();
    }

    public void AppendLog(string text)
    {
        if (loglabel == null) return;
        loglabel.AppendText("• " + text + "\n");
        loglabel.ScrollToLine(Mathf.Max(0, loglabel.GetLineCount() - 1));
    }

    public void ClearLog()
    {
        if (loglabel != null) loglabel.Clear();
    }

    // ================================================================
    //  板子刷新
    // ================================================================

    private void RefreshBoard(int gridWidth, int gridHeight)
    {
        bool canLight = battleManager.CurrentPhase == BattleManager.Phase.PlayerTurn;

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

            if (runtime == null)
            {
                btn.Disabled = true;
                continue;
            }

            btn.Disabled = !canLight;

            var data = runtime.Get("data").As<GodotObject>();
            bool isLit = cell.Get("is_lit").AsBool();
            int cooldown = runtime.Get("cooldown_remaining").AsInt32();

            btn.Text = $"{data.Get("icon_text")}\n{data.Get("display_name")}\n{(isLit ? "点亮" : "未点亮")}";

            if (isLit)
                btn.Modulate = new Color(1.0f, 1.0f, 0.65f);

            if (cooldown > 0)
            {
                btn.Modulate = new Color(0.55f, 0.55f, 0.55f);
                btn.Text += $"\n冷却{cooldown}";
            }
        }
    }

    // ================================================================
    //  状态刷新
    // ================================================================

    private void RefreshStatus()
    {
        if (statuslabel != null)
        {
            statuslabel.Text = string.Format(
                "玩家 HP {0}/{1}　护盾 {2}　｜　假人 HP {3}/{4}　护盾 {5}",
                battleManager.PlayerHp, battleManager.PlayerMaxHp,
                battleManager.PlayerShield,
                battleManager.EnemyHp, battleManager.EnemyMaxHp,
                battleManager.EnemyShield);
        }

        if (roundlabel != null)
            roundlabel.Text = $"回合 {battleManager.RoundNumber}";

        if (phaselabel != null)
            phaselabel.Text = PhaseText();

        if (energylabel != null)
        {
            var rules = DataManager.Instance.GetRules();
            int maxEnergy = rules?.Get("energy_per_turn").AsInt32() ?? 4;
            energylabel.Text = $"⚡ {battleManager.PlayerEnergy} / {maxEnergy}";
        }

        if (distancelabel != null)
        {
            distancelabel.Text = $"玩家 {battleManager.PlayerMapPosition} · 怪物 {battleManager.EnemyMapPosition} · 距离 {battleManager.Distance} 格";
        }

        // 移动按钮状态
        bool canAct = battleManager.CurrentPhase == BattleManager.Phase.PlayerTurn;
        var rules2 = DataManager.Instance.GetRules();
        if (rules2 != null)
        {
            int moveStep = rules2.Get("player_move_step").AsInt32();
            int moveCost = rules2.Get("move_energy_cost").AsInt32();

            if (movebackbutton != null)
            {
                movebackbutton.Text = $"← 后退{moveStep}格 · {moveCost} ⚡";
                movebackbutton.Disabled = !canAct || !battleManager.CanPlayerMove((int)BattleManager.MoveAction.Backward);
            }
            if (moveforwardbutton != null)
            {
                moveforwardbutton.Text = $"前进{moveStep}格 · {moveCost} ⚡ →";
                moveforwardbutton.Disabled = !canAct || !battleManager.CanPlayerMove((int)BattleManager.MoveAction.Forward);
            }
        }

        if (endturnbutton != null)
            endturnbutton.Disabled = !canAct;
    }

    // ================================================================
    //  发动按钮刷新
    // ================================================================

    private void RefreshActivationButtons()
    {
        if (activationbox == null) return;
        foreach (Node child in activationbox.GetChildren())
            child.QueueFree();

        if (readylist != null)
        {
            foreach (Node child in readylist.GetChildren())
                child.QueueFree();
        }

        int readyCount = 0;
        foreach (var runtime in boardManager.runtime_cards)
        {
            if (!boardManager.CheckCardReady(runtime)) continue;

            readyCount++;
            var data = runtime.Get("data").As<GodotObject>();
            var button = new Button();
            button.Text = $"发动｜{data.Get("display_name")}";
            button.Disabled = battleManager.CurrentPhase != BattleManager.Phase.PlayerTurn;
            int id = runtime.Get("instance_id").AsInt32();
            button.Pressed += () => EmitSignal(SignalName.PlayCardRequested, id);
            activationbox.AddChild(button);
        }

        if (readylabel != null)
            readylabel.Text = $"{readyCount} 张就绪";
    }

    // ================================================================
    //  工具
    // ================================================================

    private string PhaseText()
    {
        return battleManager.CurrentPhase switch
        {
            BattleManager.Phase.Build => "构筑阶段",
            BattleManager.Phase.PlayerTurn => "玩家回合",
            BattleManager.Phase.EnemyTurn => "敌方回合",
            BattleManager.Phase.BattleEnd => "战斗结束",
            _ => "状态切换"
        };
    }

    private void OnBattleCellPressed(int index)
    {
        var pos = buttonPositions[index];
        EmitSignal(SignalName.LightCellRequested, pos);
    }
}