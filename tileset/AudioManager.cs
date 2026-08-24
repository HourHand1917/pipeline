using Godot;

public partial class AudioManager : Node
{
    public enum Bus { MASTER, MUSIC, SFX, AMBIENT }

    private const string MASTER_BUS = "Master";
    private const string MUSIC_BUS = "Music";
    private const string SFX_BUS = "SFX";
    private const string AMBIENT_BUS = "Ambient";

    private const int MUSIC_PLAYER_COUNT = 2;
    private const int SFX_PLAYER_COUNT = 6;
    private const int AMBIENT_PLAYER_COUNT = 2;
    private const float AMBIENT_VOLUME_DB = -20f;

    private const string UI_CLICK_PATH = "res://tileset/music_resource/OGG/SFX/UI/UI_Click.ogg";
    private const string UI_HOVER_PATH = "res://tileset/music_resource/OGG/SFX/UI/UI_Hover.ogg";
    private const string UI_INVALID_PATH = "res://tileset/music_resource/OGG/SFX/UI/UI_Invalid.ogg";
    private const string UI_SUCCESS_PATH = "res://tileset/music_resource/OGG/SFX/UI/UI_Success.ogg";
    private const string SHOP_BUY_SUCCESS_PATH = "res://tileset/music_resource/OGG/SFX/UI/shop_buy_success.ogg";

    private AudioStreamPlayer[] _musicPlayers;
    private AudioStreamPlayer[] _sfxPlayers;
    private AudioStreamPlayer[] _ambientPlayers;

    private AudioStream _uiClickSound;
    private AudioStream _uiHoverSound;
    private AudioStream _uiInvalidSound;
    private AudioStream _uiSuccessSound;
    private AudioStream _shopBuySuccessSound;

    private int _activeMusicIndex = 0;
    private int _activeAmbientIndex = 0;
    private int _fadingOutMusicIndex = -1;
    private int _fadingOutAmbientIndex = -1;

    private float _fadeDuration = 1.2f;
    private float _fadeTimer = 0f;
    private bool _isFading = false;

    public override void _Ready()
    {
        GD.Print("AudioManager 已成功加载并挂载到根节点。");
        InitializeMusicPlayers();
        InitializeSfxPlayers();
        InitializeAmbientPlayers();
        LoadUiSounds();
    }

    private void InitializeMusicPlayers()
    {
        _musicPlayers = new AudioStreamPlayer[MUSIC_PLAYER_COUNT];
        for (int i = 0; i < MUSIC_PLAYER_COUNT; i++)
        {
            var player = new AudioStreamPlayer
            {
                Name = $"MusicPlayer{i}",
                Bus = MUSIC_BUS,
                ProcessMode = ProcessModeEnum.Always
            };
            AddChild(player);
            _musicPlayers[i] = player;
        }
    }

    private void InitializeSfxPlayers()
    {
        _sfxPlayers = new AudioStreamPlayer[SFX_PLAYER_COUNT];
        for (int i = 0; i < SFX_PLAYER_COUNT; i++)
        {
            var player = new AudioStreamPlayer
            {
                Name = $"SfxPlayer{i}",
                Bus = SFX_BUS
            };
            AddChild(player);
            _sfxPlayers[i] = player;
        }
    }

    private void InitializeAmbientPlayers()
    {
        _ambientPlayers = new AudioStreamPlayer[AMBIENT_PLAYER_COUNT];
        for (int i = 0; i < AMBIENT_PLAYER_COUNT; i++)
        {
            var player = new AudioStreamPlayer
            {
                Name = $"AmbientPlayer{i}",
                Bus = AMBIENT_BUS,
                VolumeDb = AMBIENT_VOLUME_DB,
                ProcessMode = ProcessModeEnum.Always
            };
            AddChild(player);
            _ambientPlayers[i] = player;
        }
    }

    private void LoadUiSounds()
    {
        _uiClickSound = GD.Load<AudioStream>(UI_CLICK_PATH);
        _uiHoverSound = GD.Load<AudioStream>(UI_HOVER_PATH);
        _uiInvalidSound = GD.Load<AudioStream>(UI_INVALID_PATH);
        _uiSuccessSound = GD.Load<AudioStream>(UI_SUCCESS_PATH);
        _shopBuySuccessSound = GD.Load<AudioStream>(SHOP_BUY_SUCCESS_PATH);
    }

