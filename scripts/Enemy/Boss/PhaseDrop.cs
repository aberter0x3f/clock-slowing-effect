using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseDropState : BasePhaseState {
  public PhaseDrop.AttackState CurrentState;
  public float Timer;
  public int WaveCounter;

  // Reticle states
  public bool ReticleLocked;
  public Vector3 LockedPlayerPos;
  public float ReticleAngle;
  public bool ReticleVisible;
  public float ReticleBlinkTimer;
  public bool IsReticleCircleActive;
  public bool IsReticleXActive;
  public float ReticleCircleDestroyTimer;

  // Boss Action states
  public Vector3 BossJumpStartPos;
  public Vector3 SmashLandPos;
  public Vector3 SmashDir;
  public int SmashStepIndex;
  public float SmashStepTimer;
}

public partial class PhaseDrop : BasePhase {
  public enum AttackState {
    Waiting,
    ReticleTracking,
    BossJump,
    BossSmash,
    Resetting
  }

  [ExportGroup("Reticle Settings")]
  [Export] public PackedScene ReticleBulletScene { get; set; } // 用于构成准星的子弹
  [Export] public PackedScene BurstBulletScene { get; set; } // 用于交点爆发
  [Export] public int BurstBulletCount { get; set; } = 40; // 用于交点爆发
  [Export] public int CirclePointCount { get; set; } = 40;
  [Export] public int PointsPerLine { get; set; } = 6;
  [Export] public float ReticleRadius { get; set; } = 1f;
  [Export] public int ReticleRotationQuarterRounds { get; set; } = 2;
  [Export] public float TrackingDuration { get; set; } = 1f;
  [Export] public float BlinkIntervalStart { get; set; } = 0.1f;
  [Export] public float BlinkIntervalEnd { get; set; } = 0.02f;

  [ExportGroup("Morph Settings")]
  [Export] public PackedScene MorphBulletScene { get; set; }
  [Export] public float MorphDuration { get; set; } = 1.0f;
  [Export] public float ExpandSpinSpeed { get; set; } = 5.0f;
  [Export] public float ExpandAcceleration { get; set; } = 5.0f;

  [ExportGroup("Boss Action")]
  [Export] public float JumpDuration { get; set; } = 2.2f;
  [Export] public float JumpHeight { get; set; } = 3f;
  [Export] public PackedScene JumpRingBulletScene { get; set; }
  [Export] public int JumpRingCount { get; set; } = 200;
  [Export] public float JumpRingSpeed { get; set; } = 1.5f;

  [ExportGroup("Smash Attack")]
  [Export] public PackedScene SmashBulletScene { get; set; }
  [Export] public float SmashStepInterval { get; set; } = 0.05f;
  [Export] public float SmashStepDistance { get; set; } = 0.5f;
  [Export] public float ResetTimer { get; set; } = 1f; // 一波攻击后的休息时间
  [Export] public float SmashBulletUpSpeedMean { get; set; } = 2.0f;
  [Export] public float SmashBulletUpSpeedSigma { get; set; } = 0.5f;
  [Export] public float SmashBulletPlanarSpeedMean { get; set; } = 4f;
  [Export] public float SmashBulletPlanarSpeedSigma { get; set; } = 0.5f;
  [Export] public float SmashBulletAngleSigmaDeg { get; set; } = 15.0f;
  [Export] public float SmashBulletGravity { get; set; } = 6.0f;

  private AttackState _currentState = AttackState.Waiting;
  private float _timer;

  // Reticle logic state
  private bool _reticleLocked;
  private Vector3 _lockedPlayerPos;
  private float _reticleAngle;
  private bool _reticleVisible;
  private float _reticleBlinkTimer;
  // 控制子弹存活的开关
  private bool _isReticleCircleActive;
  private bool _isReticleXActive;
  private float _reticleCircleDestroyTimer; // 用于 Morph 完成后的销毁倒计时

