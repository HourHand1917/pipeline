using Godot;

/// <summary>
/// NPC 基类。在可互动基础上添加对话、任务等通用 NPC 功能。
/// </summary>
[GlobalClass]
public abstract partial class NPCBase : InteractableBase
{
    /// <summary>默认对话文本</summary>
    [Export] public string DefaultDialog { get; set; } = "";

    /// <summary>NPC 名字标签</summary>
    [Export] public string NpcName { get; set; } = "NPC";

<<<<<<< Updated upstream
=======
    [ExportGroup("Dialogic 对话")]
    /// <summary>把 Dialogic Timeline（.dtl）直接拖到这里。</summary>
    [Export(PropertyHint.ResourceType, "DialogicTimeline")]
    public Resource DialogueTimeline { get; set; }

    /// <summary>把该 NPC 对应的 Dialogic Character（.dch）拖到这里。</summary>
    [Export(PropertyHint.ResourceType, "DialogicCharacter")]
    public Resource DialogueCharacter { get; set; }

    /// <summary>Timeline 中代表玩家的 Dialogic Character（.dch）。</summary>
    [Export(PropertyHint.ResourceType, "DialogicCharacter")]
    public Resource PlayerDialogueCharacter { get; set; }

    /// <summary>
    /// 其他属于主角一侧的 Character，例如 reb。数组中的角色与玩家共用同一锚点和尾巴方向。
    /// </summary>
    [Export]
    public Godot.Collections.Array<Resource> AdditionalPlayerDialogueCharacters { get; set; } = new();

    /// <summary>气泡相对 NPC 原点的位置，默认在头顶。</summary>
    [Export] public Vector2 DialogueBubbleOffset { get; set; } = new(0, -96);

    /// <summary>玩家气泡相对玩家原点的位置；默认与 NPC 锚点等高。</summary>
    [Export] public Vector2 PlayerDialogueBubbleOffset { get; set; } = new(0, -160);

    /// <summary>Forced story conversations can disable ESC while ordinary NPCs keep it enabled.</summary>
    [Export] public bool AllowEscapeToExitDialogue { get; set; } = true;

    [ExportGroup("Dialogic 动画演出")]
    /// <summary>
    /// 可选 AnimationPlayer。Timeline 中使用 Dialogic Signal：animation:动画名，即可播放。
    /// 也支持字典信号 {"animation":"动画名"}。
    /// </summary>
    [Export] public AnimationPlayer DialogueAnimationPlayer { get; set; }

    public bool HasConfiguredDialogue => DialogueTimeline != null;
    public bool IsDialogueActive => _dialogueActive;

    private Node _dialogic;
    private GodotObject _dialogicText;
    private Node _dialogueCanvas;
    private Node2D _dialogueAnchor;
    private Node2D _playerDialogueAnchor;
    private Control _npcBubbleColumn;
    private Control _playerBubbleColumn;
    private Control _dialogueInputBlocker;
    private CanvasLayer _dialogueCanvasLayer;
    private PlayerController _lockedDialoguePlayer;
    private readonly List<Control> _allBubbles = new();
    private readonly Queue<bool> _pendingBubbleSides = new();
    private int _pendingRouteDelayFrames;
    private bool _choiceSpaceReserved;
    private bool _dialogueActive;
    private bool _dialogicSignalsConnected;
    private bool _movementLockedByDialogue;
    private bool _dialogueCanvasLayerRaised;
    private bool _dialogueCancelled;
    private bool _dialogueCancelSignalConnected;
    private int _dialogueCanvasOriginalLayer;
    private Callable _timelineEndedCallable;
    private Callable _timelineStartedCallable;
    private Callable _dialogicSignalCallable;
    private Callable _aboutToShowTextCallable;
    private Callable _dialogueCancelRequestedCallable;

    public override void _Ready()
    {
        base._Ready();
        EnsureDialogueAnchor();
        ConnectDialogicSignals();
    }

    public override void _ExitTree()
    {
        if (_dialogueOwner == this)
            _dialogueOwner = null;
        ExitDialogueModalState();
        DisconnectDialogueCanvasCancelSignal();
        DisconnectDialogicSignals();
        base._ExitTree();
    }

    public override void _Process(double delta)
    {
        if (!_dialogueActive)
            return;

        TryRoutePendingBubble();
        UpdateSpeakerColumnPositions();
        UpdateChoiceListPosition();
        UpdateChoiceReservation();
    }

>>>>>>> Stashed changes
    public override void HandleInteract()
    {
        if (!string.IsNullOrEmpty(DefaultDialog))
            GD.Print($"[{NpcName}] {DefaultDialog}");
        else
            base.HandleInteract();
    }
<<<<<<< Updated upstream
=======

