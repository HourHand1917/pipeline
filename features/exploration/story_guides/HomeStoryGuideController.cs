using Godot;
using Godot.Collections;

/// <summary>
/// Home-only story guide coordinator. It is intentionally packaged as one
/// draggable scene and gates itself from real boss persistence flags rather
/// than the player's level (level 1 is also the new-game default).
/// </summary>
[GlobalClass]
public partial class HomeStoryGuideController : Node
{
    [ExportGroup("Scene bindings (optional; auto-found when empty)")]
    [Export] public PlayerController Player { get; set; }
    [Export] public WorkbenchInteractable Workbench { get; set; }
    [Export] public WorkbenchUI WorkbenchUI { get; set; }
    [Export] public StoreInteractable Store { get; set; }
    [Export] public ShopUI ShopUI { get; set; }
    [Export] public ExplorationInteractionGuide WorkbenchGuide { get; set; }
    [Export] public ExplorationInteractionGuide StoreGuide { get; set; }

    [ExportGroup("Mousy scenes")]
    [Export] public PackedScene PostF1MousyScene { get; set; }
    [Export] public PackedScene PostF2MousyScene { get; set; }
    [Export] public Vector2 PostF1MousyOffset { get; set; } = new(145, 0);
    [Export] public Vector2 PostF2MousyOffsetFromStore { get; set; } = new(-180, 0);

    [ExportGroup("Progress sources")]
    [Export] public string RockyMapId { get; set; } = "f1_2";
    [Export] public string RockyPersistenceId { get; set; } = "enemy_rocky_f1_2";
    [Export] public string SharkkMapId { get; set; } = "f2_4";
    [Export] public string SharkkPersistenceId { get; set; } = "enemy_sharkk_f2_4";

    [ExportGroup("Guide persistence")]
    [Export] public string GuideStateMapId { get; set; } = "__story_guides__";
    [Export] public string PostF1GuideStateId { get; set; } = "home_after_f1_workbench";
    [Export] public string PostF2GuideStateId { get; set; } = "home_after_f2_shop";

    public bool IsPostF1Active => _activeStage == StoryStage.PostF1;
    public bool IsPostF2Active => _activeStage == StoryStage.PostF2;
    public NPCBase ActiveMousy => _activeMousy;

    private enum StoryStage { None, PostF1, PostF2 }
    private StoryStage _activeStage;
    private NPCBase _activeMousy;
    private bool _pendingDialogueStart;
    private int _dialogueStartAttempts;
    private bool _f1DialogueCompleted;
    private bool _f1GuideCompleted;
    private bool _f1Departed;
    private bool _f2DialogueCompleted;
    private bool _f2GuideCompleted;
    private bool _readyFinished;

    public override void _Ready()
    {
        Callable.From(InitializeDeferred).CallDeferred();
        SetProcess(true);
    }

    public override void _ExitTree()
    {
        DisconnectGuideSignals();
        DisconnectMousy();

        // Mousy remains beside Rubber for this home visit. Once that visit
        // ends after their first conversation, the portrait is retired.
        if (_readyFinished && _f1DialogueCompleted && !_f1Departed)
        {
            _f1Departed = true;
            SaveF1State();
        }
    }

    public override void _Process(double delta)
    {
        if (!_pendingDialogueStart || _activeMousy == null || !IsInstanceValid(_activeMousy))
            return;

        if (_activeMousy.StartConfiguredDialogueNow())
        {
            _pendingDialogueStart = false;
            return;
        }

        _dialogueStartAttempts++;
        if (_dialogueStartAttempts > 180)
        {
            GD.PushWarning("HomeStoryGuideController: 鼠鼠对话在 180 帧内未能启动。");
            _pendingDialogueStart = false;
        }
    }

    public bool IsRockyDefeated() => SourceIsDefeated(RockyMapId, RockyPersistenceId);
    public bool IsSharkkDefeated() => SourceIsDefeated(SharkkMapId, SharkkPersistenceId);

    public static bool IsDefeatedState(Dictionary state) =>
        state != null && state.TryGetValue("defeated", out Variant defeated) && defeated.AsBool();

