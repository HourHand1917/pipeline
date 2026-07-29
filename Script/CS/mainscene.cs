using Godot;

public partial class MainScene : Node
{
    [Export] public BoardManager BoardManager { get; set; }
    [Export] public EffectResolver EffectResolver { get; set; }
    [Export] public BattleManager BattleManager { get; set; }
    [Export] public BuildScreen BuildScreen { get; set; }
    [Export] public BattleScreen BattleScreen { get; set; }

    private int currentGridWidth = 3;
    private int currentGridHeight = 2;

    public override void _Ready()
    {
        BattleManager.Setup(BoardManager, EffectResolver);

        BuildScreen.Setup(BoardManager);
        BattleScreen.Setup(BoardManager, BattleManager);

        BuildScreen.PlaceCardRequested += OnPlaceCardRequested;
        BuildScreen.RemoveCardRequested += BoardManager.RemoveCard;
        BuildScreen.StartBattleRequested += OnStartBattleRequested;
        BuildScreen.ClearBuildRequested += OnClearBuildRequested;
        BuildScreen.GridSizeChanged += OnGridSizeChanged;

        BattleScreen.LightCellRequested += BattleManager.TryLightCell;
        BattleScreen.PlayCardRequested += BattleManager.TryPlayCard;
        BattleScreen.EndTurnRequested += BattleManager.EndTurn;
        BattleScreen.BackToBuildRequested += ShowBuild;

        BoardManager.BoardChanged += RefreshCurrentScreen;
        BattleManager.BattleStateChanged += RefreshBattleIfActive;
        BattleManager.LogMessage += BattleScreen.AppendLog;

        ShowBuild();
    }

    private void OnPlaceCardRequested(StringName cardId, Vector2I anchor, int rotationSteps)
    {
        var card = DataManager.Instance.GetCard(cardId);
        if (card == null) return;
        BoardManager.PlaceCard(card, anchor, rotationSteps);
    }

    private void OnStartBattleRequested()
    {
        if (BoardManager.runtime_cards.Count == 0)
        {
            BattleScreen.AppendLog("至少放置一个物品。");
            return;
        }
        DataManager.Instance.SaveBuild(BoardManager.runtime_cards);
        ShowBattle();
        BattleManager.StartBattle();
    }

    private void OnClearBuildRequested()
    {
        BoardManager.ResetBoard(currentGridWidth, currentGridHeight);
    }

    private void OnGridSizeChanged(int width, int height)
    {
        currentGridWidth = width;
        currentGridHeight = height;
        BoardManager.ResetBoard(width, height);
    }

    private void ShowBuild()
    {
        BuildScreen.Visible = true;
        BattleScreen.Visible = false;
        BuildScreen.RefreshAll();
    }

    private void ShowBattle()
    {
        BuildScreen.Visible = false;
        BattleScreen.Visible = true;
        BattleScreen.RefreshAll(currentGridWidth, currentGridHeight);
    }

    private void RefreshCurrentScreen()
    {
        if (BuildScreen.Visible)
            BuildScreen.RefreshAll();
        else
            BattleScreen.RefreshAll(currentGridWidth, currentGridHeight);
    }

    private void RefreshBattleIfActive()
    {
        if (BattleScreen.Visible)
            BattleScreen.RefreshAll(currentGridWidth, currentGridHeight);
    }
}