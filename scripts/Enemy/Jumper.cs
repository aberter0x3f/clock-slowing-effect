using Bullet;
using Godot;
using Rewind;

namespace Enemy;

public class JumperState : BaseEnemyState {
  public Jumper.State CurrentState;
  public float StateTimer;
  public float ShootTimer;
  public Vector3 JumpStartPosition;
  public Vector3 JumpTargetPosition;
  public float JumpDuration;
  public float JumpTime;
}

public partial class Jumper : BaseEnemy {
  public enum State {
    RandomWalk,
    Jumping
  }

  private State _currentState = State.RandomWalk;
  private float _stateTimer;
  private float _shootTimer;

  private Vector3 _jumpStartPosition;
  private Vector3 _jumpTargetPosition;
  private float _jumpDuration;
  private float _jumpTime;

  private RandomWalkComponent _randomWalkComponent;
  private readonly RandomNumberGenerator _rnd = new();

  [ExportGroup("State Durations")]
  [Export] public float RandomWalkDuration { get; set; } = 4.0f;

  [ExportGroup("Attack Behavior")]
  [Export] public PackedScene BulletScene { get; set; }
  [Export] public float JumpSpeed { get; set; } = 4f;
  [Export] public float ShootInterval { get; set; } = 0.08f;
  [Export] public float JumpHeight { get; set; } = 1.5f;
  [Export] public float MinJumpDistance { get; set; } = 3f;
  [Export] public float MinPlayerAvoidanceDistance { get; set; } = 2f;

  public override void _Ready() {
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
    _stateTimer = _rnd.RandfRange(1f, RandomWalkDuration);
    base._Ready();
  }

  public override void UpdateEnemy(float scaledDelta, float effectiveTimeScale) {
    _stateTimer -= scaledDelta;

    switch (_currentState) {
      case State.RandomWalk:
        Velocity = _randomWalkComponent.TargetVelocity * effectiveTimeScale;
        if (_stateTimer <= 0) SwitchToJumpingState();
        break;

      case State.Jumping:
        HandleJumpingState(scaledDelta);
        break;
    }
  }

  private void HandleJumpingState(float scaledDelta) {
    _jumpTime += scaledDelta;
    float progress = Mathf.Min(1.0f, _jumpTime / _jumpDuration);

    // 3D 抛物线插值：XZ 平面线性移动，Y 轴正弦跳跃
    Vector3 currentPos = _jumpStartPosition.Lerp(_jumpTargetPosition, progress);
    currentPos.Y = Mathf.Sin(progress * Mathf.Pi) * JumpHeight;
    GlobalPosition = currentPos;

    _shootTimer -= scaledDelta;
    if (_shootTimer <= 0) {
      ShootBullet();
      _shootTimer = ShootInterval;
    }

    if (progress >= 1.0f) {
      GlobalPosition = _jumpTargetPosition with { Y = 0 };
      SwitchToRandomWalkState();
    }
  }

  private void SwitchToJumpingState() {
    var target = PlayerNode;
    if (_mapGenerator == null || target == null) {
      _stateTimer = 0.1f;
      return;
    }

    // 寻找有效落点
    for (int i = 0; i < 30; ++i) {
      int idx = _rnd.RandiRange(0, _mapGenerator.WalkableTiles.Count - 1);
      Vector3 worldPos = _mapGenerator.MapToWorld(_mapGenerator.WalkableTiles[idx]);

      if (worldPos.DistanceTo(GlobalPosition) >= MinJumpDistance &&
          worldPos.DistanceTo(target.GlobalPosition) >= MinPlayerAvoidanceDistance) {
        _jumpStartPosition = GlobalPosition;
        _jumpTargetPosition = worldPos;
        _jumpDuration = Mathf.Max(worldPos.DistanceTo(GlobalPosition) / JumpSpeed, 0.6f);
        _jumpTime = 0;
        _shootTimer = 0;
        _currentState = State.Jumping;
        SoundManager.Instance.Play(SoundEffect.FireSmall);
        return;
      }
    }
    _stateTimer = 0.1f; // 没找到位置就继续走
  }

  private void SwitchToRandomWalkState() {
    _currentState = State.RandomWalk;
    _stateTimer = RandomWalkDuration;
  }

  private void ShootBullet() {
    if (BulletScene == null || PlayerNode == null) return;
    var bullet = BulletScene.Instantiate<SimpleBullet>();

    Vector3 startPos = GlobalPosition;
    Vector3 targetPos = PlayerNode.GlobalPosition;
    Vector3 direction = (targetPos - startPos).Normalized();

    float v0 = 1.5f;   // 初始速度
    float a = 3.0f;    // 加速度
    float vMax = 4.0f; // 最大速度

    // 最大速度所需的时间和位移
    float tCap = (vMax - v0) / a;
    float dCap = v0 * tCap + 0.5f * a * tCap * tCap;

    bullet.UpdateFunc = (time) => {
      SimpleBullet.UpdateState state = new();
      float distance;
      if (time < tCap) {
        distance = v0 * time + 0.5f * a * time * time;
      } else {
        distance = dCap + vMax * (time - tCap);
      }
      state.position = startPos + direction * distance;
      if (state.position.Y < 0) state.position.Y = 0;
      return state;
    };

    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  public override RewindState CaptureState() {
    var baseState = (BaseEnemyState) base.CaptureState();
    return new JumperState {
      GlobalPosition = baseState.GlobalPosition,
      Velocity = baseState.Velocity,
      Health = baseState.Health,
      HitTimerLeft = baseState.HitTimerLeft,
      IsInHitState = baseState.IsInHitState,
      CurrentState = _currentState,
      StateTimer = _stateTimer,
      ShootTimer = _shootTimer,
      JumpStartPosition = _jumpStartPosition,
      JumpTargetPosition = _jumpTargetPosition,
      JumpDuration = _jumpDuration,
      JumpTime = _jumpTime
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not JumperState js) return;
    _currentState = js.CurrentState;
    _stateTimer = js.StateTimer;
    _shootTimer = js.ShootTimer;
    _jumpStartPosition = js.JumpStartPosition;
    _jumpTargetPosition = js.JumpTargetPosition;
    _jumpDuration = js.JumpDuration;
    _jumpTime = js.JumpTime;
  }
}