    /// <summary>
    /// 启动 Inspector 中配置的 Dialogic Timeline。
    /// 返回 false 表示未配置、已有其他 Timeline 正在运行，或 Dialogic 不可用。
    /// </summary>
    protected bool TryStartConfiguredDialogue()
    {
        if (DialogueTimeline == null || _dialogueActive)
            return false;

        if (IsInstanceValid(_dialogueOwner) && _dialogueOwner != this)
            return false;

        if (!ConnectDialogicSignals())
            return false;

        Variant currentTimeline = _dialogic.Get("current_timeline");
        if (currentTimeline.VariantType != Variant.Type.Nil &&
            currentTimeline.AsGodotObject() != null)
            return false;

        _dialogueCanvas = EnsureDialogueCanvas();
        _dialogueAnchor = EnsureDialogueAnchor();
        _playerDialogueAnchor = EnsurePlayerDialogueAnchor();
        if (_dialogueCanvas == null || _dialogueAnchor == null || _playerDialogueAnchor == null)
            return false;

        ConnectDialogueCanvasCancelSignal();
        _dialogueCanvas.Set("allow_escape_to_exit", AllowEscapeToExitDialogue);

        // Canvas 仍是完全独立的场景；NPC 只提供锚点和 Timeline 配置。
        _dialogueCanvas.Call("set_dialogue_anchor", _dialogueAnchor);
        PrepareSpeakerColumns();
        EnterDialogueModalState();

        _dialogueOwner = this;
        _dialogueCancelled = false;
        _dialogueActive = true;
        EmitSignal(SignalName.DialogueStarted);

        // 避免用于开启对话的鼠标点击同时推进第一句。
        _dialogic.CallDeferred("start_timeline", DialogueTimeline);
        return true;
    }

    /// <summary>Scene controllers can start the already configured timeline without faking a mouse click.</summary>
    public bool StartConfiguredDialogueNow() => TryStartConfiguredDialogue();

    /// <summary>派生类在这里实现“对话结束后战斗/开店”等行为。</summary>
    protected virtual void OnDialogueCompleted()
    {
    }

    /// <summary>
    /// 玩家主动取消对话时调用。派生类只应清理尚未执行的对话后动作；
    /// 通用输入解锁、气泡清理与 DialogueFinished 仍由基类负责。
    /// </summary>
    protected virtual void OnDialogueCancelled()
    {
    }

    /// <summary>
    /// 派生类可读取 Dialogic Signal Event；动画指令已由基类先处理。
    /// </summary>
    protected virtual void OnDialogueSignalReceived(Variant argument)
    {
    }

    private bool ConnectDialogicSignals()
    {
        if (_dialogicSignalsConnected && IsInstanceValid(_dialogic))
            return true;

        _dialogic = GetNodeOrNull<Node>("/root/Dialogic");
        if (_dialogic == null)
        {
            if (DialogueTimeline != null)
                GD.PushError($"NPC「{NpcName}」找不到 Dialogic 自动加载。");
            return false;
        }

        _timelineEndedCallable = Callable.From(OnDialogicTimelineEnded);
        _timelineStartedCallable = Callable.From(OnDialogicTimelineStarted);
        _dialogicSignalCallable = Callable.From<Variant>(OnDialogicSignalEvent);
        _aboutToShowTextCallable =
            Callable.From<Godot.Collections.Dictionary>(OnDialogicTextAboutToShow);

        if (!_dialogic.IsConnected("timeline_started", _timelineStartedCallable))
            _dialogic.Connect("timeline_started", _timelineStartedCallable);
        if (!_dialogic.IsConnected("timeline_ended", _timelineEndedCallable))
            _dialogic.Connect("timeline_ended", _timelineEndedCallable);
        if (!_dialogic.IsConnected("signal_event", _dialogicSignalCallable))
            _dialogic.Connect("signal_event", _dialogicSignalCallable);

        _dialogicText = _dialogic.Get("Text").AsGodotObject();
        if (IsInstanceValid(_dialogicText) &&
            !_dialogicText.IsConnected("about_to_show_text", _aboutToShowTextCallable))
            _dialogicText.Connect("about_to_show_text", _aboutToShowTextCallable);

        _dialogicSignalsConnected = true;
        return true;
    }

