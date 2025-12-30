using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseWave2State : BasePhaseState {
  public PhaseWave2.AttackState CurrentState;
  public float Timer;
  public Vector3 StartPosition;
  public Vector3 TargetPosition;
  public int WaveCounter;
  public float CurrentPerimeterPosition;
  public float TotalMoveDistance;
  public float MoveDirection;
}

public partial class PhaseWave2 : BasePhase {
  public enum AttackState { MovingToStartPosition, Attacking }

  private AttackState _currentState;
  private float _timer;
  private int _waveCounter;

  private Vector3 _startPosition;
  private Vector3 _targetPosition;

  private float _mapHalfWidth;
  private float _mapHalfHeight;
  private float _perimeter;
  private float _currentPerimeterPosition;
  private float _totalMoveDistance;
  private float _moveDirection;

  private MapGenerator _mapGenerator;

  public override float MaxHealth { get; protected set; } = 35f;

  [ExportGroup("Movement")]
  [Export] public float MoveSpeed { get; set; } = 8.0f;

  [ExportGroup("Attack Pattern")]
  [Export] public PackedScene BulletScene { get; set; }
  [Export] public float WaveInterval { get; set; } = 1.2f;
  [Export] public float BulletSpacing { get; set; } = 0.15f;

  [ExportGroup("Bullet Properties")]
  [Export] public float BulletForwardSpeed { get; set; } = 3.5f;
  [Export] public float BulletMaxHeight { get; set; } = 1.0f;
  [Export] public float BulletT1 { get; set; } = 0.8f;
  [Export] public float BulletT2 { get; set; } = 1.0f;
  [Export] public float BulletPhaseScale { get; set; } = 0.5f;

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    _mapHalfWidth = (_mapGenerator.MapWidth / 2f - 1) * _mapGenerator.TileSize;
    _mapHalfHeight = (_mapGenerator.MapHeight / 2f - 1) * _mapGenerator.TileSize;
    _perimeter = 2 * (_mapHalfWidth * 2) + 2 * (_mapHalfHeight * 2);

    float rank = GameManager.Instance.EnemyRank;
    WaveInterval = Mathf.Min(2.5f, WaveInterval / (rank * 2 / (rank + 5)));
    BulletT1 = Mathf.Max(0.4f, BulletT1 * 5f / rank);
    BulletForwardSpeed *= rank / 5f;

