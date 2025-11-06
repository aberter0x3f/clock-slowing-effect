using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseWave2State : BasePhaseState {
  public PhaseWave2.AttackState CurrentState;
  public float Timer;
  public Vector2 TargetPosition; // The destination for the current move
  public int WaveCounter;
  public float CurrentPerimeterPosition;
  public float TotalMoveDistance;
  public float MoveDirection;
}

public partial class PhaseWave2 : BasePhase {
  public enum AttackState {
    MovingToStartPosition,
    Attacking,
  }

  private AttackState _currentState;
  private float _timer;
  private int _waveCounter;

  // --- 边界移动相关的状态变量 ---
  private Vector2 _startPosition;
  private Vector2 _targetPosition;
  private float _mapHalfWidth;
  private float _mapHalfHeight;
  private float _perimeter;
  private float _currentPerimeterPosition;
  private float _totalMoveDistance;
  private float _moveDirection; // 1 for clockwise, -1 for counter-clockwise

  private MapGenerator _mapGenerator;
  private readonly RandomNumberGenerator _rng = new();

  public override float MaxHealth { get; protected set; } = 25f;

  [ExportGroup("Movement")]
  [Export]
  public float MoveSpeed { get; set; } = 600f;

  [ExportGroup("Attack Pattern")]
  [Export]
  public PackedScene WaveBulletScene { get; set; }
  [Export(PropertyHint.Range, "0.1, 10.0, 0.1")]
  public float WaveInterval { get; set; } = 1.2f;
  [Export(PropertyHint.Range, "1, 100, 1")]
  public float BulletSpacing { get; set; } = 10f;