    private void DisconnectDialogicSignals()
    {
        if (!_dialogicSignalsConnected || !IsInstanceValid(_dialogic))
            return;

        if (_dialogic.IsConnected("timeline_started", _timelineStartedCallable))
            _dialogic.Disconnect("timeline_started", _timelineStartedCallable);
        if (_dialogic.IsConnected("timeline_ended", _timelineEndedCallable))
            _dialogic.Disconnect("timeline_ended", _timelineEndedCallable);
        if (_dialogic.IsConnected("signal_event", _dialogicSignalCallable))
            _dialogic.Disconnect("signal_event", _dialogicSignalCallable);
        if (IsInstanceValid(_dialogicText) &&
            _dialogicText.IsConnected("about_to_show_text", _aboutToShowTextCallable))
            _dialogicText.Disconnect("about_to_show_text", _aboutToShowTextCallable);

        _dialogicSignalsConnected = false;
    }

    private void ConnectDialogueCanvasCancelSignal()
    {
        if (!IsInstanceValid(_dialogueCanvas) ||
            !_dialogueCanvas.HasSignal("dialogue_cancel_requested"))
            return;

        _dialogueCancelRequestedCallable = Callable.From(OnDialogueCancelRequested);
        if (!_dialogueCanvas.IsConnected(
                "dialogue_cancel_requested", _dialogueCancelRequestedCallable))
            _dialogueCanvas.Connect(
                "dialogue_cancel_requested", _dialogueCancelRequestedCallable);
        _dialogueCancelSignalConnected = true;
    }

    private void DisconnectDialogueCanvasCancelSignal()
    {
        if (!IsInstanceValid(_dialogueCanvas) ||
            !_dialogueCanvas.HasSignal("dialogue_cancel_requested") ||
            !_dialogueCancelSignalConnected)
            return;

        if (_dialogueCanvas.IsConnected(
                "dialogue_cancel_requested", _dialogueCancelRequestedCallable))
            _dialogueCanvas.Disconnect(
                "dialogue_cancel_requested", _dialogueCancelRequestedCallable);
        _dialogueCancelSignalConnected = false;
    }

    private void OnDialogueCancelRequested()
    {
        if (_dialogueActive && _dialogueOwner == this)
            _dialogueCancelled = true;
    }

    private Node EnsureDialogueCanvas()
    {
        Node canvas = GetTree().GetFirstNodeInGroup("npc_dialogue_canvas");
        if (canvas != null)
            return canvas;

        PackedScene canvasScene = GD.Load<PackedScene>(DialogueCanvasScenePath);
        if (canvasScene == null)
        {
            GD.PushError($"NPC「{NpcName}」无法加载独立对话 Canvas：{DialogueCanvasScenePath}");
            return null;
        }

        canvas = canvasScene.Instantiate();
        Node parent = GetTree().CurrentScene ?? GetTree().Root;
        parent.AddChild(canvas);
        return canvas;
    }

    private Node2D EnsureDialogueAnchor()
    {
        _dialogueAnchor ??= GetNodeOrNull<Node2D>(GeneratedAnchorName);
        if (_dialogueAnchor == null)
        {
            _dialogueAnchor = new Marker2D
            {
                Name = GeneratedAnchorName,
                Position = DialogueBubbleOffset,
            };
            AddChild(_dialogueAnchor);
        }

        _dialogueAnchor.Position = DialogueBubbleOffset;
        return _dialogueAnchor;
    }

    private Node2D EnsurePlayerDialogueAnchor()
    {
        PlayerController player = PlayerController.Instance;
        if (player == null)
        {
            GD.PushWarning($"NPC「{NpcName}」找不到 PlayerController，玩家气泡暂用 NPC 锚点。");
            return _dialogueAnchor;
        }

        _playerDialogueAnchor =
            player.GetNodeOrNull<Node2D>(GeneratedPlayerAnchorName);
        if (_playerDialogueAnchor == null)
        {
            _playerDialogueAnchor = new Marker2D
            {
                Name = GeneratedPlayerAnchorName,
                Position = PlayerDialogueBubbleOffset,
            };
            player.AddChild(_playerDialogueAnchor);
        }

        _playerDialogueAnchor.Position = PlayerDialogueBubbleOffset;
        return _playerDialogueAnchor;
    }