    _currentState = AttackState.MovingToStartPosition;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    switch (_currentState) {
      case AttackState.MovingToStartPosition:
        var startPos = new Vector3(0, 0, -_mapHalfHeight);
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(startPos, MoveSpeed * scaledDelta);
        if (ParentBoss.GlobalPosition.IsEqualApprox(startPos)) {
          FireWave();
          PrepareNextWave();
          _timer = WaveInterval;
          _currentState = AttackState.Attacking;
        }
        break;

      case AttackState.Attacking:
        _timer -= scaledDelta;

        if (_totalMoveDistance > 0) {
          // 根据剩余时间同步移动
          float speed = _totalMoveDistance / WaveInterval;
          _currentPerimeterPosition += speed * scaledDelta * _moveDirection;
          ParentBoss.GlobalPosition = GetBoundaryPosition(_currentPerimeterPosition);
        }

        if (_timer <= 0) {
          FireWave();
          PrepareNextWave();
          _timer = WaveInterval;
        }
        break;
    }
  }

  private void PrepareNextWave() {
    ++_waveCounter;
    _startPosition = ParentBoss.GlobalPosition;

    // 随机选择下一条边上的目标
    if (_waveCounter % 2 == 0) {
      // 偶数波：目标在 Top/Bottom (Z轴)
      _targetPosition = new Vector3((float) GD.RandRange(-_mapHalfWidth, _mapHalfWidth), 0, GD.Randf() > 0.5f ? _mapHalfHeight : -_mapHalfHeight);
    } else {
      // 奇数波：目标在 Left/Right (X轴)
      _targetPosition = new Vector3(GD.Randf() > 0.5f ? _mapHalfWidth : -_mapHalfWidth, 0, (float) GD.RandRange(-_mapHalfHeight, _mapHalfHeight));
    }

    float startP = GetPerimeterPosition(new Vector2(_startPosition.X, _startPosition.Z));
    float targetP = GetPerimeterPosition(new Vector2(_targetPosition.X, _targetPosition.Z));

    float distCW = (targetP - startP + _perimeter) % _perimeter;
    float distCCW = (startP - targetP + _perimeter) % _perimeter;

    // 选择最短路径移动
    if (distCW <= distCCW) { _moveDirection = 1; _totalMoveDistance = distCW; } else { _moveDirection = -1; _totalMoveDistance = distCCW; }

    _currentPerimeterPosition = startP;
  }

  private void FireWave() {
    SoundManager.Instance.Play(SoundEffect.FireBig);

    Vector3 pos = ParentBoss.GlobalPosition;
    Vector3 forward;
    bool isHorizontal;

    if (Mathf.IsEqualApprox(pos.Z, _mapHalfHeight)) { forward = Vector3.Forward; isHorizontal = true; } // Back -> Forward
    else if (Mathf.IsEqualApprox(pos.X, _mapHalfWidth)) { forward = Vector3.Left; isHorizontal = false; } // Right -> Left
    else if (Mathf.IsEqualApprox(pos.Z, -_mapHalfHeight)) { forward = Vector3.Back; isHorizontal = true; } // Forward -> Back
    else { forward = Vector3.Right; isHorizontal = false; } // Left -> Right

    bool invert = GD.Randf() > 0.5f;

    if (isHorizontal) {
      for (float x = -_mapHalfWidth; x <= _mapHalfWidth; x += BulletSpacing) {
        SpawnBullet(forward, invert, new Vector3(x, 0, pos.Z), x - pos.X);
      }
    } else {
      for (float z = -_mapHalfHeight; z <= _mapHalfHeight; z += BulletSpacing) {
        SpawnBullet(forward, invert, new Vector3(pos.X, 0, z), z - pos.Z);
      }
    }
  }

  private void SpawnBullet(Vector3 forward, bool invert, Vector3 startPos, float phaseOff) {
    var bullet = BulletScene.Instantiate<SimpleBullet>();
    float initialPhase = phaseOff * BulletPhaseScale;
    bullet.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();
      float period = BulletT1 + BulletT2;
      float time = Mathf.PosMod(phaseOff + (invert ? -t : t), period);

      float h = 0;
      if (time <= BulletT1)
        h = BulletMaxHeight * Mathf.Sin(time / BulletT1 * Mathf.Pi);

      s.position = startPos + forward * (BulletForwardSpeed * t) + Vector3.Up * h;
      return s;
    };
    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  private float GetPerimeterPosition(Vector2 pos) {
    float w = _mapHalfWidth * 2;
    float h = _mapHalfHeight * 2;
    // 顺时针周长映射
    if (Mathf.IsEqualApprox(pos.Y, _mapHalfHeight)) return pos.X + _mapHalfWidth;
    if (Mathf.IsEqualApprox(pos.X, _mapHalfWidth)) return w + (_mapHalfHeight - pos.Y);
    if (Mathf.IsEqualApprox(pos.Y, -_mapHalfHeight)) return w + h + (_mapHalfWidth - pos.X);
    return w * 2 + h + (pos.Y + _mapHalfHeight);
  }

  private Vector3 GetBoundaryPosition(float p) {
    p = (p % _perimeter + _perimeter) % _perimeter;
    float w = _mapHalfWidth * 2;
    float h = _mapHalfHeight * 2;
    if (p <= w) return new Vector3(p - _mapHalfWidth, 0, _mapHalfHeight);
    p -= w;
    if (p <= h) return new Vector3(_mapHalfWidth, 0, _mapHalfHeight - p);
    p -= h;
    if (p <= w) return new Vector3(_mapHalfWidth - p, 0, -_mapHalfHeight);
    return new Vector3(-_mapHalfWidth, 0, p - _mapHalfHeight);
  }

  public override RewindState CaptureInternalState() => new PhaseWave2State {
    CurrentState = _currentState,
    Timer = _timer,
    WaveCounter = _waveCounter,
    StartPosition = _startPosition,
    TargetPosition = _targetPosition,
    CurrentPerimeterPosition = _currentPerimeterPosition,
    TotalMoveDistance = _totalMoveDistance,
    MoveDirection = _moveDirection
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseWave2State pws) return;
    _currentState = pws.CurrentState;
    _timer = pws.Timer;
    _waveCounter = pws.WaveCounter;
    _startPosition = pws.StartPosition;
    _targetPosition = pws.TargetPosition;
    _currentPerimeterPosition = pws.CurrentPerimeterPosition;
    _totalMoveDistance = pws.TotalMoveDistance;
    _moveDirection = pws.MoveDirection;
  }
}