  [ExportGroup("Bullet Properties")]
  [Export(PropertyHint.Range, "50, 1000, 10")]
  public float BulletForwardSpeed { get; set; } = 300f;
  [Export(PropertyHint.Range, "10, 500, 10")]
  public float BulletMaxHeight { get; set; } = 100f;
  [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
  public float BulletT1 { get; set; } = 1f;
  [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
  public float BulletT2 { get; set; } = 1f;
  [Export(PropertyHint.Range, "0.001, 0.01, 0.001")]
  public float BulletPhaseScale { get; set; } = 0.006f;

  [ExportGroup("Difficulty Scaling")]
  [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
  public float MinBulletT1 { get; set; } = 0.4f;
  [Export(PropertyHint.Range, "0.1, 10.0, 0.1")]
  public float MaxWaveInterval { get; set; } = 2.5f;

  public override void StartPhase(Boss parent) {
    base.StartPhase(parent);
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr("PhaseWave2: MapGenerator not found. Phase cannot start.");
      EndPhase();
      return;
    }

    // 缓存地图尺寸
    _mapHalfWidth = _mapGenerator.MapWidth * _mapGenerator.TileSize / 2.0f;
    _mapHalfHeight = _mapGenerator.MapHeight * _mapGenerator.TileSize / 2.0f;
    _perimeter = 2 * (_mapHalfWidth * 2) + 2 * (_mapHalfHeight * 2);

    // 根据难度调整参数
    float rank = GameManager.Instance.EnemyRank;
    WaveInterval = Mathf.Min(MaxWaveInterval, WaveInterval / (rank * 2 / (rank + 5)));
    BulletT1 = Mathf.Max(MinBulletT1, BulletT1 * 5f / rank);
    BulletForwardSpeed *= rank / 5f;

    // 设置初始状态
    _waveCounter = 0;
    _currentState = AttackState.MovingToStartPosition;
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    switch (_currentState) {
      case AttackState.MovingToStartPosition:
        // 第 0 波：移动到上边界中心
        var startPos = new Vector2(0, -_mapHalfHeight);
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(startPos, MoveSpeed * scaledDelta);
        if (ParentBoss.GlobalPosition.IsEqualApprox(startPos)) {
          // 到达初始位置，发射第一波并切换到主攻击循环
          FireWave();
          PrepareNextWave();
          _timer = WaveInterval;
          _currentState = AttackState.Attacking;
        }
        break;

      case AttackState.Attacking:
        _timer -= scaledDelta;

        // 并行处理移动
        if (_totalMoveDistance > 0) {
          // 速度应确保在 WaveInterval 时间内刚好完成移动
          float speed = _totalMoveDistance / WaveInterval;
          _currentPerimeterPosition += speed * scaledDelta * _moveDirection;
          ParentBoss.GlobalPosition = GetBoundaryPosition(_currentPerimeterPosition);
        }

        if (_timer <= 0) {
          // 时间到，发射弹幕并准备下一次移动
          FireWave();
          PrepareNextWave();
          _timer = WaveInterval;
        }
        break;
    }
  }

  /// <summary>
  /// 准备下一次移动：计算目标点、路径和距离．
  /// </summary>
  private void PrepareNextWave() {
    ++_waveCounter;
    _startPosition = ParentBoss.GlobalPosition;

    // 根据波次的奇偶性决定目标边界
    if (_waveCounter % 2 == 0) {
      // 偶数波 (0, 2, 4, ...): 目标是上或下边界
      float randomX = (float) _rng.RandfRange(-_mapHalfWidth, _mapHalfWidth);
      float y = _rng.Randf() > 0.5f ? _mapHalfHeight : -_mapHalfHeight;
      _targetPosition = new Vector2(randomX, y);
    } else {
      // 奇数波 (1, 3, ...): 目标是左或右边界
      float x = _rng.Randf() > 0.5f ? _mapHalfWidth : -_mapHalfWidth;
      float randomY = (float) _rng.RandfRange(-_mapHalfHeight, _mapHalfHeight);
      _targetPosition = new Vector2(x, randomY);
    }

    // 计算当前位置和目标位置在一维周长上的坐标
    float startPerimeterPos = GetPerimeterPosition(_startPosition);
    float targetPerimeterPos = GetPerimeterPosition(_targetPosition);

    // 计算顺时针和逆时针距离
    float distCW = (targetPerimeterPos - startPerimeterPos + _perimeter) % _perimeter;
    float distCCW = (startPerimeterPos - targetPerimeterPos + _perimeter) % _perimeter;

    // 选择更短的路径
    if (distCW <= distCCW) {
      _moveDirection = 1; // 顺时针
      _totalMoveDistance = distCW;
    } else {
      _moveDirection = -1; // 逆时针
      _totalMoveDistance = distCCW;
    }

    // 如果距离过小，则视为不移动，以避免除零错误
    if (_totalMoveDistance < 0.01f) {
      _totalMoveDistance = 0;
    }

    _currentPerimeterPosition = startPerimeterPos;
  }

  private void FireWave() {
    if (WaveBulletScene == null) {
      GD.PrintErr("PhaseWave2: WaveBulletScene is not set!");
      return;
    }

    Vector2 direction;
    bool isHorizontalWave; // 弹幕墙是水平的（沿 X 轴生成）
    Vector2 pos = ParentBoss.GlobalPosition;

    // 根据 Boss 当前在哪条边上，决定弹幕方向和生成轴
    if (Mathf.IsEqualApprox(pos.Y, -_mapHalfHeight)) { // Top edge
      direction = Vector2.Down;
      isHorizontalWave = true;
    } else if (Mathf.IsEqualApprox(pos.X, _mapHalfWidth)) { // Right edge
      direction = Vector2.Left;
      isHorizontalWave = false;
    } else if (Mathf.IsEqualApprox(pos.Y, _mapHalfHeight)) { // Bottom edge
      direction = Vector2.Up;
      isHorizontalWave = true;
    } else { // Left edge
      direction = Vector2.Right;
      isHorizontalWave = false;
    }

    bool invertWave = _rng.Randf() > 0.5f;

    PlayAttackSound();

    if (isHorizontalWave) {
      for (float x = -_mapHalfWidth; x <= _mapHalfWidth; x += BulletSpacing) {
        SpawnWaveBullet(direction, invertWave, new Vector3(x, pos.Y, 0), (x - pos.X));
      }
    } else { // Vertical wave
      for (float y = -_mapHalfHeight; y <= _mapHalfHeight; y += BulletSpacing) {
        SpawnWaveBullet(direction, invertWave, new Vector3(pos.X, y, 0), (y - pos.Y));
      }
    }
  }

  private void SpawnWaveBullet(Vector2 direction, bool invertWave, Vector3 rawPosition, float phaseOffset) {
    var bullet = WaveBulletScene.Instantiate<PhaseWaveBullet>();
    bullet.ForwardSpeed = BulletForwardSpeed;
    bullet.MaxHeight = BulletMaxHeight;
    bullet.T1 = BulletT1;
    bullet.T2 = BulletT2;
    bullet.Direction = direction;
    bullet.InitialPhase = phaseOffset * BulletPhaseScale;
    bullet.InvertWave = invertWave;
    bullet.RawPosition = rawPosition;
    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  /// <summary>
  /// 将边界上的 2D 坐标转换为一维的周长距离．
  /// </summary>
  private float GetPerimeterPosition(Vector2 pos) {
    // 周长计算起点：左上角 (-halfWidth, -halfHeight)，顺时针方向
    float topEdgeLength = _mapHalfWidth * 2;
    float rightEdgeLength = _mapHalfHeight * 2;
    float bottomEdgeLength = _mapHalfWidth * 2;

    if (Mathf.IsEqualApprox(pos.Y, -_mapHalfHeight) && pos.X >= -_mapHalfWidth) { // Top edge
      return pos.X + _mapHalfWidth;
    }
    if (Mathf.IsEqualApprox(pos.X, _mapHalfWidth) && pos.Y >= -_mapHalfHeight) { // Right edge
      return topEdgeLength + (pos.Y + _mapHalfHeight);
    }
    if (Mathf.IsEqualApprox(pos.Y, _mapHalfHeight) && pos.X <= _mapHalfWidth) { // Bottom edge
      return topEdgeLength + rightEdgeLength + (_mapHalfWidth - pos.X);
    }
    if (Mathf.IsEqualApprox(pos.X, -_mapHalfWidth) && pos.Y <= _mapHalfHeight) { // Left edge
      return topEdgeLength + rightEdgeLength + bottomEdgeLength + (_mapHalfHeight - pos.Y);
    }
    return 0; // Fallback
  }

  /// <summary>
  /// 将一维的周长距离转换回边界上的 2D 坐标．
  /// </summary>
  private Vector2 GetBoundaryPosition(float perimeterPos) {
    perimeterPos = (perimeterPos % _perimeter + _perimeter) % _perimeter; // 确保值在 [0, perimeter) 范围内

    float topEdgeLength = _mapHalfWidth * 2;
    float rightEdgeLength = _mapHalfHeight * 2;
    float bottomEdgeLength = _mapHalfWidth * 2;

    if (perimeterPos <= topEdgeLength) { // Top edge
      return new Vector2(perimeterPos - _mapHalfWidth, -_mapHalfHeight);
    }
    perimeterPos -= topEdgeLength;
    if (perimeterPos <= rightEdgeLength) { // Right edge
      return new Vector2(_mapHalfWidth, perimeterPos - _mapHalfHeight);
    }
    perimeterPos -= rightEdgeLength;
    if (perimeterPos <= bottomEdgeLength) { // Bottom edge
      return new Vector2(_mapHalfWidth - perimeterPos, _mapHalfHeight);
    }
    perimeterPos -= bottomEdgeLength;
    // Left edge
    return new Vector2(-_mapHalfWidth, _mapHalfHeight - perimeterPos);
  }

  public override RewindState CaptureInternalState() {
    return new PhaseWave2State {
      CurrentState = this._currentState,
      Timer = this._timer,
      TargetPosition = this._targetPosition,
      WaveCounter = this._waveCounter,
      CurrentPerimeterPosition = this._currentPerimeterPosition,
      TotalMoveDistance = this._totalMoveDistance,
      MoveDirection = this._moveDirection
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseWave2State pws) return;
    this._currentState = pws.CurrentState;
    this._timer = pws.Timer;
    this._targetPosition = pws.TargetPosition;
    this._waveCounter = pws.WaveCounter;
    this._currentPerimeterPosition = pws.CurrentPerimeterPosition;
    this._totalMoveDistance = pws.TotalMoveDistance;
    this._moveDirection = pws.MoveDirection;
  }
}
