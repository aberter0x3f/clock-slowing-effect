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
  public Vector3 ChargeDirection;
  public Vector3 ChargeVelocity;
  public float RecoveryTimer;
}

public partial class PhaseThreeBody : BasePhase {
  public enum BossState {
    Idle,
    Charging,
    Recovering
  }

  public override float MaxHealth { get; protected set; } = 50f;

  private BossState _currentBossState = BossState.Idle;
  private int _orbitFireStateIndex = 0;
  private float _orbitAngle = 0f;
  private float _lastFireAngle = 0f;
  private float _chargeTimer;
  private Vector3 _chargeDirection;
  private Vector3 _chargeVelocity;
  private float _recoveryTimer;

  private SimpleBullet _bigABullet;
  private SimpleBullet _bigBBullet;

  private MapGenerator _mapGenerator;
  private float _mapHalfWidth;
  private float _mapHalfHeight;

  [ExportGroup("Scene References")]
  [Export] public PackedScene BigBulletAScene { get; set; }
  [Export] public PackedScene BigBulletBScene { get; set; }
  [Export] public PackedScene SmallBulletAScene { get; set; }
  [Export] public PackedScene SmallBulletBScene { get; set; }
  [Export] public PackedScene WallBulletScene { get; set; }

  [ExportGroup("Orbiting Bullets")]
  [Export] public float OrbitRadius { get; set; } = 1.0f;
  [Export] public float OrbitSpeed { get; set; } = 3.0f;
  [Export] public int SmallBulletCount { get; set; } = 30;

  [ExportGroup("Charge Attack")]
  [Export] public float ChargeInterval { get; set; } = 1f;
  [Export] public float ChargeAcceleration { get; set; } = 4.0f;
  [Export] public float RecoveryDuration { get; set; } = 1f;

  [ExportGroup("Wall Impact Bullets")]
  [Export] public int WallBulletCountPerSide { get; set; } = 40;
  [Export] public float WallBulletAngleSigma { get; set; } = 20.0f;
  [Export] public float WallBulletInitialSpeedMean { get; set; } = 1.5f;
  [Export] public float WallBulletInitialSpeedSigma { get; set; } = 0.3f;
  [Export] public float WallBulletAccelerationMean { get; set; } = 0.5f;
  [Export] public float WallBulletMaxSpeed { get; set; } = 3.0f;

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _mapGenerator = GetTree().Root.GetNode<MapGenerator>("GameRoot/MapGenerator");

    // -1 是因为地图最外圈一格是墙
    _mapHalfWidth = (_mapGenerator.MapWidth / 2f - 1) * _mapGenerator.TileSize;
    _mapHalfHeight = (_mapGenerator.MapHeight / 2f - 1) * _mapGenerator.TileSize;

    var rank = GameManager.Instance.EnemyRank;
    OrbitSpeed *= (rank + 5) / 10f;
    SmallBulletCount = Mathf.RoundToInt(SmallBulletCount * (rank + 3) / 8f);
    WallBulletCountPerSide = Mathf.RoundToInt(WallBulletCountPerSide * (rank + 3) / 8f);