    public override void _Process(double delta)
    {
        if (!_isFading) return;

        _fadeTimer += (float)delta;
        float t = Mathf.Clamp(_fadeTimer / _fadeDuration, 0f, 1f);

        if (_fadingOutMusicIndex >= 0)
        {
            _musicPlayers[_fadingOutMusicIndex].VolumeDb = Mathf.Lerp(0f, -40f, t);
        }

        if (_fadingOutAmbientIndex >= 0)
        {
            _ambientPlayers[_fadingOutAmbientIndex].VolumeDb = Mathf.Lerp(AMBIENT_VOLUME_DB, -40f, t);
        }

        var activeMusic = _musicPlayers[_activeMusicIndex];
        if (activeMusic.Playing)
        {
            activeMusic.VolumeDb = Mathf.Lerp(-40f, 0f, t);
        }

        var activeAmbient = _ambientPlayers[_activeAmbientIndex];
        if (activeAmbient.Playing)
        {
            activeAmbient.VolumeDb = Mathf.Lerp(-40f, AMBIENT_VOLUME_DB, t);
        }

        if (t >= 1f)
        {
            if (_fadingOutMusicIndex >= 0)
            {
                _musicPlayers[_fadingOutMusicIndex].Stop();
                _musicPlayers[_fadingOutMusicIndex].Stream = null;
                _musicPlayers[_fadingOutMusicIndex].VolumeDb = 0f;
                _fadingOutMusicIndex = -1;
            }

            if (_fadingOutAmbientIndex >= 0)
            {
                _ambientPlayers[_fadingOutAmbientIndex].Stop();
                _ambientPlayers[_fadingOutAmbientIndex].Stream = null;
                _ambientPlayers[_fadingOutAmbientIndex].VolumeDb = AMBIENT_VOLUME_DB;
                _fadingOutAmbientIndex = -1;
            }

            _isFading = false;
        }
    }

    // ================================================================
    //  音乐
    // ================================================================

    public void PlayMusicWithFade(AudioStream music, float fadeDuration = 1.2f)
    {
        if (music == null) return;

        var current = _musicPlayers[_activeMusicIndex];
        if (current.Stream == music && current.Playing) return;

        if (_fadingOutMusicIndex >= 0)
        {
            _musicPlayers[_fadingOutMusicIndex].Stop();
            _musicPlayers[_fadingOutMusicIndex].Stream = null;
            _musicPlayers[_fadingOutMusicIndex].VolumeDb = 0f;
        }

        if (current.Playing)
            _fadingOutMusicIndex = _activeMusicIndex;

        _activeMusicIndex = (_activeMusicIndex + 1) % MUSIC_PLAYER_COUNT;
        var newPlayer = _musicPlayers[_activeMusicIndex];
        newPlayer.Stream = music;
        newPlayer.VolumeDb = -40f;
        newPlayer.Play();

        _fadeDuration = fadeDuration;
        _fadeTimer = 0f;
        _isFading = true;
    }

    public void PlayMusic(AudioStream music)
    {
        if (music == null) return;

        var current = _musicPlayers[_activeMusicIndex];
        if (current.Stream == music && current.Playing) return;

        current.Stop();
        current.Stream = music;
        current.VolumeDb = 0f;
        current.Play();
    }

    public void FadeOutMusic(float fadeDuration = 1.2f)
    {
        var current = _musicPlayers[_activeMusicIndex];
        if (!current.Playing) return;

        _fadingOutMusicIndex = _activeMusicIndex;
        _fadeDuration = fadeDuration;
        _fadeTimer = 0f;
        _isFading = true;
    }

    public void StopMusic()
    {
        foreach (var player in _musicPlayers)
        {
            player.Stop();
            player.Stream = null;
            player.VolumeDb = 0f;
        }
        _isFading = false;
    }

    public bool IsMusicPlaying(AudioStream music)
    {
        if (music == null) return false;
        foreach (var player in _musicPlayers)
            if (player.Playing && player.Stream == music) return true;
        return false;
    }