  // Boss Jump/Smash logic
  private Vector3 _bossJumpStartPos;
  private Vector3 _smashLandPos;
  private Vector3 _smashDir;
  private int _smashStepIndex;
  private float _smashStepTimer;

  private MapGenerator _mapGenerator;
  private Rect2 _mapBounds;

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _mapGenerator = GetTree().Root.GetNode<MapGenerator>("GameRoot/MapGenerator");
    float w = _mapGenerator.MapWidth * _mapGenerator.TileSize;
    float h = _mapGenerator.MapHeight * _mapGenerator.TileSize;
    _mapBounds = new Rect2(-w / 2, -h / 2, w, h);

    var rank = GameManager.Instance.EnemyRank;
    float scale = rank / 5.0f;
    TrackingDuration /= scale;
    MorphDuration /= scale;
    JumpDuration /= scale;
    ExpandAcceleration *= scale;
    ExpandSpinSpeed *= scale;
    ResetTimer /= scale;
    SmashStepInterval /= scale;

    StartSequence();
  }

  private void StartSequence() {
    _currentState = AttackState.Waiting;
    _timer = 1.0f;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    // 持续更新准星圆环的销毁计时器（如果已锁定）
    if (_reticleLocked && _isReticleCircleActive) {
      _reticleCircleDestroyTimer -= scaledDelta;
      if (_reticleCircleDestroyTimer <= 0) {
        _isReticleCircleActive = false; // 这将通知所有圆环子弹自我销毁
      }
    }

    switch (_currentState) {
      case AttackState.Waiting:
        _timer -= scaledDelta;
        if (_timer <= 0) {
          InitializeReticleSequence();
          _currentState = AttackState.ReticleTracking;
          _timer = TrackingDuration;
        }
        break;

      case AttackState.ReticleTracking:
        _timer -= scaledDelta;
        UpdateReticleState(scaledDelta);

        if (_timer <= 0) {
          LockReticle();
          // 锁定后不等待 Morph 动画结束，直接进入 Boss 跳跃阶段（并行）
          // 但设置 Morph 时长作为圆环存在的剩余时间
          _reticleCircleDestroyTimer = MorphDuration;

          PrepareBossJump();
          _currentState = AttackState.BossJump;
          _timer = JumpDuration;
        }
        break;

      case AttackState.BossJump:
        _timer -= scaledDelta;

        float t = Mathf.Clamp(1.0f - (_timer / JumpDuration), 0, 1);
        Vector3 currentPos;

        if (t < 1.0f / 3.0f) {
          // 前 1/3：静止
          currentPos = _bossJumpStartPos;
        } else if (t < 2.0f / 3.0f) {
          // 中 1/3：跳到玩家头顶指定位置
          float p = (t - 1.0f / 3.0f) * 3.0f;
          currentPos = _bossJumpStartPos.Lerp(_lockedPlayerPos, p);
          currentPos.Y = Mathf.Sin(p * Mathf.Pi * 0.5f) * JumpHeight;
        } else {
          // 后 1/3：做带有初速度的向下加速运动砸下来
          float p = (t - 2.0f / 3.0f) * 3.0f;
          currentPos = _lockedPlayerPos;
          float fallProgress = 0.2f * p + 0.8f * p * p;
          currentPos.Y = Mathf.Lerp(JumpHeight, 0, fallProgress);
        }

        ParentBoss.GlobalPosition = currentPos;

        if (_timer <= 0) {
          // 落地
          ParentBoss.GlobalPosition = _lockedPlayerPos;
          OnBossLanded();
          _currentState = AttackState.BossSmash;
          _timer = 0;
        }
        break;

      case AttackState.BossSmash:
        UpdateSmashLogic(scaledDelta);
        break;

      case AttackState.Resetting:
        _timer -= scaledDelta;
        if (_timer <= 0) {
          StartSequence();
        }
        break;
    }
  }

  private void InitializeReticleSequence() {
    _reticleLocked = false;
    _reticleAngle = 0f;
    _reticleVisible = true;
    _reticleBlinkTimer = BlinkIntervalStart;
    _isReticleCircleActive = true;
    _isReticleXActive = true;

    SpawnReticleBullets();
  }

  private void SpawnReticleBullets() {
    // 圆形子弹
    for (int i = 0; i < CirclePointCount; ++i) {
      var b = ReticleBulletScene.Instantiate<SimpleBullet>();
      // 闭包捕获索引和当前 PhaseDrop 实例
      // 这样回溯后，只要 PhaseDrop 状态恢复，子弹就能算出正确位置
      int index = i;
      b.UpdateFunc = (t) => CalculateCircleBulletState(index);
      GameRootProvider.CurrentGameRoot.AddChild(b);
    }

    // X 形子弹
    for (int line = 0; line < 2; ++line) {
      for (int i = 0; i < PointsPerLine; ++i) {
        // 4 个臂
        for (int dir = 0; dir < 2; ++dir) {
          var b = ReticleBulletScene.Instantiate<SimpleBullet>();
          int l = line;
          int ptIdx = i;
          int d = dir;
          b.UpdateFunc = (t) => CalculateXBulletState(l, ptIdx, d);
          GameRootProvider.CurrentGameRoot.AddChild(b);
        }
      }
    }
  }

  /// <summary>
  /// 仅更新用于计算位置的状态变量，不直接操作子弹．
  /// 子弹会通过 UpdateFunc 读取这些变量．
  /// </summary>
  private void UpdateReticleState(float dt) {
    // 旋转
    if (_reticleLocked) return;

    _reticleAngle += ReticleRotationQuarterRounds * Mathf.Pi / 2 / TrackingDuration * dt;

    // 闪烁逻辑
    float progress = 1.0f - (_timer / TrackingDuration);
    float currentInterval = Mathf.Lerp(BlinkIntervalStart, BlinkIntervalEnd, progress * progress);

    _reticleBlinkTimer -= dt;
    if (_reticleBlinkTimer <= 0) {
      _reticleBlinkTimer = currentInterval;
      _reticleVisible = !_reticleVisible;
    }
  }

  private SimpleBullet.UpdateState CalculateCircleBulletState(int index) {
    if (!_isReticleCircleActive) return new SimpleBullet.UpdateState { destroy = true };

    SimpleBullet.UpdateState s = new();

    // 确定中心
    Vector3 center = _reticleLocked ? _lockedPlayerPos : PlayerNode.GlobalPosition;
    center.Y = 0;

    // 确定旋转
    float baseAngle = (Mathf.Tau / CirclePointCount) * index;
    // 如果未锁定则旋转，锁定后保持当前角度 (_reticleAngle 不再更新)
    float finalAngle = baseAngle + _reticleAngle;

    Vector3 offset = new Vector3(Mathf.Cos(finalAngle), 0, Mathf.Sin(finalAngle)) * ReticleRadius;
    s.position = center + offset;

    // 确定可见性
    s.modulate = (_reticleVisible || _reticleLocked) ? Colors.White : new Color(1, 1, 1, 0.2f);

    return s;
  }

  private SimpleBullet.UpdateState CalculateXBulletState(int line, int ptIdx, int dir) {
    if (!_isReticleXActive) return new SimpleBullet.UpdateState { destroy = true };

    SimpleBullet.UpdateState s = new();

    Vector3 center = _reticleLocked ? _lockedPlayerPos : PlayerNode.GlobalPosition;
    center.Y = 0;

    // 计算臂的角度
    // line 0: 0 & pi, line 1: pi/2 & 3pi/2.
    // dir 0: +, dir 1: - (adding pi)
    float armBaseAngle = (line * Mathf.Pi / 2.0f) + (dir * Mathf.Pi);
    float finalArmAngle = _reticleAngle + armBaseAngle;

    Vector3 direction = new Vector3(Mathf.Cos(finalArmAngle), 0, Mathf.Sin(finalArmAngle));
    // 从中心向外分布
    float dist = Mathf.Lerp(0.7f, 1.5f, ptIdx / (PointsPerLine - 1f)) * ReticleRadius;

    s.position = center + direction * dist;
    s.modulate = (_reticleVisible || _reticleLocked) ? Colors.White : new Color(1, 1, 1, 0.2f);

    return s;
  }

  private void LockReticle() {
    _reticleLocked = true;
    _lockedPlayerPos = PlayerNode.GlobalPosition;
    _lockedPlayerPos.Y = 0;
    _isReticleXActive = false; // X 形子弹立即销毁

    SoundManager.Instance.Play(SoundEffect.FireSmall);

    // 在四个交点处发射爆发子弹
    for (int arm = 0; arm < 4; ++arm) {
      float armAngle = _reticleAngle + (Mathf.Pi / 2.0f) * arm;
      Vector3 dir = new Vector3(Mathf.Cos(armAngle), 0, Mathf.Sin(armAngle));
      Vector3 spawnPos = _lockedPlayerPos + dir * ReticleRadius;
      SpawnLinearBurst(spawnPos, dir);
    }

    // 生成 Morph 子弹
    SpawnMorphRing();
  }

  private void SpawnLinearBurst(Vector3 pos, Vector3 dir) {
    if (BurstBulletScene == null) return;
    for (int i = 0; i < BurstBulletCount; ++i) {
      var b = BurstBulletScene.Instantiate<SimpleBullet>();
      float speed = 3.0f + i * 0.2f;
      b.UpdateFunc = (t) => new SimpleBullet.UpdateState { position = pos + dir * (speed * t) };
      GameRootProvider.CurrentGameRoot.AddChild(b);
    }
  }

  private void SpawnMorphRing() {
    if (MorphBulletScene == null) return;

    int count = CirclePointCount;
    Vector3 A = ParentBoss.GlobalPosition;
    Vector3 B = _lockedPlayerPos;

    for (int i = 0; i < count; ++i) {
      // s 映射 -1 到 1
      float s = (float) i / (count - 1) * 2.0f - 1.0f;

      var b = MorphBulletScene.Instantiate<PhaseDropMorphBullet>();
      b.PointA = A;
      b.PointB = B;
      b.Radius = ReticleRadius;
      b.SParameter = s;
      b.MorphDuration = MorphDuration;
      b.SpinSpeed = ExpandSpinSpeed;
      b.ExpandAcceleration = ExpandAcceleration;
      b.MapBounds = _mapBounds;

      GameRootProvider.CurrentGameRoot.AddChild(b);
    }
  }

  private void PrepareBossJump() {
    _bossJumpStartPos = ParentBoss.GlobalPosition;
    SoundManager.Instance.Play(SoundEffect.FireBig);
    FireJumpRing();
  }

  private void FireJumpRing() {
    if (JumpRingBulletScene == null) return;
    Vector3 pos = ParentBoss.GlobalPosition;
    for (int i = 0; i < JumpRingCount; ++i) {
      float angle = Mathf.Tau / JumpRingCount * i;
      Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
      var b = JumpRingBulletScene.Instantiate<SimpleBullet>();
      var s = JumpRingSpeed;
      b.UpdateFunc = (t) => new SimpleBullet.UpdateState { position = pos + dir * (s * t) };
      GameRootProvider.CurrentGameRoot.AddChild(b);
    }
  }

  private void OnBossLanded() {
    SoundManager.Instance.Play(SoundEffect.BossDeath);

    // 落地后确定砸击延伸方向 (指向当前玩家)
    _smashDir = PlayerNode.GlobalPosition - _lockedPlayerPos;
    _smashDir.Y = 0;
    if (_smashDir.IsZeroApprox()) _smashDir = Vector3.Right;
    else _smashDir = _smashDir.Normalized();

    _smashLandPos = _lockedPlayerPos;
    _smashStepIndex = 0;
    _smashStepTimer = 0;
  }

  private void UpdateSmashLogic(float dt) {
    _smashStepTimer -= dt;
    if (_smashStepTimer <= 0) {
      _smashStepTimer = SmashStepInterval;

      // 在中心两侧生成
      float offset = _smashStepIndex * SmashStepDistance;
      bool spawnedAny = false;

      // 正方向
      Vector3 pos1 = _smashLandPos + _smashDir * offset;
      if (_mapBounds.HasPoint(new Vector2(pos1.X, pos1.Z))) {
        SpawnSmashBullet(pos1);
        spawnedAny = true;
      }

      // 负方向 (index > 0 时避免中心重复)
      if (_smashStepIndex > 0) {
        Vector3 pos2 = _smashLandPos - _smashDir * offset;
        if (_mapBounds.HasPoint(new Vector2(pos2.X, pos2.Z))) {
          SpawnSmashBullet(pos2);
          spawnedAny = true;
        }
      }

      if (!spawnedAny) {
        // 两侧都出界，结束
        _currentState = AttackState.Resetting;
        _timer = ResetTimer;
      }

      ++_smashStepIndex;
    }
  }

  private void SpawnSmashBullet(Vector3 pos) {
    var b = SmashBulletScene.Instantiate<SimpleBullet>();

    float upSpeed = Mathf.Max(0, (float) GD.Randfn(SmashBulletUpSpeedMean, SmashBulletUpSpeedSigma));
    float planarSpeed = Mathf.Max(0, (float) GD.Randfn(SmashBulletPlanarSpeedMean, SmashBulletPlanarSpeedSigma));
    float gravity = SmashBulletGravity;

    Vector3 dir1 = _smashDir.Rotated(Vector3.Up, Mathf.Pi / 2.0f);
    Vector3 dir2 = _smashDir.Rotated(Vector3.Up, -Mathf.Pi / 2.0f);
    Vector3 baseDir = (GD.Randf() > 0.5f) ? dir1 : dir2;
    float angleOffset = Mathf.DegToRad((float) GD.Randfn(0, SmashBulletAngleSigmaDeg));
    Vector3 finalDir = baseDir.Rotated(Vector3.Up, angleOffset);

    b.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();
      float y = upSpeed * t - 0.5f * gravity * t * t;
      s.position = pos + finalDir * (planarSpeed * t);
      s.position.Y = Mathf.Max(0, y);
      return s;
    };
    GameRootProvider.CurrentGameRoot.AddChild(b);
  }

  public override RewindState CaptureInternalState() {
    return new PhaseDropState {
      CurrentState = _currentState,
      Timer = _timer,
      ReticleLocked = _reticleLocked,
      LockedPlayerPos = _lockedPlayerPos,
      ReticleAngle = _reticleAngle,
      ReticleVisible = _reticleVisible,
      ReticleBlinkTimer = _reticleBlinkTimer,
      IsReticleCircleActive = _isReticleCircleActive,
      IsReticleXActive = _isReticleXActive,
      ReticleCircleDestroyTimer = _reticleCircleDestroyTimer,
      BossJumpStartPos = _bossJumpStartPos,
      SmashLandPos = _smashLandPos,
      SmashDir = _smashDir,
      SmashStepIndex = _smashStepIndex,
      SmashStepTimer = _smashStepTimer
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseDropState s) return;

    _currentState = s.CurrentState;
    _timer = s.Timer;
    _reticleLocked = s.ReticleLocked;
    _lockedPlayerPos = s.LockedPlayerPos;
    _reticleAngle = s.ReticleAngle;
    _reticleVisible = s.ReticleVisible;
    _reticleBlinkTimer = s.ReticleBlinkTimer;
    _isReticleCircleActive = s.IsReticleCircleActive;
    _isReticleXActive = s.IsReticleXActive;
    _reticleCircleDestroyTimer = s.ReticleCircleDestroyTimer;
    _bossJumpStartPos = s.BossJumpStartPos;
    _smashLandPos = s.SmashLandPos;
    _smashDir = s.SmashDir;
    _smashStepIndex = s.SmashStepIndex;
    _smashStepTimer = s.SmashStepTimer;
  }
}
