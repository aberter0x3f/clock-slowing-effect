using System.Collections.Generic;
using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseRainState : BasePhaseState {
  public PhaseRain.PhaseState CurrentState;
  public Vector3 StartPos;
  public Vector3 TargetPos;
  public bool MovingRight;
  public float SweepTime;
  public float TotalSweepDuration;
  public float SniperTimer;
  public ulong SweepSeed;
  public int NextShotIndex; // 只需要记录当前执行到了第几个事件，而不需要保存整个列表
}

public partial class PhaseRain : BasePhase {
  public enum PhaseState {
    SetupCenter,   // 飞向中心
    SetupSide,     // 飞向起始侧
    FightingSweep, // 扫荡攻击中
  }

  public override float MaxHealth { get; protected set; } = 24f;

  [ExportGroup("Phase Settings")]
  [Export] public float FlightHeight { get; set; } = 1.0f;
  [Export] public float BaseMoveSpeed { get; set; } = 10.0f;

  [ExportGroup("Rain Attack")]
  [Export] public PackedScene RainBulletScene { get; set; }
  [Export] public int RainBulletCount { get; set; } = 15; // 每次扫荡的爆发次数
  [Export] public int RainBulletChainCount { get; set; } = 6; // 每次爆发的子弹数量
  [Export] public float RainBulletChainDistance { get; set; } = 0.1f; // 链条中子弹的间距
  [Export] public float RainGravity { get; set; } = 6.0f;
  [Export] public float RainHorizontalSpeedMean { get; set; } = 4f;
  [Export] public float RainHorizontalSpeedSigma { get; set; } = 0.5f;
  [Export] public float RainAngleOffsetDeg { get; set; } = 15f;
  [Export] public float RainAngleSigma { get; set; } = 5f;

  [ExportGroup("Sniper Attack")]
  [Export] public PackedScene SniperBulletScene { get; set; }
  [Export] public float SniperInterval { get; set; } = 0.05f;
  [Export] public float SniperSpeed { get; set; } = 3f;
  [Export] public float SniperAcceleration { get; set; } = 3.0f;
  [Export] public float SniperMinHeight { get; set; } = -0.2f;

  // 运行时状态
  private PhaseState _currentState;
  private Vector3 _startPos;
  private Vector3 _targetPos;
  private bool _movingRight = true;

  // 扫荡状态
  private float _sweepTime;
  private float _totalSweepDuration;
  private float _sniperTimer;
  private ulong _sweepSeed;
  private int _nextShotIndex;

  private MapGenerator _mapGenerator;
  private float _moveHalfWidth;
  private float _moveHalfHeight;
  private readonly RandomNumberGenerator _rng = new();

  // 简化的结构体，描述一次「爆发事件」
  private struct ShotEvent {
    public float FireTime;     // 触发时间 (0 到 TotalDuration)
    public float PathProgress; // Boss 路径上的进度 (0 到 1)
    public Vector2 TargetPos2D;
    public Vector2 Direction2D;
  }

  private readonly List<ShotEvent> _schedule = new();

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _mapGenerator = GetTree().Root.GetNode<MapGenerator>("GameRoot/MapGenerator");

    // 计算 Boss 移动和瞄准的边界
    _moveHalfWidth = (_mapGenerator.MapWidth / 2f) * _mapGenerator.TileSize * 1.2f;
    _moveHalfHeight = (_mapGenerator.MapHeight / 2f) * _mapGenerator.TileSize;

    _currentState = PhaseState.SetupCenter;
    _targetPos = new Vector3(0, FlightHeight, 0);