    // ================================================================
    //  环境声
    // ================================================================

    public void PlayAmbientWithFade(AudioStream ambient, float fadeDuration = 1.2f)
    {
        if (ambient == null) return;

        var current = _ambientPlayers[_activeAmbientIndex];
        if (current.Stream == ambient && current.Playing) return;

        if (_fadingOutAmbientIndex >= 0)
        {
            _ambientPlayers[_fadingOutAmbientIndex].Stop();
            _ambientPlayers[_fadingOutAmbientIndex].Stream = null;
        }

        if (current.Playing)
            _fadingOutAmbientIndex = _activeAmbientIndex;

        _activeAmbientIndex = (_activeAmbientIndex + 1) % AMBIENT_PLAYER_COUNT;
        var newPlayer = _ambientPlayers[_activeAmbientIndex];
        newPlayer.Stream = ambient;
        newPlayer.VolumeDb = -40f;
        newPlayer.Play();

        _fadeDuration = fadeDuration;
        _fadeTimer = 0f;
        _isFading = true;
    }

    public void FadeOutAmbient(float fadeDuration = 1.2f)
    {
        var current = _ambientPlayers[_activeAmbientIndex];
        if (!current.Playing) return;

        _fadingOutAmbientIndex = _activeAmbientIndex;
        _fadeDuration = fadeDuration;
        _fadeTimer = 0f;
        _isFading = true;
    }

    public bool IsAmbientPlaying(AudioStream ambient)
    {
        if (ambient == null) return false;
        foreach (var player in _ambientPlayers)
            if (player.Playing && player.Stream == ambient) return true;
        return false;
    }

    // ================================================================
    //  音效
    // ================================================================

    /// <summary>
    /// 播放单个音效
    /// </summary>
    public void PlaySfx(AudioStream sfx)
    {
        if (sfx == null) return;

        foreach (var player in _sfxPlayers)
        {
            if (!player.Playing)
            {
                player.Stream = sfx;
                player.Play();
                return;
            }
        }

        _sfxPlayers[0].Stop();
        _sfxPlayers[0].Stream = sfx;
        _sfxPlayers[0].Play();
    }

    /// <summary>
    /// 从音效数组中随机选择一个播放
    /// </summary>
    public void PlayRandomSfx(AudioStream[] sfxArray)
    {
        if (sfxArray == null || sfxArray.Length == 0) return;
        
        var sfx = sfxArray[GD.RandRange(0, sfxArray.Length - 1)];
        PlaySfx(sfx);
    }

    public void SetBusVolume(Bus bus, float volume)
    {
        string busName = bus switch
        {
            Bus.MASTER => MASTER_BUS,
            Bus.MUSIC => MUSIC_BUS,
            Bus.SFX => SFX_BUS,
            Bus.AMBIENT => AMBIENT_BUS,
            _ => MASTER_BUS
        };

        int busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex < 0) return;

        float clamped = Mathf.Clamp(volume, 0f, 1f);
        float db = Mathf.LinearToDb(clamped);
        AudioServer.SetBusVolumeDb(busIndex, db);
    }

    // ================================================================
    //  UI 音效
    // ================================================================

    public void PlayUiClick() => PlaySfx(_uiClickSound);
    public void PlayUiHover() => PlaySfx(_uiHoverSound);
    public void PlayUiInvalid() => PlaySfx(_uiInvalidSound);
    public void PlayUiSuccess() => PlaySfx(_uiSuccessSound);
    public void PlayShopBuySuccess() => PlaySfx(_shopBuySuccessSound);

    public void AttachUiSounds(BaseButton button)
    {
        if (button == null) return;

        button.Pressed += () => PlaySfx(_uiClickSound);

        button.MouseEntered += () =>
        {
            if (!button.Disabled)
                PlaySfx(_uiHoverSound);
        };

        button.GuiInput += (@event) =>
        {
            if (@event is InputEventMouseButton mb
                && mb.Pressed
                && mb.ButtonIndex == MouseButton.Left
                && button.Disabled)
            {
                PlaySfx(_uiInvalidSound);
            }
        };
    }
}
