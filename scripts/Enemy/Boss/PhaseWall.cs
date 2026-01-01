using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseWallState : BasePhaseState {
  public PhaseWall.AttackState CurrentState;
  public float XMidTimer;
  public float WallTimer;
  public float ThrowTimer;
  public bool NextWallFromLeft;
  public bool IsSweeping;
  public float SweepTimer;
  public float SmallBulletFireTimer;
  public bool HasFiredLargeBullet;
}

public partial class PhaseWall : BasePhase {
  public enum AttackState {
    MovingToStart,
    Fighting
  }

  private AttackState _currentState = AttackState.MovingToStart;
  private float _xMidTimer;
  private float _wallTimer;
  private float _throwTimer;
  private bool _nextWallFromLeft = true;
  private bool _isSweeping = false;
  private float _sweepTimer = 0f;
  private float _smallBulletFireTimer = 0f;
  private bool _hasFiredLargeBullet = false;

  private MapGenerator _mapGenerator;
  private float _mapHalfWidth;
  private float _mapHalfHeight;

  [ExportGroup("Movement")]
  [Export] public Vector3 StartPosition { get; set; } = new(0, 0, -3.0f);
  [Export] public float MoveToStartSpeed { get; set; } = 5.0f;
  [Export] public float XMidFrequency { get; set; } = 0.15f;
  [Export] public float XMidAmplitude { get; set; } = 2f;

  [ExportGroup("Wall Pattern")]
  [Export] public PackedScene WallBulletScene { get; set; }
  [Export] public float WallInterval { get; set; } = 0.3f;
  [Export] public float WallBulletSpacing { get; set; } = 0.4f;
  [Export] public float WallGapHalfWidth { get; set; } = 2f;
  [Export] public float WallTravelDuration { get; set; } = 1f;

  [ExportGroup("Throw Pattern")]
  [Export] public PackedScene LargeThrowBulletScene { get; set; }
  [Export] public PackedScene SmallThrowBulletScene { get; set; }
  [Export] public float ThrowInterval { get; set; } = 1f;
  [Export] public float SweepDuration { get; set; } = 2f;
  [Export] public float SmallBulletInterval { get; set; } = 0.08f;
  [Export] public int SmallBulletCount { get; set; } = 4;
  [Export] public float SmallBulletSpeedMin { get; set; } = 2.0f;
  [Export] public float SmallBulletSpeedMax { get; set; } = 3.0f;
  [Export] public float ThrowGravity { get; set; } = 6f;
  [Export] public float ThrowVerticalSpeed { get; set; } = 3.0f;

  [ExportGroup("Falling Line Pattern")]
  [Export] public PackedScene FallingLineBulletScene { get; set; }
  [Export] public float LineBulletSpacing { get; set; } = 0.3f;
  [Export] public float LineMinHeight { get; set; } = 1.5f;
  [Export] public float LineMaxHeight { get; set; } = 5.0f;
  [Export] public float LineGravity { get; set; } = 6f;

  [ExportGroup("Final Spread Pattern")]
  [Export] public PackedScene FinalSpreadBulletScene { get; set; }
  [Export] public float SpreadAcceleration { get; set; } = 2.0f;
  [Export] public float SpreadMaxSpeed { get; set; } = 6.0f;
  [Export] public float SpreadAngleSigmaDeg { get; set; } = 30.0f;

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    _mapHalfWidth = (_mapGenerator.MapWidth / 2f - 1) * _mapGenerator.TileSize;
    _mapHalfHeight = (_mapGenerator.MapHeight / 2f - 1) * _mapGenerator.TileSize;

    var rank = GameManager.Instance.EnemyRank;

    SmallBulletInterval /= (rank + 3) / 8f;
    WallGapHalfWidth /= (rank + 3) / 8f;
    SmallBulletSpeedMin *= (rank + 5) / 10f;
    SmallBulletSpeedMax *= (rank + 5) / 10f;
    TimeScaleSensitivity = 10f / (rank + 10);