    /// <summary>
    /// 对话期间只锁定探索移动并拦截非对话鼠标输入。
    /// 不暂停 SceneTree，因此 Dialogic、气泡 Tween 和选项按钮仍可正常运行。
    /// </summary>
    private void EnterDialogueModalState()
    {
        if (!_movementLockedByDialogue)
        {
            _lockedDialoguePlayer = PlayerController.Instance;
            if (IsInstanceValid(_lockedDialoguePlayer))
            {
                _lockedDialoguePlayer.LockMovement();
                _movementLockedByDialogue = true;
            }
        }

        _dialogueCanvasLayer = _dialogueCanvas as CanvasLayer;
        if (IsInstanceValid(_dialogueCanvasLayer) && !_dialogueCanvasLayerRaised)
        {
            _dialogueCanvasOriginalLayer = _dialogueCanvasLayer.Layer;
            _dialogueCanvasLayer.Layer = Mathf.Max(
                _dialogueCanvasOriginalLayer, DialogueModalCanvasLayer);
            _dialogueCanvasLayerRaised = true;
        }

        Control root = _dialogueCanvas?.GetNodeOrNull<Control>("Root");
        if (root == null)
            return;

        _dialogueInputBlocker = root.GetNodeOrNull<Control>(DialogueInputBlockerName);
        if (_dialogueInputBlocker == null)
        {
            _dialogueInputBlocker = new Control
            {
                Name = DialogueInputBlockerName,
                MouseFilter = Control.MouseFilterEnum.Stop,
                FocusMode = Control.FocusModeEnum.None,
            };
            _dialogueInputBlocker.AnchorLeft = 0.0f;
            _dialogueInputBlocker.AnchorTop = 0.0f;
            _dialogueInputBlocker.AnchorRight = 1.0f;
            _dialogueInputBlocker.AnchorBottom = 1.0f;
            _dialogueInputBlocker.OffsetLeft = 0.0f;
            _dialogueInputBlocker.OffsetTop = 0.0f;
            _dialogueInputBlocker.OffsetRight = 0.0f;
            _dialogueInputBlocker.OffsetBottom = 0.0f;
            root.AddChild(_dialogueInputBlocker);
        }

        _dialogueInputBlocker.MouseFilter = Control.MouseFilterEnum.Stop;
        _dialogueInputBlocker.Show();
        // 放到最底层：覆盖整个屏幕以拦截世界/UI，气泡与官方 ChoiceButton 仍在其上方。
        root.MoveChild(_dialogueInputBlocker, 0);
    }

    private void ExitDialogueModalState()
    {
        if (IsInstanceValid(_dialogueInputBlocker))
            _dialogueInputBlocker.QueueFree();
        _dialogueInputBlocker = null;

        if (_dialogueCanvasLayerRaised && IsInstanceValid(_dialogueCanvasLayer))
            _dialogueCanvasLayer.Layer = _dialogueCanvasOriginalLayer;
        _dialogueCanvasLayerRaised = false;
        _dialogueCanvasLayer = null;

        if (_movementLockedByDialogue && IsInstanceValid(_lockedDialoguePlayer))
            _lockedDialoguePlayer.UnlockMovement();
        _movementLockedByDialogue = false;
        _lockedDialoguePlayer = null;
    }

    private void OnDialogicTimelineEnded()
    {
        // Dialogic 是全局插件；只有真正启动了本次对话的 NPC 才响应结束事件。
        if (!_dialogueActive)
            return;

        bool wasCancelled = _dialogueCancelled;
        _dialogueCancelled = false;
        _dialogueActive = false;
        if (_dialogueOwner == this)
            _dialogueOwner = null;
        ExitDialogueModalState();
        ClearRoutedBubbles();
        _pendingBubbleSides.Clear();
        _pendingRouteDelayFrames = 0;
        _choiceSpaceReserved = false;
        EmitSignal(SignalName.DialogueFinished);

        if (wasCancelled)
        {
            OnDialogueCancelled();
            return;
        }

        // 让 Dialogic 与气泡 Canvas 先完成 timeline_ended 清理，再切战斗或打开商店。
        Callable.From(OnDialogueCompleted).CallDeferred();
    }

    private void OnDialogicTimelineStarted()
    {
        if (!_dialogueActive)
            return;

        // Canvas 会先执行自己的开始布局；下一空闲帧再把选项气泡放回 NPC 附近。
        Callable.From(ConfigureChoiceBubblesNearNpc).CallDeferred();
    }

    private void OnDialogicSignalEvent(Variant argument)
    {
        if (!_dialogueActive)
            return;

        TryPlayDialogueAnimation(argument);
        EmitSignal(SignalName.DialoguePluginSignal, argument);
        OnDialogueSignalReceived(argument);
    }

    private void OnDialogicTextAboutToShow(Godot.Collections.Dictionary info)
    {
        if (!_dialogueActive || _dialogueOwner != this)
            return;

        if (info.TryGetValue("append", out Variant append) && append.AsBool())
            return;

        bool playerSide = info.TryGetValue("character", out Variant character) &&
                          IsPlayerSideCharacter(character);
        _pendingBubbleSides.Enqueue(playerSide);
        if (_pendingBubbleSides.Count == 1)
            _pendingRouteDelayFrames = 2;
    }

