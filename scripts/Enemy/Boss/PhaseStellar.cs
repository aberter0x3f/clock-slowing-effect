using System.Collections.Generic;
using System.Linq;
using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseStellarState : BasePhaseState {
  // 核心阶段状态
  public PhaseStellar.Phase CurrentPhase;
  public float Timer;
  public float OrbiterAngle;
  public float OrbiterSpeed;
  public float DefenseTimer;

  // 环/层生成状态
  public int CurrentPhaseLayerIndex;
  public int LayersInCurrentColorPhase;

  // 特殊机制计时器
  public float GreenTimer;
  public float YellowTimer;
  public float OrangeTimer;
  public float OrangeTimeAccumulator;

  // 集合状态 (ID 引用)
  public Godot.Collections.Array<Godot.Collections.Array<ulong>> Rings;
  public Godot.Collections.Dictionary<int, int> RingArrivalCounters;
  public Godot.Collections.Array<int> RotatingRingIndices;
  public Godot.Collections.Dictionary<ulong, Godot.Collections.Array<ulong>> OrbiterQueues;
  public Godot.Collections.Dictionary<ulong, int> OrbiterFireIndices;
}

public partial class PhaseStellar : BasePhase {
  public enum Phase { OrbiterWait, Blue, Green, Yellow, Orange, Red, Supernova, BlackHole }

  public override float MaxHealth { get; protected set; } = 998244353f;

  [ExportGroup("Orbiters")]
  [Export] public int OrbiterCount = 4;
  [Export] public float OrbiterRadius = 9.0f;
  [Export] public float InitialOrbiterSpeed = 3.0f;

  [ExportGroup("Rings")]
  [Export] public float Radius0 = 0.2f;
  [Export] public float DeltaR = 0.05f;
  [Export] public float BulletSpacing = 0.15f;
  [Export] public float SmallBulletSpeedMean = 3.0f;
  [Export] public float SmallBulletSpeedSigma = 0.3f;
  [Export] public float RingRotationSpeed = 2.5f;
  [Export] public Godot.Collections.Array<int> LayerCounts { get; set; } = new() { 4, 8, 10, 12, 20 };

  [ExportGroup("Special: Green")]
  [Export] public float GreenChainInterval = 0.4f;
  [Export] public int GreenChainLength = 10;
  [Export] public float GreenChainBulletSpacing = 0.1f; // 间距
  [Export] public float GreenChainSpeed = 3f;

  [ExportGroup("Special: Yellow")]
  [Export] public float YellowBulletInterval = 3.5f;
  [Export] public int YellowBulletCount = 24;
  [Export] public float YellowBulletSpeed = 5.0f;
  [Export] public float YellowBulletAccel = 5.0f;

  [ExportGroup("Special: Orange")]
  [Export] public float OrangeBulletInterval = 0.02f;
  [Export] public int OrangeBulletCount = 6;
  [Export] public float OrangeBulletSpeed = 8.0f;
  [Export] public float OrangeOscillationK = 0.8f;
  [Export] public float OrangeOscillationAmp = 0.52f; // PI/6

  [ExportGroup("Special: Red")]
  [Export] public float RedPushSpeed = 1f;

  [ExportGroup("Scenes")]
  [Export] public PackedScene BigBulletScene;
  [Export] public PackedScene SmallBulletScene;
  [Export] public PackedScene GreenSpecialBulletScene;
  [Export] public PackedScene YellowSpecialBulletScene;
  [Export] public PackedScene OrangeSpecialBulletScene;
  [Export] public PackedScene JetBulletScene;
  [Export] public PackedScene DefenseBulletScene;
  [Export] public PackedScene RelativisticBulletScene;

  // 运行时状态
  private Phase _currentPhase = Phase.OrbiterWait;
  private float _timer;
  private float _orbiterAngle;
  private float _orbiterSpeed;
  private float _defenseTimer;

  // 层生成逻辑
  private int _currentPhaseLayerIndex = 0;
  private int _layersInCurrentColorPhase = 0;

