using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseThreeBodyState : BasePhaseState {
  public PhaseThreeBody.BossState CurrentBossState;
  public int OrbitFireStateIndex;
  public float OrbitAngle;
  public float LastFireAngle;
  public float ChargeTimer;
  public Vector2 ChargeTargetPosition;
  public Vector2 ChargeVelocity;
  public float RecoveryTimer;
}

public partial class PhaseThreeBody : BasePhase {
  public enum BossState {
    Idle,
    Charging,
    Recovering
  }

  public override float MaxHealth { get; protected set; } = 50f;

  // --- 状态变量 ---
  private BossState _currentBossState = BossState.Idle;
  private int _orbitFireStateIndex = 0;
  private float _orbitAngle = 0f;
  private float _lastFireAngle = 0f;
  private float _chargeTimer;
  private Vector2 _chargeDirection;
  private Vector2 _chargeVelocity;
  private float _recoveryTimer;
  private BaseBullet _bigABullet;
  private BaseBullet _bigBBullet;

  private MapGenerator _mapGenerator;
  private readonly RandomNumberGenerator _rng = new();

  [ExportGroup("Scene References")]
  [Export]
  public PackedScene BigBulletAScene { get; set; }
  [Export]
  public PackedScene BigBulletBScene { get; set; }
  [Export]
  public PackedScene SmallBulletAScene { get; set; }
  [Export]
  public PackedScene SmallBulletBScene { get; set; }
  [Export]
  public PackedScene WallBulletScene { get; set; }

  [ExportGroup("Orbiting Bullets")]
  [Export(PropertyHint.Range, "50, 500, 10")]
  public float OrbitRadius { get; set; } = 100f;
  [Export(PropertyHint.Range, "0.1, 10.0, 0.1")]
  public float OrbitSpeed { get; set; } = 3.0f; // Radians per second
  [Export(PropertyHint.Range, "1, 50, 1")]
  public int SmallBulletCount { get; set; } = 30;

  [ExportGroup("Charge Attack")]
  [Export(PropertyHint.Range, "1.0, 20.0, 0.5")]
  public float ChargeInterval { get; set; } = 1f;
  [Export(PropertyHint.Range, "100, 5000, 100")]
  public float ChargeAcceleration { get; set; } = 400f;
  [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
  public float RecoveryDuration { get; set; } = 1f;

  [ExportGroup("Wall Impact Bullets")]
  [Export(PropertyHint.Range, "1, 100, 1")]
  public int WallBulletCountPerSide { get; set; } = 40;
  [Export(PropertyHint.Range, "0, 90, 1")]
  public float WallBulletAngleSigma { get; set; } = 5.0f;
  [Export]
  public float WallBulletInitialSpeedMean { get; set; } = 150f;
  [Export]
  public float WallBulletInitialSpeedSigma { get; set; } = 30f;
  [Export]
  public float WallBulletAccelerationMean { get; set; } = 50f;
  [Export]
  public float WallBulletAccelerationSigma { get; set; } = 10f;
  [Export]
  public float WallBulletMaxSpeed { get; set; } = 300f;

  public override void StartPhase(Boss parent) {
    base.StartPhase(parent);
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr("PhaseThreeBody: MapGenerator not found. Phase cannot start.");
      EndPhase();
      return;
    }

    // 根据难度调整
    OrbitSpeed *= (GameManager.Instance.EnemyRank + 5) / 10f;
    SmallBulletCount = Mathf.RoundToInt(SmallBulletCount * (GameManager.Instance.EnemyRank + 3) / 8f);
    WallBulletCountPerSide = Mathf.RoundToInt(WallBulletCountPerSide * (GameManager.Instance.EnemyRank + 3) / 8f);

    // 实例化卫星
    _bigABullet = BigBulletAScene.Instantiate<BaseBullet>();
    _bigBBullet = BigBulletBScene.Instantiate<BaseBullet>();
    UpdateOrbitingBulletPositions();
    AddChild(_bigABullet);
    AddChild(_bigBBullet);

    // 初始化状态
    _chargeTimer = ChargeInterval;
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) {
      UpdateOrbitingBulletPositions();
      return;
    }
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    // 更新卫星轨道
    _orbitAngle += OrbitSpeed * scaledDelta;
    UpdateOrbitingBulletPositions();