    private static bool MatchesCharacter(Variant character, Resource configuredCharacter)
    {
        if (configuredCharacter == null || character.VariantType != Variant.Type.Object)
            return false;

        GodotObject actualCharacter = character.AsGodotObject();
        if (!IsInstanceValid(actualCharacter))
            return false;
        if (actualCharacter.GetInstanceId() == configuredCharacter.GetInstanceId())
            return true;

        return actualCharacter is Resource actualResource &&
               !string.IsNullOrEmpty(actualResource.ResourcePath) &&
               actualResource.ResourcePath == configuredCharacter.ResourcePath;
    }

    private bool IsPlayerSideCharacter(Variant character)
    {
        if (MatchesCharacter(character, PlayerDialogueCharacter))
            return true;

        if (AdditionalPlayerDialogueCharacters == null)
            return false;

        foreach (Resource configuredCharacter in AdditionalPlayerDialogueCharacters)
        {
            if (MatchesCharacter(character, configuredCharacter))
                return true;
        }

        return false;
    }

    private void PrepareSpeakerColumns()
    {
        Control root = _dialogueCanvas?.GetNodeOrNull<Control>("Root");
        if (root == null)
            return;

        _npcBubbleColumn = EnsureBubbleColumn(root, NpcBubbleColumnName);
        _playerBubbleColumn = EnsureBubbleColumn(root, PlayerBubbleColumnName);
        ClearRoutedBubbles();
        _pendingBubbleSides.Clear();
        _pendingRouteDelayFrames = 0;
        _choiceSpaceReserved = false;
        UpdateSpeakerColumnPositions();
    }

    private static Control EnsureBubbleColumn(Control root, string name)
    {
        Control column = root.GetNodeOrNull<Control>(name);
        if (column != null)
            return column;

        column = new Control
        {
            Name = name,
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ZIndex = 2,
        };
        root.AddChild(column);
        return column;
    }

    private void RouteNewestBubble(bool playerSide)
    {
        if (!_dialogueActive || !IsInstanceValid(_dialogueCanvas))
            return;

        Control sourceStack = _dialogueCanvas.GetNodeOrNull<Control>("Root/BubbleStack");
        Control targetColumn = playerSide ? _playerBubbleColumn : _npcBubbleColumn;
        if (sourceStack == null || targetColumn == null || sourceStack.GetChildCount() == 0)
            return;

        Control bubble = sourceStack.GetChild(sourceStack.GetChildCount() - 1) as Control;
        if (bubble == null)
            return;

        // 终止 Canvas 的单列布局 Tween，随后只沿各自锚点的 Y 负方向排布。
        Variant layoutTweenValue = _dialogueCanvas.Get("_layout_tween");
        if (layoutTweenValue.VariantType == Variant.Type.Object &&
            layoutTweenValue.AsGodotObject() is Tween layoutTween &&
            IsInstanceValid(layoutTween))
            layoutTween.Kill();

        bubble.Reparent(targetColumn, false);
        bubble.SetMeta("dialogue_player_side", playerSide);

        Variant canvasBubblesValue = _dialogueCanvas.Get("_bubbles");
        if (canvasBubblesValue.VariantType == Variant.Type.Array)
        {
            Godot.Collections.Array canvasBubbles = canvasBubblesValue.AsGodotArray();
            canvasBubbles.Remove(bubble);
            _dialogueCanvas.Set("_bubbles", canvasBubbles);
        }
        _allBubbles.RemoveAll(item => !IsInstanceValid(item));

        float spacing = ReadCanvasFloat("bubble_spacing", 12.0f);
        float width = ReadCanvasFloat("bubble_width", 520.0f);
        float tweenTime = ReadCanvasFloat("bubble_tween_time", 0.22f);
        float height = Mathf.Max(bubble.GetCombinedMinimumSize().Y, bubble.Size.Y);
        if (height < 1.0f)
            height = 82.0f;

        Tween riseTween = CreateTween().SetParallel(true)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        float riseDistance = height + spacing;
        foreach (Control historyBubble in _allBubbles)
        {
            Vector2 oldPosition = historyBubble.Position;
            Vector2 targetPosition = new(oldPosition.X, oldPosition.Y - riseDistance);
            riseTween.TweenProperty(historyBubble, "position", targetPosition, tweenTime);
        }

        Vector2 bubbleTarget = new(-width * 0.5f, -height);
        bubble.Position = bubbleTarget + new Vector2(0, 18.0f);
        Color startModulate = bubble.Modulate;
        bubble.Modulate = new Color(startModulate.R, startModulate.G, startModulate.B, 0.0f);
        riseTween.TweenProperty(bubble, "position", bubbleTarget, tweenTime);
        riseTween.TweenProperty(bubble, "modulate:a", 1.0f, tweenTime);
        _allBubbles.Add(bubble);

        int maxVisible = Mathf.Max(1, (int)ReadCanvasFloat("max_visible_bubbles", 6.0f));
        while (_allBubbles.Count > maxVisible)
        {
            Control oldest = _allBubbles[0];
            _allBubbles.RemoveAt(0);
            if (IsInstanceValid(oldest))
                oldest.QueueFree();
        }

        RefreshBubbleTails();
    }