  // 颜色转换逻辑
  private bool _isTransformingColor = false;
  private float _colorTransformTimer = 0f;
  private int _colorTransformRingIndex = -1;
  private PhaseStellarSmallBullet.BulletColor _colorTransformTarget;

  // 特殊机制计时器
  private float _greenTimer;
  private float _yellowTimer;
  private float _orangeTimer;
  private float _orangeTimeAccumulator;

  // 容器
  private readonly List<SimpleBullet> _orbiters = new();
  private readonly List<List<PhaseStellarSmallBullet>> _rings = new();
  private readonly Dictionary<int, int> _ringArrivalCounters = new();
  private readonly HashSet<int> _rotatingRingIndices = new();
  private readonly Dictionary<SimpleBullet, List<PhaseStellarSmallBullet>> _orbiterQueues = new();
  private readonly Dictionary<SimpleBullet, int> _orbiterFireIndices = new();

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    parent.SetCollisionEnabled(false);
    _orbiterSpeed = InitialOrbiterSpeed;

    var rank = GameManager.Instance.EnemyRank;
    float diffMult = (rank + 10) / 15f;

    TimeScaleSensitivity = 5f / (rank + 5);
    InitialOrbiterSpeed *= rank / 5f;
    SmallBulletSpeedMean *= (rank + 10) / 15f;

    GreenChainSpeed *= diffMult;
    GreenChainInterval /= diffMult;
    YellowBulletSpeed *= diffMult;
    YellowBulletInterval /= diffMult;
    OrangeOscillationK *= diffMult;

    SpawnOrbiters();
    TransitionTo(Phase.OrbiterWait);
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    // 只有在非变色期间，且处于 Green~Red 阶段时才启用特殊机制
    if (!_isTransformingColor && _currentPhase >= Phase.Green && _currentPhase < Phase.Supernova) {
      UpdateSpecialMechanics(scaledDelta);
    }

    if (_isTransformingColor) {
      ProcessColorTransformation(scaledDelta);
      return;
    }

    _timer -= scaledDelta;
    _defenseTimer -= scaledDelta;
    float defenseRadius = _currentPhase < Phase.Supernova ? Radius0 + _rings.Count * DeltaR - 0.1f : 1.0f;
    if (_defenseTimer <= 0 && PlayerNode.GlobalPosition.Length() <= defenseRadius) {
      FireDefensePattern();
      _defenseTimer = 0.2f;
    }

    _orbiterAngle += _orbiterSpeed * scaledDelta;