    var rank = GameManager.Instance.EnemyRank;
    TimeScaleSensitivity = 5f / (rank + 5);
    SniperInterval /= (rank + 10) / 15f;
    RainBulletCount = Mathf.RoundToInt(RainBulletCount * (rank + 10) / 15f);
    RainHorizontalSpeedMean *= (rank + 15) / 20f;
    SniperSpeed *= (rank + 10) / 15f;
    SniperAcceleration *= (rank + 10) / 15f;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    switch (_currentState) {
      case PhaseState.SetupCenter:
        MoveBossToTarget(scaledDelta, 8.0f);
        if (ParentBoss.GlobalPosition.IsEqualApprox(_targetPos)) {
          _currentState = PhaseState.SetupSide;
          _targetPos = GetRandomSidePosition(true); // 初始飞向左侧
        }
        break;

      case PhaseState.SetupSide:
        MoveBossToTarget(scaledDelta, 8.0f);
        if (ParentBoss.GlobalPosition.IsEqualApprox(_targetPos)) {
          _movingRight = true;
          StartNewSweep();
        }
        break;

      case PhaseState.FightingSweep:
        _sweepTime += scaledDelta;

        // Boss 移动逻辑：使用 Lerp 确保位置与时间绝对对应
        if (_sweepTime >= _totalSweepDuration) {
          ParentBoss.GlobalPosition = _targetPos;
          _movingRight = !_movingRight; // 下一次反向
          StartNewSweep();
        } else {
          float progress = _sweepTime / _totalSweepDuration;
          ParentBoss.GlobalPosition = _startPos.Lerp(_targetPos, progress);
        }

        // 处理雨弹射击事件
        while (_nextShotIndex < _schedule.Count && _schedule[_nextShotIndex].FireTime <= _sweepTime) {
          var shot = _schedule[_nextShotIndex];
          // 计算精确的时间偏差，用于物理补偿
          float timeOffset = _sweepTime - shot.FireTime;
          FireRainChain(shot, timeOffset);
          ++_nextShotIndex;
        }

        // 处理自机狙
        _sniperTimer -= scaledDelta;
        if (_sniperTimer <= 0) {
          FireSniper();
          _sniperTimer = SniperInterval;
        }
        break;
    }
  }

  private void MoveBossToTarget(float dt, float speed) {
    ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(_targetPos, speed * dt);
  }

  private Vector3 GetRandomSidePosition(bool leftSide) {
    float x = leftSide ? -_moveHalfWidth : _moveHalfWidth;
    float z = (float) GD.RandRange(-_moveHalfHeight * 1.2f, -_moveHalfHeight * 0.8f);
    return new Vector3(x, FlightHeight, z);
  }

  private void StartNewSweep() {
    _currentState = PhaseState.FightingSweep;
    _startPos = ParentBoss.GlobalPosition;
    _targetPos = GetRandomSidePosition(!_movingRight); // 目标是另一侧

    // 根据血量计算速度
    float hpRatio = Mathf.Clamp((MaxHealth - Health) / (MaxHealth * 0.5f), 0f, 1f);
    float speed = BaseMoveSpeed * Mathf.Lerp(1.0f, 3.0f, hpRatio);
    float dist = _startPos.DistanceTo(_targetPos);

    _totalSweepDuration = dist / speed;
    _sweepTime = 0f;
    _sniperTimer = SniperInterval;
    _nextShotIndex = 0;

    SoundManager.Instance.Play(SoundEffect.FireBig);

    // 生成确定性的调度表
    _sweepSeed = ((ulong) GD.Randi() << 32) | (ulong) GD.Randi();
    GenerateSchedule(_sweepSeed);
  }

  /// <summary>
  /// 预计算所有雨弹的落点和触发时间，以确保它们落在有效地图区域内．
  /// </summary>
  private void GenerateSchedule(ulong seed) {
    _schedule.Clear();
    _rng.Seed = seed;

    Vector2 bossStart2D = new Vector2(_startPos.X, _startPos.Z);
    Vector2 bossEnd2D = new Vector2(_targetPos.X, _targetPos.Z);
    Vector2 pathVec = bossEnd2D - bossStart2D;

    int attempts = 0;
    int successCount = 0;

    // 尝试找到 RainBulletCount 个有效的相交解
    while (successCount < RainBulletCount && attempts < RainBulletCount * 5) {
      ++attempts;

      // 在地图地面上随机选一个落点 P
      float px = _rng.RandfRange(-_moveHalfWidth, _moveHalfWidth);
      float pz = _rng.RandfRange(-_moveHalfHeight, 0); // 假设 Boss 在 Z 轴负方向的顶部
      Vector2 P = new Vector2(px, pz);

      // 随机选择雨的倾斜角度（向后发射）
      // 基础角度依赖于移动方向，以制造「扫过」的感觉
      float offset = Mathf.DegToRad(RainAngleOffsetDeg);
      float directionBias = _movingRight ? -offset : offset;
      float randomVar = Mathf.DegToRad(_rng.Randfn(0, RainAngleSigma));

      // 角度逻辑：-Pi/2 是垂直向下（相对于屏幕），即 3D 中的 Z+
      float finalAngle = -Mathf.Pi / 2 + directionBias + randomVar;
      Vector2 rainDir = new Vector2(Mathf.Cos(finalAngle), Mathf.Sin(finalAngle));

      // 计算射线 P + (-rainDir) 与线段 BossPath 的交点
      // 这回答了：「Boss 必须在什么位置发射，子弹才能沿 rainDir 落在 P 点？」
      if (TryGetIntersectionT(bossStart2D, pathVec, P, rainDir, out float t)) {
        // t 是 Boss 路径上的进度 (0..1)
        float fireTime = t * _totalSweepDuration;

        _schedule.Add(new ShotEvent {
          FireTime = fireTime,
          PathProgress = t,
          TargetPos2D = P,
          Direction2D = rainDir
        });
        ++successCount;
      }
    }

    // 按时间排序，以便顺序处理
    _schedule.Sort((a, b) => a.FireTime.CompareTo(b.FireTime));
  }

  /// <summary>
  /// 如果从 P 点发出的反向雨射线与线段 AB 相交，则返回 t (0..1)．
  /// </summary>
  private bool TryGetIntersectionT(Vector2 A, Vector2 AB, Vector2 P, Vector2 rainDir, out float t) {
    t = -1;
    // 我们要求解：A + t*AB = P - k*rainDir  (其中 k > 0 是距离)
    // 移项得：t*AB + k*rainDir = P - A
    // 使用行列式求解
    float det = AB.X * rainDir.Y - AB.Y * rainDir.X;

    if (Mathf.IsZeroApprox(det)) return false; // 平行

    Vector2 delta = P - A;
    t = (delta.X * rainDir.Y - delta.Y * rainDir.X) / det;
    // k 代表从 P 点反向推回路径的距离，虽然物理上不需要 k，但它必须为正值才表示从上方射下
    // 在方程 P - A = t*AB + k*rainDir 中求解 k
    // float k = (delta.X * AB.Y - delta.Y * AB.X) / det;

    return t >= 0f && t <= 1f;
  }

  private void FireRainChain(ShotEvent shot, float timeOffset) {
    // 重建精确的物理参数
    Vector3 bossPosAtFireTime = _startPos.Lerp(_targetPos, shot.PathProgress);
    Vector3 targetPos = new Vector3(shot.TargetPos2D.X, 0, shot.TargetPos2D.Y);
    Vector3 rainDir3D = new Vector3(shot.Direction2D.X, 0, shot.Direction2D.Y).Normalized();

    // 物理计算
    float g = RainGravity;
    float H = bossPosAtFireTime.Y;

    Vector3 distVec = targetPos - bossPosAtFireTime;
    Vector3 distVecH = new Vector3(distVec.X, 0, distVec.Z);
    float distH = distVecH.Length();

    // 可以在每个链条中稍微随机化速度以增加变化
    float v_h = Mathf.Max(0.1f, (float) _rng.Randfn(RainHorizontalSpeedMean, RainHorizontalSpeedSigma));
    float flightTime = distH / v_h;

    // 计算为了在 flightTime 时刻正好落地所需的垂直初速度
    // y = H + vy*t - 0.5*g*t^2 => 0 = H + vy*T - 0.5gT^2
    float v_y0 = (0.5f * g * flightTime * flightTime - H) / flightTime;

    Vector3 velocityH = distVecH.Normalized() * v_h;
    Vector3 initialVelocity = velocityH;
    initialVelocity.Y = v_y0;

    // 发射链条
    // 我们通过偏移初始位置来创建「条纹」效果
    // 垂直于运动方向扩散会形成宽片，沿运动方向扩散会形成长条
    // 这里使用沿雨的方向偏移，模拟连续下落

    for (int i = 0; i < RainBulletChainCount; ++i) {
      var bullet = RainBulletScene.Instantiate<SimpleBullet>();

      // 计算空间偏移
      Vector3 chainOffset = rainDir3D * (i * RainBulletChainDistance);
      Vector3 bulletStartPos = bossPosAtFireTime + chainOffset;
      // 目标位置也需要相应偏移，用于滑行逻辑
      Vector3 bulletTargetPos = targetPos + chainOffset;

      bullet.TimeScaleSensitivity = TimeScaleSensitivity;
      bullet.UpdateFunc = (rawTimeAlive) => {
        // 补偿帧抖动
        float t = rawTimeAlive + timeOffset;
        SimpleBullet.UpdateState s = new();

        if (t <= flightTime) {
          // 弹道抛物线
          Vector3 displacement = initialVelocity * t;
          displacement.Y -= 0.5f * g * t * t;
          s.position = bulletStartPos + displacement;
          s.position.Y = Mathf.Max(0, s.position.Y);
        } else {
          // 地面滑行
          float extraTime = t - flightTime;
          s.position = bulletTargetPos + velocityH * extraTime;
          s.position.Y = 0;
        }
        return s;
      };

      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  private void FireSniper() {
    var bullet = SniperBulletScene.Instantiate<SimpleBullet>();
    Vector3 startPos = ParentBoss.GlobalPosition;
    Vector3 playerPos = PlayerNode.GlobalPosition;
    Vector3 dir = (playerPos - startPos).Normalized();

    float v = SniperSpeed;
    float a = SniperAcceleration;
    float minH = SniperMinHeight;

    bullet.TimeScaleSensitivity = TimeScaleSensitivity;
    bullet.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();
      float dist = v * t + 0.5f * a * t * t;
      s.position = startPos + dir * dist;
      if (s.position.Y < minH) s.destroy = true;
      return s;
    };
    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  public override RewindState CaptureInternalState() {
    return new PhaseRainState {
      CurrentState = _currentState,
      StartPos = _startPos,
      TargetPos = _targetPos,
      MovingRight = _movingRight,
      SweepTime = _sweepTime,
      TotalSweepDuration = _totalSweepDuration,
      SniperTimer = _sniperTimer,
      SweepSeed = _sweepSeed,
      NextShotIndex = _nextShotIndex
    };
  }

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseRainState s) return;
    _currentState = s.CurrentState;
    _startPos = s.StartPos;
    _targetPos = s.TargetPos;
    _movingRight = s.MovingRight;
    _sweepTime = s.SweepTime;
    _totalSweepDuration = s.TotalSweepDuration;
    _sniperTimer = s.SniperTimer;
    _nextShotIndex = s.NextShotIndex;

    // 关键：如果正在扫荡状态，且种子不匹配或列表为空，必须重新生成调度表
    // 这样才能保证 _nextShotIndex 指向正确的数据
    if (s.SweepSeed != _sweepSeed || _schedule.Count == 0) {
      _sweepSeed = s.SweepSeed;
      if (_currentState == PhaseState.FightingSweep) {
        GenerateSchedule(_sweepSeed);
      }
    }
  }
}
