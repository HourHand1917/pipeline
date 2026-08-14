using Godot;
using Godot.Collections;

/// <summary>
/// Drives the four playable battles. Core-00's hands and body are two waves of
/// the same fourth battle, so only that transition skips the build screen and
/// preserves player/card runtime state.
/// </summary>
[GlobalClass]
public partial class CombatCampaignController : Node
{
    [Signal] public delegate void CampaignStateChangedEventHandler(int state);
    [Signal] public delegate void WaveChangedEventHandler(int battleNumber, int waveNumber, string displayName);
    [Signal] public delegate void CampaignCompletedEventHandler();

    public enum CampaignState { NotStarted, Preparing, Active, Failed, Completed }

    [Export] public Resource CampaignData { get; set; }
    [Export(PropertyHint.Range, "0,5,0.05")] public float TransitionDelaySeconds { get; set; } = 0.65f;

    public int CurrentWaveIndex { get; private set; } = -1;
    public CampaignState State { get; private set; } = CampaignState.NotStarted;
    public int CurrentBattleNumber => ReadInt(CurrentWave, "battle_number", 0);
    public int CurrentWaveNumber => ReadInt(CurrentWave, "wave_number", 0);
    public string CurrentDisplayName => ReadString(CurrentWave, "display_name", "Battle");
    public Resource CurrentRules => ReadResource(CurrentWave, "game_rules");
    public bool PreservePlayerState => ReadBool(CurrentWave, "preserve_player_state", false);
    public bool PreserveBoardRuntimeState => ReadBool(CurrentWave, "preserve_board_runtime_state", false);

    /// <summary>Read-only progress values used by the HUD/debug panel.</summary>
    public int CompletedBattleCount
    {
        get
        {
            if (CurrentWaveIndex < 0) return 0;
            int currentBattle = CurrentBattleNumber;
            if (State == CampaignState.Completed) return 4;
            return Mathf.Max(0, currentBattle - 1);
        }
    }

    public string ProgressText => State == CampaignState.Completed
        ? "四场战斗完成"
        : $"第 {Mathf.Max(1, CurrentBattleNumber)} / 4 战｜阶段 {Mathf.Max(1, CurrentWaveNumber)}｜{CurrentDisplayName}";

    private idk _host;
    private GodotObject _campaign;
    private bool _transitioning;

    private GodotObject CurrentWave => GetWave(CurrentWaveIndex);

    public Resource PeekInitialRules()
    {
        EnsureCampaign();
        return ReadResource(GetWave(0), "game_rules");
    }

    public void Bind(idk host)
    {
        _host = host;
        EnsureCampaign();
    }

    public void InitializeCampaign()
    {
        if (_host == null || !EnsureCampaign())
        {
            GD.PushError("CombatCampaignController: campaign/host is not configured.");
            return;
        }

        CurrentWaveIndex = 0;
        SetState(CampaignState.Preparing);
        PrepareCurrentWave(false, $"准备 {CurrentDisplayName}。可先调整构筑，然后开始战斗。");
    }

    public void NotifyWaveStarted()
    {
        _transitioning = false;
        SetState(CampaignState.Active);
        EmitSignal(SignalName.WaveChanged, CurrentBattleNumber, CurrentWaveNumber, CurrentDisplayName);
    }

    public async void HandleBattleEnded(bool playerWon)
    {
        if (_transitioning || State != CampaignState.Active) return;
        _transitioning = true;

        if (!playerWon)
        {
            SetState(CampaignState.Failed);
            await DelayTransition();
            _transitioning = false;
            PrepareCurrentWave(false, $"{CurrentDisplayName} 失败。调整构筑后点击开始即可重试本场。");
            return;
        }

        var nextWave = GetWave(CurrentWaveIndex + 1);
        if (nextWave == null)
        {
            SetState(CampaignState.Completed);
            _transitioning = false;
            _host.ShowCampaignCompletion("四场战斗全部完成：Boom → Rocky + Boom → Sharkk → Core-00！");
            EmitSignal(SignalName.CampaignCompleted);
            return;
        }

        int defeatedBattle = CurrentBattleNumber;
        int nextBattle = ReadInt(nextWave, "battle_number", defeatedBattle + 1);
        bool sameBattleNextWave = nextBattle == defeatedBattle;
        CurrentWaveIndex++;
        await DelayTransition();

        if (sameBattleNextWave)
        {
            // Core-00 phase one -> phase two: no build break and no free heal.
            PrepareCurrentWave(true, $"{CurrentDisplayName} 启动。玩家生命、护盾、点亮与冷却全部保留。");
        }
        else
        {
            SetState(CampaignState.Preparing);
            PrepareCurrentWave(false, $"第 {defeatedBattle} 战胜利。下一场：{CurrentDisplayName}。可调整构筑。");
        }
        _transitioning = false;
    }

    public void RetryCurrentBattle()
    {
        if (_host == null || CurrentWave == null) return;
        _transitioning = false;
        SetState(CampaignState.Preparing);
        PrepareCurrentWave(false, $"重试 {CurrentDisplayName}。可先调整构筑。");
    }

    public void RestartCampaign()
    {
        if (_host == null || !EnsureCampaign()) return;
        _transitioning = false;
        CurrentWaveIndex = 0;
        SetState(CampaignState.Preparing);
        PrepareCurrentWave(false, $"战役已重置。准备 {CurrentDisplayName}。");
    }

    private void PrepareCurrentWave(bool startImmediately, string message)
    {
        var rules = CurrentRules;
        if (rules == null)
        {
            GD.PushError($"CombatCampaignController: wave {CurrentWaveIndex} has no GameRules.");
            return;
        }

        _host.PrepareCampaignWave(
            rules,
            PreservePlayerState,
            PreserveBoardRuntimeState,
            startImmediately,
            message);
    }

    private async System.Threading.Tasks.Task DelayTransition()
    {
        if (TransitionDelaySeconds <= 0.0f || !IsInsideTree()) return;
        await ToSignal(GetTree().CreateTimer(TransitionDelaySeconds), SceneTreeTimer.SignalName.Timeout);
    }

    private bool EnsureCampaign()
    {
        _campaign ??= CampaignData as GodotObject;
        if (_campaign == null) return false;
        if (_campaign.HasMethod("is_configuration_valid") && !_campaign.Call("is_configuration_valid").AsBool())
        {
            GD.PushError("CombatCampaignController: campaign data is invalid.");
            return false;
        }
        return true;
    }

    private GodotObject GetWave(int index)
    {
        if (!EnsureCampaign()) return null;
        var waves = _campaign.Get("waves").As<Array>();
        if (waves == null || index < 0 || index >= waves.Count) return null;
        return waves[index].As<GodotObject>();
    }

    private void SetState(CampaignState value)
    {
        if (State == value) return;
        State = value;
        EmitSignal(SignalName.CampaignStateChanged, (int)value);
    }

    private static int ReadInt(GodotObject obj, StringName key, int fallback) => obj == null ? fallback : obj.Get(key).AsInt32();
    private static bool ReadBool(GodotObject obj, StringName key, bool fallback) => obj == null ? fallback : obj.Get(key).AsBool();
    private static string ReadString(GodotObject obj, StringName key, string fallback) => obj == null ? fallback : obj.Get(key).AsString();
    private static Resource ReadResource(GodotObject obj, StringName key) => obj?.Get(key).As<Resource>();
}