    private void TryRoutePendingBubble()
    {
        if (_pendingBubbleSides.Count == 0 || !IsInstanceValid(_dialogueCanvas))
            return;

        if (_pendingRouteDelayFrames > 0)
        {
            _pendingRouteDelayFrames--;
            return;
        }

        Control sourceStack = _dialogueCanvas.GetNodeOrNull<Control>("Root/BubbleStack");
        if (sourceStack == null || sourceStack.GetChildCount() == 0)
            return;

        bool playerSide = _pendingBubbleSides.Dequeue();
        RouteNewestBubble(playerSide);
        if (_pendingBubbleSides.Count > 0)
            _pendingRouteDelayFrames = 2;
    }

    private float ReadCanvasFloat(string property, float fallback)
    {
        if (!IsInstanceValid(_dialogueCanvas))
            return fallback;
        Variant value = _dialogueCanvas.Get(property);
        return value.VariantType is Variant.Type.Float or Variant.Type.Int
            ? (float)value.AsDouble()
            : fallback;
    }

    private void UpdateSpeakerColumnPositions()
    {
        if (!IsInstanceValid(_npcBubbleColumn) || !IsInstanceValid(_playerBubbleColumn))
            return;

        if (IsInstanceValid(_dialogueAnchor))
        {
            Vector2 npcPosition = _dialogueAnchor.GetGlobalTransformWithCanvas().Origin;
            Vector2 playerPosition = IsInstanceValid(_playerDialogueAnchor)
                ? _playerDialogueAnchor.GetGlobalTransformWithCanvas().Origin
                : npcPosition;

            // 两列使用同一条基线；X 各跟随角色，Y 取更高的头顶锚点避免压住人物。
            float commonY = Mathf.Min(npcPosition.Y, playerPosition.Y);
            _npcBubbleColumn.Position = new Vector2(npcPosition.X, commonY);
            _playerBubbleColumn.Position = new Vector2(playerPosition.X, commonY);
        }

        RefreshBubbleTails();
    }

    private void RefreshBubbleTails()
    {
        if (!IsInstanceValid(_npcBubbleColumn) || !IsInstanceValid(_playerBubbleColumn))
            return;

        // 微信式左右关系：位于左侧的说话者尾巴在左，位于右侧时尾巴在右。
        bool playerIsLeft = _playerBubbleColumn.Position.X <= _npcBubbleColumn.Position.X;
        float configuredWidth = ReadCanvasFloat("bubble_width", 520.0f);
        foreach (Control bubble in _allBubbles)
        {
            if (!IsInstanceValid(bubble) || !bubble.HasMeta("dialogue_player_side"))
                continue;

            bool playerSide = bubble.GetMeta("dialogue_player_side").AsBool();
            bool tailOnLeft = playerSide ? playerIsLeft : !playerIsLeft;
            float width = Mathf.Max(1.0f, bubble.Size.X);
            if (width <= 1.0f)
                width = configuredWidth;
            SetBubbleTailSide(bubble, tailOnLeft, width, 34.0f);
        }

        RefreshChoiceBubbleTails(playerIsLeft);
    }

    private void RefreshChoiceBubbleTails(bool playerIsLeft)
    {
        Control choiceList = _dialogueCanvas?.GetNodeOrNull<Control>("Root/ChoiceList");
        if (choiceList == null)
            return;

        float configuredWidth = Mathf.Max(220.0f,
            ReadCanvasFloat("bubble_width", 520.0f));
        foreach (Node child in choiceList.GetChildren())
        {
            if (child is not Control choice)
                continue;
            float width = Mathf.Max(1.0f, choice.Size.X);
            if (width <= 1.0f)
                width = configuredWidth;
            SetBubbleTailSide(choice, playerIsLeft, width, 30.0f);
        }
    }

