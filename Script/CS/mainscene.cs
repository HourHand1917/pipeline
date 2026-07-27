using Godot;

public partial class MainScene : Node
{
    [Export] public BoardManager BoardManager { get; set; }
    [Export] public EffectResolver EffectResolver { get; set; }
    [Export] public BattleManager BattleManager { get; set; }
    [Export] public UIManager UIManager { get; set; }

    public override void _Ready()
    {
        BattleManager.Setup(BoardManager, EffectResolver);
        UIManager.Setup(BoardManager, BattleManager);

        UIManager.PlaceCardRequested += OnPlaceCardRequested;
        UIManager.RemoveCardRequested += BoardManager.RemoveCard;
        UIManager.LightCellRequested += BattleManager.TryLightCell;
        UIManager.PlayCardRequested += BattleManager.TryPlayCard;
        UIManager.EndTurnRequested += BattleManager.EndTurn;
        UIManager.StartBattleRequested += OnStartBattleRequested;
        UIManager.ClearBuildRequested += OnClearBuildRequested;

        BoardManager.BoardChanged += UIManager.RefreshAll;
        BattleManager.BattleStateChanged += UIManager.RefreshAll;
        BattleManager.LogMessage += UIManager.AppendLog;
        BattleManager.BattleEnded += UIManager.ShowResult;

        UIManager.RefreshAll();
    }

    private void OnPlaceCardRequested(StringName cardId, Vector2I anchor, int rotationSteps)
    {
        var card = DataManager.Instance.GetCard(cardId);
        if (card == null)
            return;

        BoardManager.PlaceCard(card, anchor, rotationSteps);
    }

    private void OnStartBattleRequested()
    {
        if (BoardManager.runtime_cards.Count == 0)
        {
            UIManager.AppendLog("至少放置一个物品。");
            return;
        }

        DataManager.Instance.SaveBuild(BoardManager.runtime_cards);
        UIManager.ShowBattleScreen();
        BattleManager.StartBattle();
    }

    private void OnClearBuildRequested()
    {
        BoardManager.ResetBoard();
    }
}