    private void InitializeDeferred()
    {
        Node scene = GetTree().CurrentScene ?? GetParent();
        Player ??= FindDescendant<PlayerController>(scene);
        Workbench ??= FindDescendant<WorkbenchInteractable>(scene);
        WorkbenchUI ??= FindDescendant<WorkbenchUI>(scene);
        Store ??= FindDescendant<StoreInteractable>(scene);
        ShopUI ??= FindDescendant<ShopUI>(scene);
        WorkbenchGuide ??= GetNodeOrNull<ExplorationInteractionGuide>("WorkbenchGuide");
        StoreGuide ??= GetNodeOrNull<ExplorationInteractionGuide>("StoreGuide");

        WorkbenchGuide?.Configure(Workbench, WorkbenchUI);
        StoreGuide?.Configure(Store, ShopUI);
        ConnectGuideSignals();
        LoadStates();
        _readyFinished = true;
        StartNextEligibleStage();
    }

    private void StartNextEligibleStage()
    {
        if (_activeStage != StoryStage.None) return;

        if (IsRockyDefeated() && !_f1GuideCompleted)
        {
            StartPostF1();
            return;
        }

        if (IsSharkkDefeated() && !_f2GuideCompleted)
            StartPostF2();
    }

    private void StartPostF1()
    {
        _activeStage = StoryStage.PostF1;
        if (_f1DialogueCompleted)
        {
            WorkbenchGuide?.BeginGuide();
            return;
        }

        _activeMousy = SpawnMousy(PostF1MousyScene,
            (Player?.GlobalPosition ?? Vector2.Zero) + PostF1MousyOffset);
        if (_activeMousy == null)
        {
            GD.PushWarning("HomeStoryGuideController: 未能创建 F1 战后鼠鼠，直接继续工作台引导。");
            _f1DialogueCompleted = true;
            SaveF1State();
            WorkbenchGuide?.BeginGuide();
            return;
        }

        _activeMousy.DialogueFinished += OnMousyDialogueFinished;
        QueueDialogueStart();
    }

    private void StartPostF2()
    {
        _activeStage = StoryStage.PostF2;
        if (!_f2DialogueCompleted)
        {
            Vector2 position = (Store?.GlobalPosition ?? Player?.GlobalPosition ?? Vector2.Zero)
                + PostF2MousyOffsetFromStore;
            _activeMousy = SpawnMousy(PostF2MousyScene, position);
            if (_activeMousy != null)
                _activeMousy.DialogueFinished += OnMousyDialogueFinished;
        }

        StoreGuide?.BeginGuide();
        if (_f2DialogueCompleted && Store?.IsPlayerInRange == true)
            StoreGuide?.ContinueToInteraction();
    }

    private NPCBase SpawnMousy(PackedScene packed, Vector2 globalPosition)
    {
        if (packed == null) return null;
        Node instance = packed.Instantiate();
        Node parent = GetTree().CurrentScene ?? GetParent();
        parent.AddChild(instance);
        if (instance is Node2D node2D)
            node2D.GlobalPosition = globalPosition;
        return instance as NPCBase;
    }

    private void QueueDialogueStart()
    {
        _dialogueStartAttempts = 0;
        _pendingDialogueStart = true;
    }

    private void OnStoreTargetReached()
    {
        if (_activeStage != StoryStage.PostF2) return;
        if (_f2DialogueCompleted)
        {
            StoreGuide?.ContinueToInteraction();
            return;
        }

        if (_activeMousy == null || !IsInstanceValid(_activeMousy))
        {
            _f2DialogueCompleted = true;
            SaveF2State();
            StoreGuide?.ContinueToInteraction();
            return;
        }
        QueueDialogueStart();
    }

    private void OnMousyDialogueFinished()
    {
        Callable.From(HandleDialogueFinishedDeferred).CallDeferred();
    }

    private void HandleDialogueFinishedDeferred()
    {
        if (_activeStage == StoryStage.PostF1)
        {
            _f1DialogueCompleted = true;
            SaveF1State();
            WorkbenchGuide?.BeginGuide();
            return;
        }

        if (_activeStage == StoryStage.PostF2)
        {
            _f2DialogueCompleted = true;
            SaveF2State();
            StoreGuide?.ContinueToInteraction();
        }
    }

