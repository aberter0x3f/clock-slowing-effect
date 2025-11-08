using System.Collections.Generic;
using System.Linq;
using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseStellarState : BasePhaseState {
  // --- 状态 ---
  public PhaseStellar.Phase CurrentPhase;
  public float Timer;
  public float OrbiterAngle;
  public float OrbiterSpeed;
  public float DefenseTimer;
  public float PhaseStartAngle;
  // --- 层状态 ---
  public int CurrentPhaseLayerIndex;
  public int LayersInCurrentColorPhase;
  // --- 集合状态 (通过 InstanceId 保存) ---
  public Godot.Collections.Array<Godot.Collections.Array<ulong>> Rings;
  public Godot.Collections.Dictionary<int, int> RingArrivalCounters;
  public Godot.Collections.Array<int> RotatingRingIndices;
  public Godot.Collections.Dictionary<ulong, Godot.Collections.Array<ulong>> OrbiterQueues;
  public Godot.Collections.Dictionary<ulong, int> OrbiterFireIndices;
}

public partial class PhaseStellar : BasePhase {
  public enum Phase {
    OrbiterWait,
    Blue,
    Green,
    Yellow,
    Orange,
    Red,
    Supernova,
    BlackHole,
  }

  public override float MaxHealth { get; protected set; } = 998244353f;

  [ExportGroup("Timing")]
  [Export] public float OrbiterSpawnWaitDuration { get; set; } = 1.0f;
  [Export] public float BlackHoleDuration { get; set; } = 10.0f;
  [Export] public float JetFireInterval { get; set; } = 0.2f;

  [ExportGroup("Orbiters")]
  [Export] public int OrbiterCount { get; set; } = 4;
  [Export] public float OrbiterRadius { get; set; } = 900f;
  [Export] public float InitialOrbiterSpeed { get; set; } = 2f; // rad/s
  [Export] public float PerPhaseSpeedMultiplier { get; set; } = 1.3f;
  [Export] public PackedScene BigBulletScene { get; set; }
  [Export] public int RoundsPerLayer { get; set; } = 1;

  [ExportGroup("Rings")]
  [Export] public float Radius0 { get; set; } = 100f;
  [Export] public float DeltaR { get; set; } = 10f;
  [Export] public float BulletSpacing { get; set; } = 15f;
  [Export] public Godot.Collections.Array<int> LayerCounts { get; set; } = new() { 2, 2, 5, 6, 8 };
  [Export] public float SmallBulletSpeed { get; set; } = 300f;
  [Export] public float RingRotationSpeed { get; set; } = 2f;

  [ExportGroup("Bullet Scenes")]
  [Export] public PackedScene SmallBulletScene { get; set; }

  [ExportGroup("Supernova")]
  [Export(PropertyHint.Range, "1, 10, 0.01")] public float SupernovaMaxDelay { get; set; } = 5f;

  [ExportGroup("Black Hole")]
  [Export] public PackedScene JetBulletScene { get; set; }

  [ExportGroup("Defense")]
  [Export] public float DefenseTriggerDistance { get; set; } = 100f;
  [Export] public float DefenseCooldown { get; set; } = 0.2f;
  [Export] public PackedScene DefenseBulletScene { get; set; }

  [ExportGroup("Time")]
  [Export]
  public float TimeScaleSensitivity { get; set; } = 1f;

  [ExportGroup("Sound Effects")]
  [Export]
  public AudioStream PowerUpSound { get; set; }

  // --- 状态 ---
  private Phase _currentPhase = Phase.OrbiterWait;
  private float _timer;
  private float _orbiterAngle;
  private float _orbiterSpeed;
  private float _defenseTimer;
  private float _phaseStartAngle; // 记录每个「层」开始时的轨道角度
  // --- 层状态 ---
  private int _currentPhaseLayerIndex = 0;
  private int _layersInCurrentColorPhase = 0;
  // --- 颜色转换状态 ---
  private bool _isTransformingColor = false;
  private float _colorTransformTimer = 0f;
  private int _colorTransformRingIndex = -1;
  private PhaseStellarSmallBullet.BulletColor _colorTransformTarget;

  private readonly List<BaseBullet> _orbiters = new();
  private readonly List<List<PhaseStellarSmallBullet>> _rings = new();
  private readonly Dictionary<int, int> _ringArrivalCounters = new();
  private readonly HashSet<int> _rotatingRingIndices = new();
  private readonly Dictionary<BaseBullet, List<PhaseStellarSmallBullet>> _orbiterQueues = new();
  // 追踪每个轨道器发射到哪个索引
  private readonly Dictionary<BaseBullet, int> _orbiterFireIndices = new();
  private readonly RandomNumberGenerator _rng = new();

