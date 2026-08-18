using Godot;
using System.Collections.Generic;

public partial class AudioManager : Node
{
    public enum Bus { MASTER, MUSIC, SFX }

    private const string MUSIC_BUS = "Music";
    private const string SFX_BUS = "SFX";

    private const int MUSIC_PLAYER_COUNT = 2;

    private AudioStreamPlayer[] _musicPlayers;
    private AudioStreamPlayer _sfxPlayer;
    private int _activeMusicIndex = 0;
    private int _fadingOutIndex = -1;
    private float _fadeDuration = 1.2f;
    private float _fadeTimer = 0f;
    private bool _isFading = false;

    public override void _Ready()
    {
        GD.Print("AudioManager 已成功加载并挂载到根节点。");
        InitializeMusicPlayers();
        InitializeSfxPlayer();
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

    private void InitializeSfxPlayer()
    {
        _sfxPlayer = new AudioStreamPlayer
        {
            Name = "SfxPlayer",
            Bus = SFX_BUS
        };
        AddChild(_sfxPlayer);
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

        // 同一首已在播放，忽略
        if (current.Stream == music && current.Playing) return;

        // 先停掉上一个淡出中的播放器
        if (_fadingOutIndex >= 0)
        {
            _musicPlayers[_fadingOutIndex].Stop();
            _musicPlayers[_fadingOutIndex].Stream = null;
            _musicPlayers[_fadingOutIndex].VolumeDb = 0f;
        }

        // 当前播放器变成淡出
        if (current.Playing)
        {
            _fadingOutIndex = _activeMusicIndex;
        }

        // 切换到另一个播放器
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

    public void PlaySfx(AudioStream sfx)
    {
        if (sfx == null || _sfxPlayer == null) return;
        _sfxPlayer.Stream = sfx;
        _sfxPlayer.Play();
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