    private void OnWorkbenchGuideCompleted()
    {
        if (_activeStage != StoryStage.PostF1) return;
        _f1GuideCompleted = true;
        SaveF1State();
        FinishActiveStage();
    }

    private void OnStoreGuideCompleted()
    {
        if (_activeStage != StoryStage.PostF2) return;
        _f2GuideCompleted = true;
        SaveF2State();
        FinishActiveStage();
    }

    private void FinishActiveStage()
    {
        StoryStage finishedStage = _activeStage;
        DisconnectMousy();
        if (finishedStage == StoryStage.PostF2 && _activeMousy != null && IsInstanceValid(_activeMousy))
            _activeMousy.QueueFree();
        _activeMousy = null;
        _pendingDialogueStart = false;
        _activeStage = StoryStage.None;

        // The first Mousy remains beside Rubber for the rest of this home
        // visit. Even if a debug/save state already contains the Sharkk win,
        // do not create a second portrait on top of it; F2 starts next visit.
        if (finishedStage != StoryStage.PostF1)
            StartNextEligibleStage();
    }

    private void ConnectGuideSignals()
    {
        if (WorkbenchGuide != null)
            WorkbenchGuide.GuideCompleted += OnWorkbenchGuideCompleted;
        if (StoreGuide != null)
        {
            StoreGuide.TargetReached += OnStoreTargetReached;
            StoreGuide.GuideCompleted += OnStoreGuideCompleted;
        }
    }

    private void DisconnectGuideSignals()
    {
        if (WorkbenchGuide != null && IsInstanceValid(WorkbenchGuide))
            WorkbenchGuide.GuideCompleted -= OnWorkbenchGuideCompleted;
        if (StoreGuide != null && IsInstanceValid(StoreGuide))
        {
            StoreGuide.TargetReached -= OnStoreTargetReached;
            StoreGuide.GuideCompleted -= OnStoreGuideCompleted;
        }
    }

    private void DisconnectMousy()
    {
        if (_activeMousy != null && IsInstanceValid(_activeMousy))
            _activeMousy.DialogueFinished -= OnMousyDialogueFinished;
    }

    private bool SourceIsDefeated(string mapId, string persistenceId) =>
        IsDefeatedState(GameState.Instance?.GetObjectState(mapId, persistenceId));

    private void LoadStates()
    {
        Dictionary f1 = GameState.Instance?.GetObjectState(GuideStateMapId, PostF1GuideStateId);
        _f1DialogueCompleted = ReadBool(f1, "dialogue_completed");
        _f1GuideCompleted = ReadBool(f1, "guide_completed");
        _f1Departed = ReadBool(f1, "departed");

        Dictionary f2 = GameState.Instance?.GetObjectState(GuideStateMapId, PostF2GuideStateId);
        _f2DialogueCompleted = ReadBool(f2, "dialogue_completed");
        _f2GuideCompleted = ReadBool(f2, "guide_completed");
    }

    private void SaveF1State()
    {
        GameState.Instance?.SetObjectState(GuideStateMapId, PostF1GuideStateId, new Dictionary
        {
            { "dialogue_completed", _f1DialogueCompleted },
            { "guide_completed", _f1GuideCompleted },
            { "departed", _f1Departed },
        });
    }

    private void SaveF2State()
    {
        GameState.Instance?.SetObjectState(GuideStateMapId, PostF2GuideStateId, new Dictionary
        {
            { "dialogue_completed", _f2DialogueCompleted },
            { "guide_completed", _f2GuideCompleted },
        });
    }

    private static bool ReadBool(Dictionary state, string key) =>
        state != null && state.TryGetValue(key, out Variant value) && value.AsBool();

    private static T FindDescendant<T>(Node root) where T : Node
    {
        if (root == null) return null;
        if (root is T match) return match;
        foreach (Node child in root.GetChildren())
        {
            T found = FindDescendant<T>(child);
            if (found != null) return found;
        }
        return null;
    }
}