  public override void StartPhase(Boss parent) {
    base.StartPhase(parent);

    parent.SetCollisionEnabled(false);

    _orbiterSpeed = InitialOrbiterSpeed;
    var mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (mapGenerator != null) {
      float minMapDim = Mathf.Min(mapGenerator.MapWidth, mapGenerator.MapHeight) * mapGenerator.TileSize;
      OrbiterRadius = minMapDim * 0.75f;
    }

    var rank = GameManager.Instance.EnemyRank;
    TimeScaleSensitivity = 3f / (rank + 3);
    InitialOrbiterSpeed *= rank / 5f;
    SmallBulletSpeed *= (rank + 20) / 25f;

    SpawnOrbiters();
    TransitionTo(Phase.OrbiterWait);
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) {
      UpdateOrbiterPositions();
      return;
    }
    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;

    // 在颜色转换期间，暂停所有其他逻辑
    if (_isTransformingColor) {
      ProcessColorTransformation(scaledDelta);
      return;
    }

    _timer -= scaledDelta;

    _defenseTimer -= scaledDelta;
    if (_defenseTimer <= 0 && PlayerNode.GlobalPosition.Length() <= DefenseTriggerDistance) {
      FireDefensePattern();
      _defenseTimer = DefenseCooldown;
    }

    switch (_currentPhase) {
      case Phase.OrbiterWait:
        UpdateOrbiterPositions();
        if (_timer <= 0) TransitionTo(Phase.Blue);
        break;
      case Phase.Blue:
      case Phase.Green:
      case Phase.Yellow:
      case Phase.Orange:
      case Phase.Red:
        UpdateOrbiterPositions();
        ProcessAngularFiring();

        // 检查当前「层」的子弹是否已全部发射
        bool currentLayerFired = _orbiters.All(orb =>
          !_orbiterFireIndices.ContainsKey(orb) ||
          _orbiterFireIndices[orb] >= _orbiterQueues[orb].Count
        );

        if (currentLayerFired) {
          // 检查这是否是当前颜色阶段的最后一层
          if (_currentPhaseLayerIndex >= _layersInCurrentColorPhase - 1) {
            // 这是最后一层．现在我们等待它的子弹全部到达．
            // 这一层的环是最后一个被添加的环．
            int lastRingIndex = _rings.Count - 1;
            if (lastRingIndex >= 0) {
              // 检查这个环中的所有子弹是否都已到达．
              // 到达状态由 _ringArrivalCounters 追踪．
              bool allArrived = _ringArrivalCounters.ContainsKey(lastRingIndex) &&
                                _ringArrivalCounters[lastRingIndex] >= _rings[lastRingIndex].Count;

              if (allArrived) {
                // 最后一层的所有子弹都已到达．开始颜色转换效果．
                StartColorTransformation(GetColorForPhase(_currentPhase));
              }
            }
            // 如果还没有全部到达，我们什么都不做，等待下一帧．
          } else {
            // 不是最后一层．递增并准备下一层．
            ++_currentPhaseLayerIndex;
            PrepareNextLayer();
          }
        }
        break;
      case Phase.Supernova:
        UpdateOrbiterPositions();
        bool allSupernovasDone = _rings.SelectMany(r => r)
          .Where(b => b.Type == PhaseStellarSmallBullet.BulletType.Supernova)
          .All(b => b.CurrentState == PhaseStellarSmallBullet.State.SupernovaHoming || b.IsDestroyed);
        if (allSupernovasDone) {
          TransitionTo(Phase.BlackHole);
        }
        break;
      case Phase.BlackHole:
        if (_timer <= 0) {
          FireJet();
          _timer = JetFireInterval;
        }
        break;
    }
  }

  private void TransitionTo(Phase nextPhase) {
    PlayPowerUpSound();

    GD.Print($"Stellar Phase transitioning from {_currentPhase} to {nextPhase}");
    _currentPhase = nextPhase;
    UpdateTemperature();

    switch (_currentPhase) {
      case Phase.OrbiterWait:
        _timer = OrbiterSpawnWaitDuration;
        break;
      case Phase.Blue:
      case Phase.Green:
      case Phase.Yellow:
      case Phase.Orange:
      case Phase.Red:
        _currentPhaseLayerIndex = 0;
        int phaseIndex = (int) _currentPhase - (int) Phase.Blue;
        _layersInCurrentColorPhase = LayerCounts[phaseIndex];
        PrepareNextLayer();
        _orbiterSpeed *= PerPhaseSpeedMultiplier;
        break;
      case Phase.Supernova:
        foreach (var ring in _rings) {
          foreach (var bullet in ring) {
            if (bullet.Type == PhaseStellarSmallBullet.BulletType.Supernova) {
              bullet.SwitchToSupernova();
            }
          }
        }
        break;
      case Phase.BlackHole:
        foreach (var ring in _rings) {
          foreach (var bullet in ring) {
            if (bullet.Type == PhaseStellarSmallBullet.BulletType.BlackHole) {
              bullet.SwitchToBlackHole();
            }
          }
        }
        Health = BlackHoleDuration;
        _timer = 0;
        break;
    }
  }

  private void SpawnOrbiters() {
    for (int i = 0; i < OrbiterCount; ++i) {
      var orbiter = BigBulletScene.Instantiate<BaseBullet>();
      _orbiters.Add(orbiter);
      _orbiterQueues[orbiter] = new List<PhaseStellarSmallBullet>();
      _orbiterFireIndices[orbiter] = 0;
      GameRootProvider.CurrentGameRoot.AddChild(orbiter);
    }
    UpdateOrbiterPositions();
  }

  private void UpdateOrbiterPositions() {
    _orbiterAngle += _orbiterSpeed * (float) GetProcessDeltaTime() * TimeManager.Instance.TimeScale;
    for (int i = 0; i < _orbiters.Count; ++i) {
      float angle = _orbiterAngle + Mathf.Tau / _orbiters.Count * i;
      _orbiters[i].GlobalPosition = Vector2.Right.Rotated(angle) * OrbiterRadius;
    }
  }

  /// <summary>
  /// 准备并生成当前层的子弹环，并为每个子弹分配随机的发射角度．
  /// </summary>
  private void PrepareNextLayer() {
    // 重置每个新层的状态
    _phaseStartAngle = _orbiterAngle;
    _orbiterFireIndices.Clear();
    foreach (var orbiter in _orbiters) {
      _orbiterFireIndices[orbiter] = 0;
      _orbiterQueues[orbiter].Clear();
    }

    var bulletColor = GetColorForPhase(_currentPhase);

    // ringIndex 是已创建环的总数，这能确保半径和旋转方向正确递增
    int ringIndex = _rings.Count;
    float radius = Radius0 + ringIndex * DeltaR;
    int count = Mathf.RoundToInt(Mathf.Tau * radius / BulletSpacing);
    float theta0 = _rng.Randf() * Mathf.Tau;
    var newRing = new List<PhaseStellarSmallBullet>();

    for (int j = 0; j < count; ++j) {
      float theta = theta0 + Mathf.Tau / count * j;
      var targetPos = Vector2.Right.Rotated(theta) * radius;

      var bullet = SmallBulletScene.Instantiate<PhaseStellarSmallBullet>();
      bullet.CurrentColor = bulletColor;
      bullet.Type = _currentPhase <= Phase.Yellow ? PhaseStellarSmallBullet.BulletType.BlackHole : PhaseStellarSmallBullet.BulletType.Supernova;
      bullet.TimeScaleSensitivity = TimeScaleSensitivity;
      bullet.TargetPosition = targetPos;
      bullet.MoveSpeed = SmallBulletSpeed;
      bullet.RingIndex = ringIndex;
      bullet.RingRotationSpeed = RingRotationSpeed * (ringIndex % 2 == 0 ? 1 : -1);
      bullet.SupernovaDelay = _rng.RandfRange(0f, SupernovaMaxDelay);
      bullet.ReachedTarget += OnSmallBulletArrived;

      float randomRounds = _rng.Randf() * RoundsPerLayer;
      bullet.FireAngleOffset = randomRounds * Mathf.Tau;

      newRing.Add(bullet);
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
    _rings.Add(newRing);
    _ringArrivalCounters[ringIndex] = 0;

    var shuffledBullets = new List<PhaseStellarSmallBullet>(newRing);
    shuffledBullets.Shuffle(_rng);

    // 将这一层的所有子弹分配到各个轨道器的发射队列中
    for (int k = 0; k < shuffledBullets.Count; ++k) {
      var orbiter = _orbiters[k % OrbiterCount];
      _orbiterQueues[orbiter].Add(shuffledBullets[k]);
    }

    // 为每个轨道器的队列按发射角度排序，以便顺序发射
    foreach (var orbiter in _orbiters) {
      _orbiterQueues[orbiter].Sort((a, b) => a.FireAngleOffset.CompareTo(b.FireAngleOffset));
    }
  }

  private void ProcessAngularFiring() {
    float currentAngleOffset = _orbiterAngle - _phaseStartAngle;
    foreach (var orbiter in _orbiters) {
      var queue = _orbiterQueues[orbiter];
      int fireIndex = _orbiterFireIndices[orbiter];

      while (fireIndex < queue.Count && currentAngleOffset >= queue[fireIndex].FireAngleOffset) {
        var bulletToActivate = queue[fireIndex];
        if (bulletToActivate.CurrentState == PhaseStellarSmallBullet.State.Inactive) {
          bulletToActivate.Activate(orbiter.GlobalPosition);
        }
        ++fireIndex;
        _orbiterFireIndices[orbiter] = fireIndex;
      }
    }
  }

  private void OnSmallBulletArrived(int ringIndex) {
    ++_ringArrivalCounters[ringIndex];
    if (_ringArrivalCounters[ringIndex] >= _rings[ringIndex].Count) {
      PlayAttackSound();
      _rotatingRingIndices.Add(ringIndex);
      foreach (var bullet in _rings[ringIndex]) {
        bullet.StartRotation();
      }
    }
  }

  private void FireJet() {
    var jet = JetBulletScene.Instantiate<PhaseStellarJetBullet>();
    jet.TimeScaleSensitivity = TimeScaleSensitivity;
    jet.RawPosition = new Vector3(ParentBoss.GlobalPosition.X, ParentBoss.GlobalPosition.Y, 0);
    jet.Velocity = new Vector3(_rng.Randfn(0, 5), _rng.Randfn(0, 5), 0);
    jet.Acceleration = new Vector3(0, 0, 1000f);
    GameRootProvider.CurrentGameRoot.AddChild(jet);
  }

  private void FireDefensePattern() {
    for (int i = 0; i < 36; ++i) {
      var bullet = DefenseBulletScene.Instantiate<SimpleBullet>();
      bullet.GlobalPosition = ParentBoss.GlobalPosition;
      bullet.Rotation = Mathf.DegToRad(i * 10f);
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  private void StartColorTransformation(PhaseStellarSmallBullet.BulletColor targetColor) {
    if (_isTransformingColor) return;

    GD.Print($"Starting color transformation to {targetColor}.");
    _isTransformingColor = true;
    _colorTransformTarget = targetColor;
    _colorTransformRingIndex = _rings.Count - 1; // 从最外圈开始
    _colorTransformTimer = 0.05f; // 第一次转换前的延迟
  }

  private void ProcessColorTransformation(float scaledDelta) {
    _colorTransformTimer -= scaledDelta;
    if (_colorTransformTimer <= 0) {
      if (_colorTransformRingIndex >= 0) {
        var ring = _rings[_colorTransformRingIndex];
        foreach (var bullet in ring) {
          if (IsInstanceValid(bullet) && !bullet.IsDestroyed && bullet.CurrentColor != _colorTransformTarget) {
            bullet.SetColor(_colorTransformTarget);
          }
        }
        --_colorTransformRingIndex;
        _colorTransformTimer = 0.05f; // 为下一圈重置计时器
      } else {
        // 转换完成
        _isTransformingColor = false;
        // 现在，转换到下一阶段
        TransitionTo(_currentPhase + 1);
      }
    }
  }

  private void UpdateTemperature() {
    // 为每个阶段设置一个主题性的「温度」值
    Health = _currentPhase switch {
      Phase.OrbiterWait => 30000f,
      Phase.Blue => 20000f,
      Phase.Green => 10000f,
      Phase.Yellow => 6000f,
      Phase.Orange => 4000f,
      Phase.Red => 3000f,
      Phase.Supernova => 1e9f,
      Phase.BlackHole => BlackHoleDuration, // 黑洞阶段使用 Health 作为计时器
      _ => Health
    };
  }

  private PhaseStellarSmallBullet.BulletColor GetColorForPhase(Phase phase) {
    return phase switch {
      Phase.Blue => PhaseStellarSmallBullet.BulletColor.Blue,
      Phase.Green => PhaseStellarSmallBullet.BulletColor.Green,
      Phase.Yellow => PhaseStellarSmallBullet.BulletColor.Yellow,
      Phase.Orange => PhaseStellarSmallBullet.BulletColor.Orange,
      Phase.Red => PhaseStellarSmallBullet.BulletColor.Red,
      _ => PhaseStellarSmallBullet.BulletColor.Blue,
    };
  }

  public override RewindState CaptureInternalState() {
    var ringsState = new Godot.Collections.Array<Godot.Collections.Array<ulong>>();
    foreach (var ring in _rings) {
      var ringIds = new Godot.Collections.Array<ulong>();
      foreach (var bullet in ring) {
        ringIds.Add(bullet.GetInstanceId());
      }
      ringsState.Add(ringIds);
    }

    var queuesState = new Godot.Collections.Dictionary<ulong, Godot.Collections.Array<ulong>>();
    foreach (var (orbiter, queue) in _orbiterQueues) {
      var queueIds = new Godot.Collections.Array<ulong>();
      foreach (var bullet in queue) {
        queueIds.Add(bullet.GetInstanceId());
      }
      queuesState[orbiter.GetInstanceId()] = queueIds;
    }

    var fireIndicesState = new Godot.Collections.Dictionary<ulong, int>();
    foreach (var (orbiter, index) in _orbiterFireIndices) {
      fireIndicesState[orbiter.GetInstanceId()] = index;
    }

    return new PhaseStellarState {
      CurrentPhase = this._currentPhase,
      Timer = this._timer,
      OrbiterAngle = this._orbiterAngle,
      OrbiterSpeed = this._orbiterSpeed,
      DefenseTimer = this._defenseTimer,
      PhaseStartAngle = this._phaseStartAngle,
      CurrentPhaseLayerIndex = this._currentPhaseLayerIndex,
      LayersInCurrentColorPhase = this._layersInCurrentColorPhase,
      Rings = ringsState,
      RingArrivalCounters = new Godot.Collections.Dictionary<int, int>(_ringArrivalCounters),
      RotatingRingIndices = new Godot.Collections.Array<int>(_rotatingRingIndices),
      OrbiterQueues = queuesState,
      OrbiterFireIndices = fireIndicesState,
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseStellarState pss) return;

    this._currentPhase = pss.CurrentPhase;
    this._timer = pss.Timer;
    this._orbiterAngle = pss.OrbiterAngle;
    this._orbiterSpeed = pss.OrbiterSpeed;
    this._defenseTimer = pss.DefenseTimer;
    this._phaseStartAngle = pss.PhaseStartAngle;
    this._currentPhaseLayerIndex = pss.CurrentPhaseLayerIndex;
    this._layersInCurrentColorPhase = pss.LayersInCurrentColorPhase;

    _rings.Clear();
    if (pss.Rings != null) {
      foreach (var ringIds in pss.Rings) {
        var newRing = new List<PhaseStellarSmallBullet>();
        foreach (var id in ringIds) {
          var bullet = InstanceFromId(id) as PhaseStellarSmallBullet;
          if (IsInstanceValid(bullet)) {
            newRing.Add(bullet);
          }
        }
        _rings.Add(newRing);
      }
    }

    _ringArrivalCounters.Clear();
    if (pss.RingArrivalCounters != null) {
      foreach (var (key, value) in pss.RingArrivalCounters) {
        _ringArrivalCounters[key] = value;
      }
    }

    _rotatingRingIndices.Clear();
    if (pss.RotatingRingIndices != null) {
      foreach (var index in pss.RotatingRingIndices) {
        _rotatingRingIndices.Add(index);
      }
    }

    _orbiterQueues.Clear();
    if (pss.OrbiterQueues != null) {
      foreach (var (orbiterId, queueIds) in pss.OrbiterQueues) {
        var orbiter = InstanceFromId(orbiterId) as BaseBullet;
        if (IsInstanceValid(orbiter)) {
          var newQueue = new List<PhaseStellarSmallBullet>();
          foreach (var bulletId in queueIds) {
            var bullet = InstanceFromId(bulletId) as PhaseStellarSmallBullet;
            if (IsInstanceValid(bullet)) {
              newQueue.Add(bullet);
            }
          }
          _orbiterQueues[orbiter] = newQueue;
        }
      }
    }

    _orbiterFireIndices.Clear();
    if (pss.OrbiterFireIndices != null) {
      foreach (var (id, index) in pss.OrbiterFireIndices) {
        var orbiter = InstanceFromId(id) as BaseBullet;
        if (IsInstanceValid(orbiter)) {
          _orbiterFireIndices[orbiter] = index;
        }
      }
    }
  }

  private void PlayPowerUpSound() {
    SoundManager.Instance.PlaySoundEffect(PowerUpSound, cooldown: 0.2f, volumeDb: 5f);
  }
}