    switch (_currentPhase) {
      case Phase.OrbiterWait:
        if (_timer <= 0) TransitionTo(Phase.Blue);
        break;
      case Phase.Blue:
      case Phase.Green:
      case Phase.Yellow:
      case Phase.Orange:
      case Phase.Red:
        UpdateLayerGeneration();
        break;
      case Phase.Supernova:
        UpdateSupernova();
        break;
      case Phase.BlackHole:
        UpdateBlackHole();
        break;
    }
  }

  private void UpdateSpecialMechanics(float delta) {
    // 叠加机制
    if (_currentPhase >= Phase.Green) UpdateGreenMechanic(delta);
    if (_currentPhase >= Phase.Yellow) UpdateYellowMechanic(delta);
    if (_currentPhase >= Phase.Orange) UpdateOrangeMechanic(delta);
    if (_currentPhase >= Phase.Red) UpdateRedMechanic(delta);
  }

  private void UpdateGreenMechanic(float delta) {
    _greenTimer -= delta;
    if (_greenTimer <= 0) {
      FireGreenChains();
      _greenTimer = GreenChainInterval;
    }
  }

  private void FireGreenChains() {
    if (GreenSpecialBulletScene == null) return;

    float angle = GD.Randf() * Mathf.Tau;
    Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

    for (int k = 0; k < GreenChainLength; ++k) {
      var b = GreenSpecialBulletScene.Instantiate<SimpleBullet>();

      // 通过位移起始点模拟链条效果
      // pos = dir * (speed * t - distance_offset)
      float distOffset = k * GreenChainBulletSpacing;

      b.TimeScaleSensitivity = TimeScaleSensitivity;
      b.UpdateFunc = (t) => {
        var s = new SimpleBullet.UpdateState();
        float effectiveDist = GreenChainSpeed * t - distOffset;
        s.position = dir * effectiveDist;

        // 如果还在原点后方，则隐藏
        if (effectiveDist < 0) s.modulate.A = 0;
        else s.modulate.A = 1;

        return s;
      };
      GameRootProvider.CurrentGameRoot.AddChild(b);
    }
    SoundManager.Instance.Play(SoundEffect.FireSmall);
  }

  private void UpdateYellowMechanic(float delta) {
    _yellowTimer -= delta;
    if (_yellowTimer <= 0) {
      FireYellowSpecial();
      _yellowTimer = YellowBulletInterval;
    }
  }

  private void FireYellowSpecial() {
    if (YellowSpecialBulletScene == null) return;

    Vector3 toPlayer = (PlayerNode.GlobalPosition - Vector3.Zero).Normalized();
    float aimAngle = Mathf.Atan2(toPlayer.Z, toPlayer.X);
    float angleStep = Mathf.Tau / YellowBulletCount;

    for (int i = 0; i < YellowBulletCount; ++i) {
      float angle = aimAngle + i * angleStep;
      Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

      var b = YellowSpecialBulletScene.Instantiate<PhaseStellarSpecialBullet>();
      b.Direction = dir;
      b.BaseSpeed = YellowBulletSpeed;
      b.Acceleration = YellowBulletAccel;
      b.TimeScaleSensitivity = TimeScaleSensitivity;
      GameRootProvider.CurrentGameRoot.AddChild(b);
    }
    SoundManager.Instance.Play(SoundEffect.FireSmall);
  }

  private void UpdateOrangeMechanic(float delta) {
    _orangeTimer -= delta;
    _orangeTimeAccumulator += delta;

    if (_orangeTimer <= 0) {
      FireOrangeOscillating();
      _orangeTimer = OrangeBulletInterval;
    }
  }

  private void FireOrangeOscillating() {
    if (OrangeSpecialBulletScene == null) return;

    float oscillation = OrangeOscillationAmp * Mathf.Sin(OrangeOscillationK * _orangeTimeAccumulator);

    for (int i = 0; i < OrangeBulletCount; ++i) {
      float baseAngle = (float) i / OrangeBulletCount * Mathf.Tau;
      float finalAngle = baseAngle + oscillation;
      Vector3 dir = new Vector3(Mathf.Cos(finalAngle), 0, Mathf.Sin(finalAngle));

      var b = OrangeSpecialBulletScene.Instantiate<SimpleBullet>();
      b.TimeScaleSensitivity = TimeScaleSensitivity;
      var player = PlayerNode;

      b.UpdateFunc = (t) => {
        var s = new SimpleBullet.UpdateState();
        s.position = (dir * (OrangeBulletSpeed * t)) with { Y = player.GlobalPosition.Y };
        return s;
      };
      GameRootProvider.CurrentGameRoot.AddChild(b);
    }
  }

  private void UpdateRedMechanic(float delta) {
    var player = GameRootProvider.CurrentGameRoot.GetNode<Player>("Player");
    if (IsInstanceValid(player) && !player.IsPermanentlyDead) {
      Vector3 pullDir = (Vector3.Zero - player.GlobalPosition).Normalized();
      pullDir.Y = 0;
      if (!pullDir.IsZeroApprox()) {
        player.GlobalPosition += pullDir * RedPushSpeed * delta;
      }
    }
  }

  private void TransitionTo(Phase nextPhase) {
    GD.Print($"Stellar Phase transitioning from {_currentPhase} to {nextPhase}");
    SoundManager.Instance.Play(SoundEffect.PowerUp);

    _currentPhase = nextPhase;
    UpdateTemperature();

    // 每次切换阶段都重置特殊机制计时器
    _greenTimer = GreenChainInterval;
    _yellowTimer = YellowBulletInterval;
    _orangeTimer = OrangeBulletInterval;
    _orangeTimeAccumulator = 0f;

    switch (_currentPhase) {
      case Phase.OrbiterWait:
        _timer = 1.0f;
        break;
      case Phase.Blue:
      case Phase.Green:
      case Phase.Yellow:
      case Phase.Orange:
      case Phase.Red:
        _currentPhaseLayerIndex = 0;
        _layersInCurrentColorPhase = LayerCounts[(int) _currentPhase - (int) Phase.Blue];
        PrepareNextLayer();
        _orbiterSpeed *= 1.3f;
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
              bullet.TargetHeight = 0f;
              bullet.SwitchToBlackHole();
            }
          }
        }
        break;
    }
  }

  private void UpdateLayerGeneration() {
    ProcessAngularFiring();

    bool currentLayerFired = _orbiters.All(orb =>
      !_orbiterFireIndices.ContainsKey(orb) ||
      _orbiterFireIndices[orb] >= _orbiterQueues[orb].Count
    );

    if (currentLayerFired) {
      if (_currentPhaseLayerIndex >= _layersInCurrentColorPhase - 1) {
        // 这是当前颜色阶段的最后一层, 等待所有子弹到位
        int lastRingIndex = _rings.Count - 1;
        if (lastRingIndex >= 0) {
          bool allArrived = _ringArrivalCounters.ContainsKey(lastRingIndex) &&
                            _ringArrivalCounters[lastRingIndex] >= _rings[lastRingIndex].Count;

          if (allArrived) {
            StartColorTransformation();
          }
        }
      } else {
        // 继续下一层
        ++_currentPhaseLayerIndex;
        PrepareNextLayer();
      }
    }
  }

  private void UpdateSupernova() {
    bool allSupernovasDone = _rings.SelectMany(r => r)
      .All(b => b.Type != PhaseStellarSmallBullet.BulletType.Supernova ||
        b.CurrentState == PhaseStellarSmallBullet.State.SupernovaHoming ||
        b.IsDestroyed);
    if (allSupernovasDone) {
      TransitionTo(Phase.BlackHole);
    }
  }

  private void UpdateBlackHole() {
    if (_timer <= 0) {
      FireJet();
      _timer = 0.2f;
    }
  }

  private void SpawnOrbiters() {
    for (int i = 0; i < OrbiterCount; ++i) {
      var orb = BigBulletScene.Instantiate<SimpleBullet>();
      int orbiterIndex = i;
      orb.TimeScaleSensitivity = TimeScaleSensitivity;
      orb.UpdateFunc = (t) => {
        float angle = _orbiterAngle + (Mathf.Tau / _orbiters.Count) * orbiterIndex;
        return new SimpleBullet.UpdateState {
          position = ParentBoss.GlobalPosition + new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * OrbiterRadius
        };
      };
      _orbiters.Add(orb);
      _orbiterQueues[orb] = new List<PhaseStellarSmallBullet>();
      _orbiterFireIndices[orb] = 0;
      GameRootProvider.CurrentGameRoot.AddChild(orb);
    }
  }

  private void PrepareNextLayer() {
    _orbiterAngle = 0;
    foreach (var o in _orbiters) {
      _orbiterFireIndices[o] = 0;
      _orbiterQueues[o].Clear();
    }

    var bulletColor = GetColorForPhase(_currentPhase);

    int rIdx = _rings.Count;
    float radius = Radius0 + rIdx * DeltaR;
    int count = Mathf.CeilToInt(Mathf.Tau * radius / BulletSpacing);
    float theta0 = GD.Randf() * Mathf.Tau;
    var ring = new List<PhaseStellarSmallBullet>();

    PhaseStellarSmallBullet last = null;
    for (int j = 0; j < count; ++j) {
      float theta = theta0 + (Mathf.Tau / count) * j;
      var b = SmallBulletScene.Instantiate<PhaseStellarSmallBullet>();

      b.TimeScaleSensitivity = TimeScaleSensitivity;
      b.CurrentColor = bulletColor;
      b.Type = _currentPhase <= Phase.Yellow ? PhaseStellarSmallBullet.BulletType.BlackHole : PhaseStellarSmallBullet.BulletType.Supernova;
      b.TargetPosition = ParentBoss.GlobalPosition + new Vector3(Mathf.Cos(theta), 0, Mathf.Sin(theta)) * radius;
      b.RingIndex = rIdx;
      b.RingRotationSpeed = RingRotationSpeed * (rIdx % 2 == 0 ? 1 : -1);
      b.SupernovaDelay = GD.Randf() * 5.0f;
      var orbIdx = j / 4 % OrbiterCount;
      var orbAng = (Mathf.Tau / _orbiters.Count) * orbIdx;
      if (j % 4 == 0) {
        b.MoveSpeed = Mathf.Max(SmallBulletSpeedMean * 0.5f, (float) GD.Randfn(SmallBulletSpeedMean, SmallBulletSpeedSigma));
        b.FireAngle = Mathf.PosMod(((float) GD.Randfn(theta, 0.5f)), Mathf.Tau);
        if (b.FireAngle < orbAng) b.FireAngle += Mathf.Tau;
      } else {
        b.MoveSpeed = last.MoveSpeed;
        b.FireAngle = last.FireAngle + 0.02f;
      }
      b.ReachedTarget += OnSmallBulletArrived;
      ring.Add(b);
      _orbiterQueues[_orbiters[orbIdx]].Add(b);
      GameRootProvider.CurrentGameRoot.AddChild(b);
      last = b;
    }
    _rings.Add(ring);
    _ringArrivalCounters[rIdx] = 0;
    foreach (var q in _orbiterQueues.Values) q.Sort((a, b) => a.FireAngle.CompareTo(b.FireAngle));
  }

  private void ProcessAngularFiring() {
    for (int orbiterIndex = 0; orbiterIndex < _orbiters.Count; ++orbiterIndex) {
      var o = _orbiters[orbiterIndex];
      var q = _orbiterQueues[o];
      int idx = _orbiterFireIndices[o];
      var oa = _orbiterAngle + (Mathf.Tau / _orbiters.Count) * orbiterIndex;
      while (idx < q.Count && oa >= q[idx].FireAngle) {
        var fa = q[idx].FireAngle;
        q[idx].Activate(new Vector3(Mathf.Cos(fa), 0, Mathf.Sin(fa)) * OrbiterRadius);
        ++idx;
        _orbiterFireIndices[o] = idx;
      }
    }
  }

  private void OnSmallBulletArrived(int ringIdx) {
    if (!_ringArrivalCounters.ContainsKey(ringIdx)) _ringArrivalCounters[ringIdx] = 0;
    ++_ringArrivalCounters[ringIdx];
    if (_ringArrivalCounters[ringIdx] >= _rings[ringIdx].Count) {
      if (ringIdx % 2 == 0) {
        _rotatingRingIndices.Add(ringIdx);
        foreach (var b in _rings[ringIdx]) b.StartRotation();

        float maxRadius = Radius0 + ringIdx * DeltaR;
        // 计算顶部环（i=0）相对于 maxRadius 的角度范围
        // i=0 对应顶部，i=ringIdx 对应底部（角度 0）
        float topAngle = Mathf.Acos(Mathf.Clamp(Radius0 / maxRadius, -1f, 1f));

        for (int i = 0; i < ringIdx; ++i) {
          // 使用角度插值来计算高度，而不是直接投影半径
          // i=0 (ratio=1) -> topAngle
          // i=ringIdx (ratio=0) -> 0
          float ratio = (float) (ringIdx - i) / ringIdx;
          float angle = ratio * topAngle;
          float h = maxRadius * Mathf.Sin(angle) * 0.5f;

          foreach (var b in _rings[i]) {
            b.TargetHeight = h;
          }
        }
      } else {
        foreach (var b in _rings[ringIdx]) b.Destroy();
        _rings[ringIdx].Clear();
      }
    }
  }

  private void StartColorTransformation() {
    _isTransformingColor = true;
    _colorTransformTarget = GetColorForPhase(_currentPhase + 1); // 目标是下一阶段的颜色
    _colorTransformRingIndex = _rings.Count - 1; // 从最外圈开始
    _colorTransformTimer = 0.05f;
  }

  private void ProcessColorTransformation(float scaledDelta) {
    _colorTransformTimer -= scaledDelta;
    if (_colorTransformTimer <= 0) {
      if (_colorTransformRingIndex >= 0) {
        foreach (var b in _rings[_colorTransformRingIndex])
          b.SetColor(_colorTransformTarget);
        --_colorTransformRingIndex;
        _colorTransformTimer = 0.04f;
      } else {
        _isTransformingColor = false;
        TransitionTo(_currentPhase + 1);
      }
    }
  }

  private void FireJet() {
    if (JetBulletScene == null) return;
    var jet = JetBulletScene.Instantiate<SimpleBullet>();
    Vector3 startPos = ParentBoss.GlobalPosition;
    Vector3 horizontalDir = new Vector3((float) GD.Randfn(0, 0.05f), 0, (float) GD.Randfn(0, 0.05f));
    float upAcceleration = 10.0f;
    float detonationHeight = 2.0f;

    jet.TimeScaleSensitivity = TimeScaleSensitivity;
    jet.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();
      float currentY = 0.5f * upAcceleration * t * t;
      s.position = startPos + horizontalDir * t + Vector3.Up * currentY;
      if (currentY >= detonationHeight) {
        s.destroy = true;
        CallDeferred(nameof(DetonateJet), s.position);
      }
      return s;
    };
    GameRootProvider.CurrentGameRoot.AddChild(jet);
  }

  private void DetonateJet(Vector3 explodePos) {
    if (RelativisticBulletScene == null) return;
    var player = GameRootProvider.CurrentGameRoot.GetNode<Player>("Player");
    var target = player.DecoyTarget ?? player;
    Vector3 targetPosOnGround = new Vector3(target.GlobalPosition.X, 0, target.GlobalPosition.Z);
    Vector3 direction = (targetPosOnGround - explodePos).Normalized();
    int fragmentCount = 12;
    for (int i = 0; i < fragmentCount; ++i) {
      var bullet = RelativisticBulletScene.Instantiate<SimpleBullet>();
      Vector3 spawnPos = explodePos + new Vector3((float) GD.Randfn(0, 0.3f), (float) GD.Randfn(0, 0.3f), (float) GD.Randfn(0, 0.3f));
      float speed = 6.0f;
      bullet.TimeScaleSensitivity = TimeScaleSensitivity;
      bullet.UpdateFunc = (t) => {
        var s = new SimpleBullet.UpdateState();
        s.position = spawnPos + direction * (speed * t);
        if (s.position.Y < 0) s.position.Y = 0;
        return s;
      };
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  private void FireDefensePattern() {
    Vector3 bossPos = ParentBoss.GlobalPosition;
    float speed = 5.0f;
    var baseDir = PlayerNode.GlobalPosition - bossPos;
    if (baseDir.IsZeroApprox())
      baseDir = Vector3.Right;
    else
      baseDir = baseDir.Normalized();
    for (int i = 0; i < 36; ++i) {
      var b = DefenseBulletScene.Instantiate<SimpleBullet>();
      float angle = Mathf.DegToRad(i * 10f);
      var dir = baseDir.Rotated(Vector3.Up, angle);
      b.TimeScaleSensitivity = TimeScaleSensitivity;
      b.UpdateFunc = (t) => new SimpleBullet.UpdateState { position = bossPos + dir * (speed * t) };
      GameRootProvider.CurrentGameRoot.AddChild(b);
    }
    SoundManager.Instance.Play(SoundEffect.FireBig);
  }

  private void UpdateTemperature() {
    Health = _currentPhase switch {
      Phase.OrbiterWait => 30000f,
      Phase.Blue => 20000f,
      Phase.Green => 10000f,
      Phase.Yellow => 6000f,
      Phase.Orange => 4000f,
      Phase.Red => 3000f,
      Phase.Supernova => 1e9f,
      Phase.BlackHole => 7.0f,
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
      _ => PhaseStellarSmallBullet.BulletColor.Red,
    };
  }

  public override RewindState CaptureInternalState() {
    var ringsIds = new Godot.Collections.Array<Godot.Collections.Array<ulong>>();
    foreach (var r in _rings) {
      var ids = new Godot.Collections.Array<ulong>();
      foreach (var b in r) ids.Add(b.GetInstanceId());
      ringsIds.Add(ids);
    }
    var queuesIds = new Godot.Collections.Dictionary<ulong, Godot.Collections.Array<ulong>>();
    foreach (var kv in _orbiterQueues) {
      var ids = new Godot.Collections.Array<ulong>();
      foreach (var b in kv.Value) if (IsInstanceValid(b)) ids.Add(b.GetInstanceId());
      queuesIds[kv.Key.GetInstanceId()] = ids;
    }
    var fireIndices = new Godot.Collections.Dictionary<ulong, int>();
    foreach (var kv in _orbiterFireIndices) fireIndices[kv.Key.GetInstanceId()] = kv.Value;

    return new PhaseStellarState {
      CurrentPhase = _currentPhase,
      Timer = _timer,
      OrbiterAngle = _orbiterAngle,
      OrbiterSpeed = _orbiterSpeed,
      DefenseTimer = _defenseTimer,
      CurrentPhaseLayerIndex = _currentPhaseLayerIndex,
      LayersInCurrentColorPhase = _layersInCurrentColorPhase,
      GreenTimer = _greenTimer,
      YellowTimer = _yellowTimer,
      OrangeTimer = _orangeTimer,
      OrangeTimeAccumulator = _orangeTimeAccumulator,
      Rings = ringsIds,
      RingArrivalCounters = new Godot.Collections.Dictionary<int, int>(_ringArrivalCounters),
      RotatingRingIndices = new Godot.Collections.Array<int>(_rotatingRingIndices),
      OrbiterQueues = queuesIds,
      OrbiterFireIndices = fireIndices
    };
  }

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseStellarState s) return;
    _currentPhase = s.CurrentPhase;
    _timer = s.Timer;
    _orbiterAngle = s.OrbiterAngle;
    _orbiterSpeed = s.OrbiterSpeed;
    _defenseTimer = s.DefenseTimer;
    _currentPhaseLayerIndex = s.CurrentPhaseLayerIndex;
    _layersInCurrentColorPhase = s.LayersInCurrentColorPhase;

    _greenTimer = s.GreenTimer;
    _yellowTimer = s.YellowTimer;
    _orangeTimer = s.OrangeTimer;
    _orangeTimeAccumulator = s.OrangeTimeAccumulator;

    _rings.Clear();
    foreach (var idList in s.Rings) {
      var ring = new List<PhaseStellarSmallBullet>();
      foreach (var id in idList) {
        if (InstanceFromId(id) is PhaseStellarSmallBullet b) ring.Add(b);
      }
      _rings.Add(ring);
    }
    _ringArrivalCounters.Clear();
    foreach (var kv in s.RingArrivalCounters) _ringArrivalCounters[kv.Key] = kv.Value;
    _rotatingRingIndices.Clear();
    foreach (var idx in s.RotatingRingIndices) _rotatingRingIndices.Add(idx);
    _orbiterQueues.Clear();
    foreach (var kv in s.OrbiterQueues) {
      if (InstanceFromId(kv.Key) is SimpleBullet o) {
        var q = new List<PhaseStellarSmallBullet>();
        foreach (var id in kv.Value) {
          if (InstanceFromId(id) is PhaseStellarSmallBullet b) q.Add(b);
        }
        _orbiterQueues[o] = q;
      }
    }
    _orbiterFireIndices.Clear();
    foreach (var kv in s.OrbiterFireIndices) {
      if (InstanceFromId(kv.Key) is SimpleBullet o) _orbiterFireIndices[o] = kv.Value;
    }
  }
}