    // 处理 Boss 自身的状态机
    switch (_currentBossState) {
      case BossState.Idle:
        _chargeTimer -= scaledDelta;
        if (_chargeTimer <= 0) {
          _chargeDirection = PlayerNode.GlobalPosition - ParentBoss.GlobalPosition;
          if (!_chargeDirection.IsZeroApprox()) {
            _chargeDirection = _chargeDirection.Normalized();
          }
          _chargeVelocity = Vector2.Zero;
          _currentBossState = BossState.Charging;
        }
        break;
      case BossState.Recovering:
        _recoveryTimer -= scaledDelta;
        if (_recoveryTimer <= 0) {
          _currentBossState = BossState.Idle;
          _chargeTimer = ChargeInterval;
        }
        break;
    }

    // 处理卫星开火逻辑
    if (_orbitAngle > _lastFireAngle + Mathf.Pi / 2) {
      _lastFireAngle += Mathf.Pi / 2;

      // 热身阶段结束后才开火
      if (_lastFireAngle >= 0) {
        switch (_orbitFireStateIndex) {
          case 0: // bigA fires
            FireSmallABullets();
            break;
          case 1: // bigB fires
            FireSmallBBullets();
            break;
          case 2: // do nothing
            break;
        }
        _orbitFireStateIndex = (_orbitFireStateIndex + 1) % 3;
      }
    }
  }

  public override void _PhysicsProcess(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    if (_currentBossState == BossState.Charging) {
      _chargeVelocity += _chargeDirection * ChargeAcceleration * scaledDelta;
      ParentBoss.Velocity = _chargeVelocity * TimeManager.Instance.TimeScale;
      ParentBoss.MoveAndSlide();

      if (ParentBoss.GetSlideCollisionCount() > 0) {
        ParentBoss.Velocity = Vector2.Zero;
        _chargeVelocity = Vector2.Zero;
        _currentBossState = BossState.Recovering;
        _recoveryTimer = RecoveryDuration;
        FireWallBulletLine();
      }
    } else {
      ParentBoss.Velocity = Vector2.Zero;
      ParentBoss.MoveAndSlide();
    }
  }

  private void UpdateOrbitingBulletPositions() {
    if (!IsInstanceValid(_bigABullet) || !IsInstanceValid(_bigBBullet)) return;

    var offsetA = new Vector2(Mathf.Cos(_orbitAngle) * OrbitRadius, Mathf.Sin(_orbitAngle) * OrbitRadius);
    var offsetB = new Vector2(Mathf.Cos(_orbitAngle + Mathf.Pi) * OrbitRadius, Mathf.Sin(_orbitAngle + Mathf.Pi) * OrbitRadius);

    _bigABullet.GlobalPosition = ParentBoss.GlobalPosition + offsetA;
    _bigBBullet.GlobalPosition = ParentBoss.GlobalPosition + offsetB;
  }

  private Vector2 GetBigABullet2DPosition() {
    var offset = new Vector2(Mathf.Cos(_orbitAngle), Mathf.Sin(_orbitAngle)) * OrbitRadius;
    return ParentBoss.GlobalPosition + offset;
  }

  private Vector2 GetBigBBullet2DPosition() {
    var offset = new Vector2(Mathf.Cos(_orbitAngle + Mathf.Pi), Mathf.Sin(_orbitAngle + Mathf.Pi)) * OrbitRadius;
    return ParentBoss.GlobalPosition + offset;
  }

  private void FireSmallABullets() {
    var startPos = GetBigABullet2DPosition();
    var directionToPlayer = (PlayerNode.GlobalPosition - startPos).Normalized();
    float baseAngle = directionToPlayer.Angle();
    float angleStep = Mathf.Tau / SmallBulletCount;

    for (int i = 0; i < SmallBulletCount; ++i) {
      var bullet = SmallBulletAScene.Instantiate<SimpleBullet>();
      float angle = baseAngle + i * angleStep;
      var direction = Vector2.Right.Rotated(angle);
      bullet.GlobalPosition = startPos;
      bullet.Rotation = direction.Angle();
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  private void FireSmallBBullets() {
    var startPos = GetBigBBullet2DPosition();
    var directionToPlayer = (PlayerNode.GlobalPosition - startPos).Normalized();
    float angleStep = Mathf.Tau / SmallBulletCount;
    float baseAngle = directionToPlayer.Angle() + angleStep / 2.0f; // 偏移半步，保证玩家在空隙中

    for (int i = 0; i < SmallBulletCount; ++i) {
      var bullet = SmallBulletBScene.Instantiate<SimpleBullet>();
      float angle = baseAngle + i * angleStep;
      var direction = Vector2.Right.Rotated(angle);
      bullet.GlobalPosition = startPos;
      bullet.Rotation = direction.Angle();
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  private void FireWallBulletLine() {
    var pos = ParentBoss.GlobalPosition;
    float mapHalfWidth = _mapGenerator.MapWidth * _mapGenerator.TileSize / 2.0f;
    float mapHalfHeight = _mapGenerator.MapHeight * _mapGenerator.TileSize / 2.0f;

    float distToTop = Mathf.Abs(pos.Y - (-mapHalfHeight));
    float distToBottom = Mathf.Abs(pos.Y - mapHalfHeight);
    float distToLeft = Mathf.Abs(pos.X - (-mapHalfWidth));
    float distToRight = Mathf.Abs(pos.X - mapHalfWidth);

    float min = Mathf.Min(Mathf.Min(distToTop, distToBottom), Mathf.Min(distToLeft, distToRight));

    Vector2 start, end, direction;
    if (Mathf.IsEqualApprox(min, distToTop)) {
      start = new Vector2(-mapHalfWidth, -mapHalfHeight);
      end = new Vector2(mapHalfWidth, -mapHalfHeight);
      direction = Vector2.Down;
    } else if (Mathf.IsEqualApprox(min, distToBottom)) {
      start = new Vector2(-mapHalfWidth, mapHalfHeight);
      end = new Vector2(mapHalfWidth, mapHalfHeight);
      direction = Vector2.Up;
    } else if (Mathf.IsEqualApprox(min, distToLeft)) {
      start = new Vector2(-mapHalfWidth, -mapHalfHeight);
      end = new Vector2(-mapHalfWidth, mapHalfHeight);
      direction = Vector2.Right;
    } else {
      start = new Vector2(mapHalfWidth, -mapHalfHeight);
      end = new Vector2(mapHalfWidth, mapHalfHeight);
      direction = Vector2.Left;
    }

    for (int i = 0; i < WallBulletCountPerSide; ++i) {
      var bullet = WallBulletScene.Instantiate<SimpleBullet>();
      bullet.GlobalPosition = start.Lerp(end, (float) i / (WallBulletCountPerSide - 1));

      float randomAngle = (float) _rng.Randfn(0, Mathf.DegToRad(WallBulletAngleSigma));
      var finalDirection = direction.Rotated(randomAngle);

      bullet.Rotation = finalDirection.Angle();
      bullet.InitialSpeed = (float) _rng.Randfn(WallBulletInitialSpeedMean, WallBulletInitialSpeedSigma);
      bullet.SameDirectionAcceleration = (float) _rng.Randfn(WallBulletAccelerationMean, WallBulletAccelerationSigma);
      bullet.MaxSpeed = WallBulletMaxSpeed;

      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  public override RewindState CaptureInternalState() {
    return new PhaseThreeBodyState {
      CurrentBossState = this._currentBossState,
      OrbitFireStateIndex = this._orbitFireStateIndex,
      OrbitAngle = this._orbitAngle,
      LastFireAngle = this._lastFireAngle,
      ChargeTimer = this._chargeTimer,
      ChargeTargetPosition = this._chargeDirection,
      ChargeVelocity = this._chargeVelocity,
      RecoveryTimer = this._recoveryTimer,
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseThreeBodyState pts) return;

    this._currentBossState = pts.CurrentBossState;
    this._orbitFireStateIndex = pts.OrbitFireStateIndex;
    this._orbitAngle = pts.OrbitAngle;
    this._lastFireAngle = pts.LastFireAngle;
    this._chargeTimer = pts.ChargeTimer;
    this._chargeDirection = pts.ChargeTargetPosition;
    this._chargeVelocity = pts.ChargeVelocity;
    this._recoveryTimer = pts.RecoveryTimer;
  }
}
