using Godot;

public partial class AudioManager : Node
{
    public enum Bus { MASTER, MUSIC, SFX }

    private const string MUSIC_BUS = "Music";
    private const string SFX_BUS = "SFX";
    private const string MASTER_BUS = "Master";

    private const int MUSIC_PLAYER_COUNT = 2;
    private const int SFX_PLAYER_COUNT = 6;

    private AudioStreamPlayer[] _musicPlayers;
    private AudioStreamPlayer[] _sfxPlayers;
    private int _activeMusicIndex = 0;
    private int _fadingOutIndex = -1;
    private float _fadeDuration = 1.2f;
    private float _fadeTimer = 0f;
    private bool _isFading = false;

    public override void _Ready()
    {
        GD.Print("AudioManager 已成功加载并挂载到根节点。");
        InitializeMusicPlayers();
        InitializeSfxPlayers();
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

    public override void _Process(double delta)
    {
        if (!_isFading) return;

        _fadeTimer += (float)delta;
        float t = Mathf.Clamp(_fadeTimer / _fadeDuration, 0f, 1f);

        if (_fadingOutIndex >= 0)
        {
            var fadingOut = _musicPlayers[_fadingOutIndex];
            fadingOut.VolumeDb = Mathf.Lerp(0f, -40f, t);
        }

        var active = _musicPlayers[_activeMusicIndex];
        active.VolumeDb = Mathf.Lerp(-40f, 0f, t);

        if (t >= 1f)
        {
            if (_fadingOutIndex >= 0)
            {
                _musicPlayers[_fadingOutIndex].Stop();
                _musicPlayers[_fadingOutIndex].Stream = null;
                _musicPlayers[_fadingOutIndex].VolumeDb = 0f;
                _fadingOutIndex = -1;
            }
            _isFading = false;
        }
    }

    /// <summary>
    /// 切换音乐：旧音乐淡出，新音乐淡入。
    /// </summary>
    public void PlayMusicWithFade(AudioStream music, float fadeDuration = 1.2f)
    {
        if (music == null) return;

        var current = _musicPlayers[_activeMusicIndex];

        if (current.Stream == music && current.Playing) return;

        if (_fadingOutIndex >= 0)
        {
            _musicPlayers[_fadingOutIndex].Stop();
            _musicPlayers[_fadingOutIndex].Stream = null;
            _musicPlayers[_fadingOutIndex].VolumeDb = 0f;
        }

        if (current.Playing)
        {
            _fadingOutIndex = _activeMusicIndex;
        }

        _activeMusicIndex = (_activeMusicIndex + 1) % MUSIC_PLAYER_COUNT;
        var newPlayer = _musicPlayers[_activeMusicIndex];
        newPlayer.Stream = music;
        newPlayer.VolumeDb = -40f;
        newPlayer.Play();

        _fadeDuration = fadeDuration;
        _fadeTimer = 0f;
        _isFading = true;
    }

    /// <summary>
    /// 立即播放（无淡入淡出）。
    /// </summary>
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

    /// <summary>
    /// 播放音效：遍历音效播放器，找到空闲的播放。
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
    /// 设置指定总线的音量。volume 在 0~1 之间。
    /// </summary>
    public void SetBusVolume(Bus bus, float volume)
    {
        string busName = bus switch
        {
            Bus.MASTER => MASTER_BUS,
            Bus.MUSIC => MUSIC_BUS,
            Bus.SFX => SFX_BUS,
            _ => MASTER_BUS
        };

        int busIndex = AudioServer.GetBusIndex(busName);
        if (busIndex < 0) return;

        float clamped = Mathf.Clamp(volume, 0f, 1f);
        float db = Mathf.LinearToDb(clamped);
        AudioServer.SetBusVolumeDb(busIndex, db);
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
        {
            if (player.Playing && player.Stream == music)
                return true;
        }
        return false;
    }
}