    SpawnSatellites();
    _chargeTimer = ChargeInterval;
  }

  private void SpawnSatellites() {
    var gr = GameRootProvider.CurrentGameRoot;

    // 卫星 A
    _bigABullet = BigBulletAScene.Instantiate<SimpleBullet>();
    _bigABullet.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();
      s.position = ParentBoss.GlobalPosition + new Vector3(Mathf.Cos(_orbitAngle), 0, Mathf.Sin(_orbitAngle)) * OrbitRadius;
      return s;
    };
    gr.AddChild(_bigABullet);

    // 卫星 B
    _bigBBullet = BigBulletBScene.Instantiate<SimpleBullet>();
    _bigBBullet.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();
      s.position = ParentBoss.GlobalPosition + new Vector3(Mathf.Cos(_orbitAngle + Mathf.Pi), 0, Mathf.Sin(_orbitAngle + Mathf.Pi)) * OrbitRadius;
      return s;
    };
    gr.AddChild(_bigBBullet);
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    // 更新轨道角度
    _orbitAngle += OrbitSpeed * scaledDelta;

    // Boss 行为状态机
    switch (_currentBossState) {
      case BossState.Idle:
        _chargeTimer -= scaledDelta;
        if (_chargeTimer <= 0) {
          _chargeDirection = (PlayerNode.GlobalPosition - ParentBoss.GlobalPosition).Normalized();
          _chargeVelocity = Vector3.Zero;
          _currentBossState = BossState.Charging;
        }
        break;

      case BossState.Charging:
        // 手动更新速度和位置
        _chargeVelocity += _chargeDirection * ChargeAcceleration * scaledDelta;
        ParentBoss.GlobalPosition += _chargeVelocity * scaledDelta;

        // 手动进行边界检测
        if (CheckForWallCollision()) {
          _chargeVelocity = Vector3.Zero;
          // 将 Boss 位置钳制在边界内，防止穿墙
          var clampedPos = ParentBoss.GlobalPosition;
          clampedPos.X = Mathf.Clamp(clampedPos.X, -_mapHalfWidth, _mapHalfWidth);
          clampedPos.Z = Mathf.Clamp(clampedPos.Z, -_mapHalfHeight, _mapHalfHeight);
          ParentBoss.GlobalPosition = clampedPos;

          _currentBossState = BossState.Recovering;
          _recoveryTimer = RecoveryDuration;
          FireWallBulletLine();
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

    // 卫星射击逻辑
    if (_orbitAngle > _lastFireAngle + Mathf.Pi / 2) {
      _lastFireAngle += Mathf.Pi / 2;
      switch (_orbitFireStateIndex) {
        case 0: FireSmallBullets(_bigABullet, SmallBulletAScene, 0f); break;
        case 1: FireSmallBullets(_bigBBullet, SmallBulletBScene, Mathf.Pi / SmallBulletCount); break;
        case 2: break; // 停顿
      }
      _orbitFireStateIndex = (_orbitFireStateIndex + 1) % 3;
    }
  }

  /// <summary>
  /// 手动检查 Boss 是否撞到地图边界．
  /// </summary>
  private bool CheckForWallCollision() {
    var pos = ParentBoss.GlobalPosition;
    return pos.X <= -_mapHalfWidth ||
           pos.X >= _mapHalfWidth ||
           pos.Z <= -_mapHalfHeight ||
           pos.Z >= _mapHalfHeight;
  }

  private void FireSmallBullets(SimpleBullet source, PackedScene scene, float offset) {
    if (!IsInstanceValid(source) || scene == null) return;
    SoundManager.Instance.Play(SoundEffect.FireBig);

    Vector3 startPos = source.GlobalPosition;
    Vector3 toPlayer = (PlayerNode.GlobalPosition - startPos).Normalized();
    float baseAngle = Mathf.Atan2(toPlayer.Z, toPlayer.X) + offset;

    for (int i = 0; i < SmallBulletCount; ++i) {
      var bullet = scene.Instantiate<SimpleBullet>();
      float angle = baseAngle + (Mathf.Tau / SmallBulletCount) * i;
      Vector3 dir = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));

      bullet.UpdateFunc = (t) => {
        SimpleBullet.UpdateState s = new();
        s.position = startPos + dir * (t * 2.5f);
        return s;
      };
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  private void FireWallBulletLine() {
    SoundManager.Instance.Play(SoundEffect.BossDeath); // explosion SE
    Vector3 pos = ParentBoss.GlobalPosition;

    float distToTop = Mathf.Abs(pos.Z - _mapHalfHeight);
    float distToBottom = Mathf.Abs(pos.Z - (-_mapHalfHeight));
    float distToLeft = Mathf.Abs(pos.X - (-_mapHalfWidth));
    float distToRight = Mathf.Abs(pos.X - _mapHalfWidth);

    float min = Mathf.Min(Mathf.Min(distToTop, distToBottom), Mathf.Min(distToLeft, distToRight));

    Vector3 start, end, shootDir;
    if (Mathf.IsEqualApprox(min, distToTop)) {
      start = new Vector3(-_mapHalfWidth, 0, _mapHalfHeight); end = new Vector3(_mapHalfWidth, 0, _mapHalfHeight); shootDir = Vector3.Forward;
    } else if (Mathf.IsEqualApprox(min, distToBottom)) {
      start = new Vector3(-_mapHalfWidth, 0, -_mapHalfHeight); end = new Vector3(_mapHalfWidth, 0, -_mapHalfHeight); shootDir = Vector3.Back;
    } else if (Mathf.IsEqualApprox(min, distToLeft)) {
      start = new Vector3(-_mapHalfWidth, 0, _mapHalfHeight); end = new Vector3(-_mapHalfWidth, 0, -_mapHalfHeight); shootDir = Vector3.Right;
    } else {
      start = new Vector3(_mapHalfWidth, 0, _mapHalfHeight); end = new Vector3(_mapHalfWidth, 0, -_mapHalfHeight); shootDir = Vector3.Left;
    }

    for (int i = 0; i < WallBulletCountPerSide; ++i) {
      var bullet = WallBulletScene.Instantiate<SimpleBullet>();
      Vector3 origin = start.Lerp(end, (float) i / (WallBulletCountPerSide - 1));

      float randAngle = (float) GD.Randfn(0, Mathf.DegToRad(WallBulletAngleSigma));
      Vector3 dir = shootDir.Rotated(Vector3.Up, randAngle);

      float v0 = (float) GD.Randfn(WallBulletInitialSpeedMean, WallBulletInitialSpeedSigma);
      float acc = (float) GD.Randfn(WallBulletAccelerationMean, 0.1f * WallBulletAccelerationMean);
      float tCap = (WallBulletMaxSpeed - v0) / acc;

      bullet.UpdateFunc = (t) => {
        SimpleBullet.UpdateState s = new();
        float speed = Mathf.Min(WallBulletMaxSpeed, v0 + acc * t);
        float dist = v0 * t + 0.5f * acc * t * t;
        if (v0 + acc * t > WallBulletMaxSpeed) {
          dist = v0 * tCap + 0.5f * acc * tCap * tCap + WallBulletMaxSpeed * (t - tCap);
        }
        s.position = origin + dir * dist;
        return s;
      };
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  public override RewindState CaptureInternalState() => new PhaseThreeBodyState {
    CurrentBossState = _currentBossState,
    OrbitFireStateIndex = _orbitFireStateIndex,
    OrbitAngle = _orbitAngle,
    LastFireAngle = _lastFireAngle,
    ChargeTimer = _chargeTimer,
    ChargeDirection = _chargeDirection,
    ChargeVelocity = _chargeVelocity,
    RecoveryTimer = _recoveryTimer,
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseThreeBodyState pts) return;
    _currentBossState = pts.CurrentBossState;
    _orbitFireStateIndex = pts.OrbitFireStateIndex;
    _orbitAngle = pts.OrbitAngle;
    _lastFireAngle = pts.LastFireAngle;
    _chargeTimer = pts.ChargeTimer;
    _chargeDirection = pts.ChargeDirection;
    _chargeVelocity = pts.ChargeVelocity;
    _recoveryTimer = pts.RecoveryTimer;
  }
}