    private static void SetBubbleTailSide(
        Control bubble, bool tailOnLeft, float width, float edgeInset)
    {
        Polygon2D tail = bubble.GetNodeOrNull<Polygon2D>("BubbleTail");
        if (tail == null)
            return;

        float scaleX = Mathf.Max(0.001f, Mathf.Abs(tail.Scale.X));
        tail.Scale = new Vector2(tailOnLeft ? scaleX : -scaleX, tail.Scale.Y);
        tail.Position = new Vector2(
            tailOnLeft ? edgeInset : Mathf.Max(edgeInset, width - edgeInset),
            Mathf.Max(0.0f, bubble.Size.Y - 2.0f));
    }

    private void ClearRoutedBubbles()
    {
        foreach (Control bubble in _allBubbles)
        {
            if (IsInstanceValid(bubble))
                bubble.QueueFree();
        }
        _allBubbles.Clear();
    }

    private void UpdateChoiceReservation()
    {
        if (!IsInstanceValid(_dialogueCanvas))
            return;

        Control choiceList = _dialogueCanvas.GetNodeOrNull<Control>("Root/ChoiceList");
        bool choicesVisible = choiceList != null && choiceList.Modulate.A > 0.01f;
        if (!choicesVisible)
        {
            _choiceSpaceReserved = false;
            return;
        }

        if (_choiceSpaceReserved)
            return;
        _choiceSpaceReserved = true;

        float totalHeight = 0.0f;
        int visibleCount = 0;
        foreach (Node child in choiceList.GetChildren())
        {
            if (child is not Control choice || !choice.Visible)
                continue;
            totalHeight += Mathf.Max(choice.GetCombinedMinimumSize().Y, choice.Size.Y);
            visibleCount++;
        }

        if (visibleCount > 1)
            totalHeight += (visibleCount - 1) * 12.0f;
        if (totalHeight <= 0.0f)
            return;

        ShiftAllHistoryUp(totalHeight + ReadCanvasFloat("bubble_spacing", 12.0f));
    }

    private void ShiftAllHistoryUp(float distance)
    {
        _allBubbles.RemoveAll(item => !IsInstanceValid(item));
        if (_allBubbles.Count == 0 || distance <= 0.0f)
            return;

        float tweenTime = ReadCanvasFloat("bubble_tween_time", 0.22f);
        Tween tween = CreateTween().SetParallel(true)
            .SetTrans(Tween.TransitionType.Quad)
            .SetEase(Tween.EaseType.Out);
        foreach (Control bubble in _allBubbles)
        {
            Vector2 oldPosition = bubble.Position;
            tween.TweenProperty(bubble, "position",
                new Vector2(oldPosition.X, oldPosition.Y - distance), tweenTime);
        }
    }

    private void TryPlayDialogueAnimation(Variant argument)
    {
        if (DialogueAnimationPlayer == null)
            return;

        string animationName = "";
        if (argument.VariantType == Variant.Type.String ||
            argument.VariantType == Variant.Type.StringName)
        {
            string command = argument.AsString();
            const string prefix = "animation:";
            if (command.StartsWith(prefix, System.StringComparison.OrdinalIgnoreCase))
                animationName = command[prefix.Length..].Trim();
        }
        else if (argument.VariantType == Variant.Type.Dictionary)
        {
            Godot.Collections.Dictionary values = argument.AsGodotDictionary();
            if (values.TryGetValue("animation", out Variant animationValue))
                animationName = animationValue.AsString().Trim();
        }

        if (string.IsNullOrEmpty(animationName))
            return;

        StringName animation = animationName;
        if (DialogueAnimationPlayer.HasAnimation(animation))
            DialogueAnimationPlayer.Play(animation);
        else
            GD.PushWarning($"NPC「{NpcName}」的 AnimationPlayer 中不存在动画「{animationName}」。");
    }

    private void ConfigureChoiceBubblesNearNpc()
    {
        if (!_dialogueActive || !IsInstanceValid(_dialogueCanvas) || !IsInstanceValid(_dialogueAnchor))
            return;

        Control choiceList = _dialogueCanvas.GetNodeOrNull<Control>("Root/ChoiceList");
        if (choiceList == null)
            return;

        float width = 520.0f;
        Variant configuredWidth = _dialogueCanvas.Get("bubble_width");
        if (configuredWidth.VariantType == Variant.Type.Float ||
            configuredWidth.VariantType == Variant.Type.Int)
            width = Mathf.Max(220.0f, (float)configuredWidth.AsDouble());

        UpdateChoiceListPosition(choiceList, width);
        ApplyDialogueBubbleFrameToChoices(choiceList, width);
    }

