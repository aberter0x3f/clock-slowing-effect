using System.Collections.Generic;
using Godot;


public enum SoundEffect {
  EnemyDeath,
  BossDeath,
  PlayerDeath,
  FireSmall,
  FireBig,
  Graze,
  CurioUse,
  CurioWrong,
  CurioSwitch,
  ItemGet,
  PlayerSkillAvailable,
  PlayerShoot,
  PlayerReloadComplete,
  PowerUp,
}

public partial class SoundManager : Node {
  public static SoundManager Instance { get; private set; }

  [Export(PropertyHint.Range, "1, 256, 1")]
  private int _playerPoolSize = 64;

  [Export] private Godot.Collections.Dictionary<string, SoundResource> _library = new();

  private List<AudioStreamPlayer> _playerPool = new();
  private Dictionary<AudioStream, ulong> _lastPlayTimestamps = new();

  public override void _Ready() {
    Instance = this;

    for (int i = 0; i < _playerPoolSize; ++i) {
      var player = new AudioStreamPlayer();
      player.ProcessMode = ProcessModeEnum.Always;
      AddChild(player);
      _playerPool.Add(player);
    }
  }

  public void Play(string effectName) {
    if (_library.TryGetValue(effectName, out var effect)) {
      Play(effect.Stream, effect.Cooldown, effect.VolumeDb, effect.Pitch);
    } else {
      GD.PrintErr($"SoundManager: SE config {effectName} not found");
    }
  }

  public void Play(SoundEffect se) => Play(se.ToString());

  /// <summary>
  /// 播放一个音效．
  /// </summary>
  /// <param name="sound">要播放的 AudioStream 资源．</param>
  /// <param name="volumeDb">音量 (分贝)．</param>
  /// <param name="pitch">音高．</param>
  /// <param name="cooldown">此音效的最小播放间隔（秒）．小于此间隔的连续播放请求将被忽略．</param>
  public void Play(AudioStream sound, float cooldown = 0.05f, float volumeDb = 0f, float pitch = 1f) {
    if (sound == null) {
      return;
    }

    ulong currentTime = Time.GetTicksMsec();

    if (_lastPlayTimestamps.TryGetValue(sound, out ulong lastTime)) {
      if (currentTime - lastTime < cooldown * 1000) {
        return;
      }
    }

    foreach (var player in _playerPool) {
      if (!player.Playing) {
        player.Stream = sound;
        player.VolumeDb = volumeDb;
        player.PitchScale = pitch;
        player.Play();

        _lastPlayTimestamps[sound] = currentTime;
        return;
      }
    }

    // 如果执行到这里，意味着所有播放器都在忙
    GD.Print("SoundManager: No available AudioStreamPlayers in the pool. Consider increasing the pool size.");
  }
}