    _currentState = AttackState.MovingToStart;
    _throwTimer = 1.0f;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    switch (_currentState) {
      case AttackState.MovingToStart:
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(StartPosition, MoveToStartSpeed * scaledDelta);
        if (ParentBoss.GlobalPosition.IsEqualApprox(StartPosition)) {
          _currentState = AttackState.Fighting;
        }
        break;

      case AttackState.Fighting:
        _xMidTimer += scaledDelta;
        float xMid = Mathf.Sin(_xMidTimer * XMidFrequency * Mathf.Tau) * XMidAmplitude;

        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition with { X = xMid };

        ProcessWallAttack(scaledDelta, xMid);
        ProcessThrowAttack(scaledDelta, xMid);
        break;
    }
  }

  private void ProcessWallAttack(float scaledDelta, float xMid) {
    _wallTimer -= scaledDelta;
    if (_wallTimer <= 0) {
      SpawnWall(xMid);
      _wallTimer = WallInterval;
      _nextWallFromLeft = !_nextWallFromLeft;
    }
  }

  private void SpawnWall(float xMid) {
    float spawnX = _nextWallFromLeft ? -_mapHalfWidth : _mapHalfWidth;
    float targetReachX = _nextWallFromLeft ? (xMid - WallGapHalfWidth) : (xMid + WallGapHalfWidth);
    float range = targetReachX - spawnX;

    for (float z = -_mapHalfHeight; z <= _mapHalfHeight; z += WallBulletSpacing) {
      for (float y = 0; y <= 1f; y += WallBulletSpacing) {
        var bullet = WallBulletScene.Instantiate<SimpleBullet>();
        Vector3 basePos = new Vector3(spawnX, y, z);
        float duration = WallTravelDuration;

        bullet.TimeScaleSensitivity = TimeScaleSensitivity;
        bullet.UpdateFunc = (t) => {
          var s = new SimpleBullet.UpdateState();
          if (t >= duration) {
            s.destroy = true;
            return s;
          }
          float progress = t / duration;
          float xOffset = range * Mathf.Sin(progress * Mathf.Pi);
          s.position = basePos + new Vector3(xOffset, 0, 0);
          return s;
        };

        GameRootProvider.CurrentGameRoot.AddChild(bullet);
      }
    }
  }

  private void ProcessThrowAttack(float scaledDelta, float bossX) {
    if (!_isSweeping) {
      _throwTimer -= scaledDelta;
      if (_throwTimer <= 0) {
        _isSweeping = true;
        _sweepTimer = 0f;
        _smallBulletFireTimer = 0f;
        _hasFiredLargeBullet = false;
        SoundManager.Instance.Play(SoundEffect.FireSmall);
      }
      return;
    }

    // 正在进行扫掠
    _sweepTimer += scaledDelta;

    // 使用余弦函数实现从左到右再到左的完整周期扫掠
    // t=0, cos=1, x = -HalfWidth
    // t=Duration/2, cos=-1, x = +HalfWidth
    // t=Duration, cos=1, x = -HalfWidth
    float progress = _sweepTimer / SweepDuration;
    float currentSweepX = -Mathf.Cos(progress * Mathf.Tau) * _mapHalfWidth;
    Vector3 spawnPos = new Vector3(currentSweepX, 0, ParentBoss.GlobalPosition.Z);

    // 检查是否到达 Boss 位置发射大子弹（仅在 L->R 阶段发射一次）
    if (!_hasFiredLargeBullet && progress < 0.5f) {
      // 检查扫掠位置是否已经越过或到达 Boss 位置
      if (currentSweepX >= bossX) {
        var dir = ((PlayerNode.GlobalPosition - ParentBoss.GlobalPosition) with { Y = 0 }).Normalized();
        SpawnThrownBullet(spawnPos, dir, true);
        _hasFiredLargeBullet = true;
      }
    }

    // 发射小子弹逻辑
    _smallBulletFireTimer -= scaledDelta;
    if (_smallBulletFireTimer <= 0) {
      // 随机方向和随机速度
      for (int i = 0; i < SmallBulletCount; ++i) {
        float randAngle = (float) GD.RandRange(0, Mathf.Tau);
        Vector3 randDir = new Vector3(Mathf.Cos(randAngle), 0, Mathf.Sin(randAngle));
        SpawnThrownBullet(spawnPos, randDir, false);
      }

      _smallBulletFireTimer = SmallBulletInterval;
    }

    if (progress >= 1.0f) {
      _isSweeping = false;
      _throwTimer = ThrowInterval;
    }
  }

  private void SpawnThrownBullet(Vector3 origin, Vector3 direction, bool isLarge) {
    var bullet = (isLarge ? LargeThrowBulletScene : SmallThrowBulletScene).Instantiate<SimpleBullet>();

    float vY = isLarge ? 0 : ThrowVerticalSpeed;
    // 小子弹随机速度，大子弹固定使用最大速度
    float vH = isLarge ? SmallBulletSpeedMax : (float) GD.RandRange(SmallBulletSpeedMin, SmallBulletSpeedMax);
    float g = ThrowGravity;

    // 计算落地时间
    float landTime = (2.0f * vY) / g;

    bullet.TimeScaleSensitivity = TimeScaleSensitivity;
    bullet.UpdateFunc = (t) => {
      var s = new SimpleBullet.UpdateState();
      Vector3 currentPos;

      if (t < landTime) {
        float height = vY * t - 0.5f * g * t * t;
        currentPos = origin + direction * (vH * t) + Vector3.Up * height;
      } else {
        float groundTime = t - landTime;
        currentPos = origin + direction * (vH * t);
        currentPos.Y = 0;

        if (isLarge) {
          if (currentPos.X >= _mapHalfWidth || currentPos.X <= -_mapHalfWidth ||
              currentPos.Z >= _mapHalfHeight || currentPos.Z <= -_mapHalfHeight) {
            s.destroy = true;
            currentPos.X = Mathf.Clamp(currentPos.X, -_mapHalfWidth, _mapHalfWidth);
            currentPos.Z = Mathf.Clamp(currentPos.Z, -_mapHalfHeight, _mapHalfHeight);
            CallDeferred(nameof(TriggerFallingLine), currentPos);
            return s;
          }
        }
      }

      s.position = currentPos;
      return s;
    };

    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  private void TriggerFallingLine(Vector3 impactPos) {
    SoundManager.Instance.Play(SoundEffect.BossDeath);

    bool hitZBoundary = Mathf.Abs(impactPos.Z) >= Mathf.Abs(impactPos.X);
    Vector3 lineStart, lineEnd, spreadDir;

    if (hitZBoundary) {
      lineStart = new Vector3(impactPos.X, 0, -_mapHalfHeight);
      lineEnd = new Vector3(impactPos.X, 0, _mapHalfHeight);
      spreadDir = Vector3.Right;
      if (impactPos.Z > 0) {
        (lineStart, lineEnd) = (lineEnd, lineStart);
      }
    } else {
      lineStart = new Vector3(-_mapHalfWidth, 0, impactPos.Z);
      lineEnd = new Vector3(_mapHalfWidth, 0, impactPos.Z);
      spreadDir = Vector3.Back;
      if (impactPos.X > 0) {
        (lineStart, lineEnd) = (lineEnd, lineStart);
      }
    }

    float lineLength = lineStart.DistanceTo(lineEnd);
    int bulletCount = Mathf.FloorToInt(lineLength / LineBulletSpacing);

    for (int i = 0; i <= bulletCount; ++i) {
      float t = (float) i / bulletCount;
      Vector3 groundPos = lineStart.Lerp(lineEnd, t);
      float startHeight = Mathf.Lerp(LineMinHeight, LineMaxHeight, t);
      SpawnFallingBullet(groundPos, startHeight, spreadDir);
    }
  }

  private void SpawnFallingBullet(Vector3 groundTarget, float height, Vector3 spreadDir) {
    var bullet = FallingLineBulletScene.Instantiate<SimpleBullet>();
    float g = LineGravity;
    float fallDuration = Mathf.Sqrt((2.0f * height) / g);

    if (GD.Randi() % 2 == 1) {
      spreadDir *= -1;
    }

    bullet.TimeScaleSensitivity = TimeScaleSensitivity;
    bullet.UpdateFunc = (t) => {
      var s = new SimpleBullet.UpdateState();
      if (t >= fallDuration) {
        s.destroy = true;
        CallDeferred(nameof(SpawnFinalSpreadBullet), groundTarget, spreadDir);
        return s;
      }

      float currentY = height - 0.5f * g * t * t;
      s.position = groundTarget + Vector3.Up * currentY;
      return s;
    };

    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  private void SpawnFinalSpreadBullet(Vector3 origin, Vector3 baseDir) {
    var bullet = FinalSpreadBulletScene.Instantiate<SimpleBullet>();

    float randomAngle = Mathf.DegToRad((float) GD.Randfn(0, SpreadAngleSigmaDeg));
    Vector3 finalDir = baseDir.Rotated(Vector3.Up, randomAngle);

    float acc = SpreadAcceleration;
    float vMax = SpreadMaxSpeed;
    float tCap = vMax / acc;
    float dCap = 0.5f * acc * tCap * tCap;

    bullet.TimeScaleSensitivity = TimeScaleSensitivity;
    bullet.UpdateFunc = (t) => {
      var s = new SimpleBullet.UpdateState();
      float distance;
      if (t < tCap) {
        distance = 0.5f * acc * t * t;
      } else {
        distance = dCap + vMax * (t - tCap);
      }

      s.position = origin + finalDir * distance;
      if (Mathf.Abs(s.position.X) > _mapHalfWidth + 2.0f || Mathf.Abs(s.position.Z) > _mapHalfHeight + 2.0f) {
        s.destroy = true;
      }
      return s;
    };

    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  public override RewindState CaptureInternalState() => new PhaseWallState {
    CurrentState = _currentState,
    XMidTimer = _xMidTimer,
    WallTimer = _wallTimer,
    ThrowTimer = _throwTimer,
    NextWallFromLeft = _nextWallFromLeft,
    IsSweeping = _isSweeping,
    SweepTimer = _sweepTimer,
    SmallBulletFireTimer = _smallBulletFireTimer,
    HasFiredLargeBullet = _hasFiredLargeBullet
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseWallState s) return;
    _currentState = s.CurrentState;
    _xMidTimer = s.XMidTimer;
    _wallTimer = s.WallTimer;
    _throwTimer = s.ThrowTimer;
    _nextWallFromLeft = s.NextWallFromLeft;
    _isSweeping = s.IsSweeping;
    _sweepTimer = s.SweepTimer;
    _smallBulletFireTimer = s.SmallBulletFireTimer;
    _hasFiredLargeBullet = s.HasFiredLargeBullet;
  }
}