    private void UpdateChoiceListPosition()
    {
        if (!_dialogueActive || !IsInstanceValid(_dialogueCanvas))
            return;

        Control choiceList = _dialogueCanvas.GetNodeOrNull<Control>("Root/ChoiceList");
        if (choiceList == null)
            return;

        float width = Mathf.Max(220.0f, ReadCanvasFloat("bubble_width", 520.0f));
        UpdateChoiceListPosition(choiceList, width);
    }

    private void UpdateChoiceListPosition(Control choiceList, float width)
    {
        Vector2 viewportSize = GetViewportRect().Size;
        Node2D choiceAnchor = IsInstanceValid(_playerDialogueAnchor)
            ? _playerDialogueAnchor
            : _dialogueAnchor;
        if (!IsInstanceValid(choiceAnchor))
            return;

        Vector2 anchorScreen = choiceAnchor.GetGlobalTransformWithCanvas().Origin;
        if (IsInstanceValid(_playerBubbleColumn))
            anchorScreen.Y = _playerBubbleColumn.Position.Y;
        const float margin = 24.0f;
        const float choiceAreaHeight = 540.0f;
        float left = Mathf.Clamp(anchorScreen.X - width * 0.5f, margin,
            Mathf.Max(margin, viewportSize.X - margin - width));
        float maximumBottom = Mathf.Max(margin, viewportSize.Y - margin);
        float minimumBottom = Mathf.Min(margin + 120.0f, maximumBottom);
        float bottom = Mathf.Clamp(anchorScreen.Y - 30.0f, minimumBottom,
            maximumBottom);
        float top = Mathf.Max(margin, bottom - choiceAreaHeight);

        choiceList.AnchorLeft = 0;
        choiceList.AnchorTop = 0;
        choiceList.AnchorRight = 0;
        choiceList.AnchorBottom = 0;
        choiceList.OffsetLeft = left;
        choiceList.OffsetTop = top;
        choiceList.OffsetRight = left + width;
        choiceList.OffsetBottom = bottom;
    }

    private static void ApplyDialogueBubbleFrameToChoices(Control choiceList, float width)
    {
        PackedScene bubbleScene = GD.Load<PackedScene>(DialogueBubbleScenePath);
        PanelContainer sampleBubble = bubbleScene?.Instantiate<PanelContainer>();
        StyleBox baseFrame = sampleBubble?.GetThemeStylebox("panel");
        if (baseFrame == null)
        {
            sampleBubble?.Free();
            return;
        }

        Color textColor = new("e6fbff");
        Color tailColor = new("21d1f5");
        foreach (Node child in choiceList.GetChildren())
        {
            if (child is not Button choice)
                continue;

            choice.CustomMinimumSize = new Vector2(width,
                Mathf.Max(82.0f, choice.CustomMinimumSize.Y));

            StyleBox normal = (StyleBox)baseFrame.Duplicate();
            StyleBox hover = (StyleBox)baseFrame.Duplicate();
            StyleBox pressed = (StyleBox)baseFrame.Duplicate();
            StyleBox disabled = (StyleBox)baseFrame.Duplicate();

            if (hover is StyleBoxFlat hoverFlat)
            {
                hoverFlat.BgColor = new Color("061f2a");
                hoverFlat.BorderColor = new Color("59eaff");
            }
            if (pressed is StyleBoxFlat pressedFlat)
            {
                pressedFlat.BgColor = new Color("0a3947");
                pressedFlat.BorderColor = new Color("d8fbff");
            }
            if (disabled is StyleBoxFlat disabledFlat)
            {
                disabledFlat.BgColor = new Color("07151a");
                disabledFlat.BorderColor = new Color("28505a");
            }

            choice.AddThemeStyleboxOverride("normal", normal);
            choice.AddThemeStyleboxOverride("hover", hover);
            choice.AddThemeStyleboxOverride("focus", hover);
            choice.AddThemeStyleboxOverride("pressed", pressed);
            choice.AddThemeStyleboxOverride("disabled", disabled);
            choice.AddThemeColorOverride("font_color", textColor);
            choice.AddThemeColorOverride("font_hover_color", textColor);
            choice.AddThemeColorOverride("font_focus_color", textColor);
            choice.AddThemeColorOverride("font_pressed_color", textColor);

            choice.GetNodeOrNull<RichTextLabel>("Content/Row/ChoiceText")?
                .AddThemeColorOverride("default_color", textColor);
            Polygon2D tail = choice.GetNodeOrNull<Polygon2D>("BubbleTail");
            if (tail != null)
                tail.Color = tailColor;
        }

        sampleBubble.Free();
    }
>>>>>>> Stashed changes
}
