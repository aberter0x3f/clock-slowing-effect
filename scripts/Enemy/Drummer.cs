using Bullet;
using Godot;
using Rewind;

namespace Enemy;

public class DrummerState : BaseEnemyState {
  public Drummer.State CurrentState;
  public Drummer.AttackSubState AttackSubState;
  public int AttackLoopCounter;
  public int FireSubLoopCounter;
  public float AttackTimer;
  public Vector3 JumpStartPosition;
  public Vector3 JumpTargetPosition;
  public float JumpDuration;
  public float JumpTime;
  public float AttackCooldown;
  public Vector3 RetreatDirection;
  public float RetreatTimer;
}

public partial class Drummer : BaseEnemy {
  public enum State { Idle, Attacking, Retreating }
  public enum AttackSubState { None, Jumping, Firing, Pausing }

  private State _currentState = State.Idle;
  private AttackSubState _attackSubState = AttackSubState.None;
  private int _attackLoopCounter;
  private int _fireSubLoopCounter;
  private float _attackTimer;

  private Vector3 _jumpStartPosition;
  private Vector3 _jumpTargetPosition;
  private float _jumpDuration;
  private float _jumpTime;
  private Vector3 _retreatDirection;
  private float _retreatTimer;
  private float _attackCooldown;

  private RandomWalkComponent _randomWalkComponent;

  [ExportGroup("Attack Configuration")]
  [Export] public PackedScene SmallBulletScene { get; set; }
  [Export] public PackedScene LargeBulletScene { get; set; }
  [Export] public float AttackInterval { get; set; } = 3.0f;
  [Export] public int SmallBulletCount { get; set; } = 24;
  [Export] public int LargeBulletCount { get; set; } = 24;

  [ExportGroup("Movement Configuration")]
  [Export] public float LungeSpeed { get; set; } = 4.0f;     // 400 * 0.01
  [Export] public float LungeDistance { get; set; } = 2.0f;  // 200 * 0.01
  [Export] public float JumpHeight { get; set; } = 1.5f;     // 150 * 0.01
  [Export] public float PlayerAvoidanceDistance { get; set; } = 1.5f;
  [Export] public float RetreatSpeed { get; set; } = 3.0f;
  [Export] public float RetreatDuration { get; set; } = 1.0f;

  public override void _Ready() {
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
    _attackCooldown = (float) GD.RandRange(1.0, AttackInterval);
    base._Ready();
  }

  public override void UpdateEnemy(float scaledDelta, float effectiveTimeScale) {
    switch (_currentState) {
      case State.Idle:
        Velocity = _randomWalkComponent.TargetVelocity * effectiveTimeScale;
        _attackCooldown -= scaledDelta;
        if (_attackCooldown <= 0) StartAttackSequence();
        break;

      case State.Attacking:
        HandleAttackingState(scaledDelta);
        break;

      case State.Retreating:
        Velocity = _retreatDirection * RetreatSpeed * effectiveTimeScale;
        _retreatTimer -= scaledDelta;
        if (_retreatTimer <= 0) {
          _currentState = State.Idle;
          _attackCooldown = AttackInterval;
        }
        break;
    }
  }

  private void StartAttackSequence() {
    _currentState = State.Attacking;
    _attackLoopCounter = 0;
    PrepareNextJump();
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);
    MoveAndSlide();
  }

  private void HandleAttackingState(float scaledDelta) {
    Velocity = Vector3.Zero;
    _attackTimer -= scaledDelta;

    switch (_attackSubState) {
      case AttackSubState.Jumping:
        _jumpTime += scaledDelta;
        float progress = Mathf.Min(1.0f, _jumpTime / _jumpDuration);
        Vector3 groundPos = _jumpStartPosition.Lerp(_jumpTargetPosition, progress);
        GlobalPosition = groundPos with { Y = Mathf.Sin(progress * Mathf.Pi) * JumpHeight };

        if (progress >= 1.0f) {
          GlobalPosition = _jumpTargetPosition with { Y = 0 };
          InitializeFiringState();
        }
        break;

      case AttackSubState.Firing:
        if (_attackTimer <= 0) {
          bool isFinal = _attackLoopCounter >= 2;
          FireBulletCircle(isFinal ? LargeBulletScene : SmallBulletScene, isFinal ? LargeBulletCount : SmallBulletCount);
          ++_fireSubLoopCounter;

          if (!isFinal && _fireSubLoopCounter < 3) _attackTimer = 0.1f;
          else {
            _attackSubState = AttackSubState.Pausing;
            _attackTimer = 0.4f;
          }
        }
        break;

      case AttackSubState.Pausing:
        if (_attackTimer <= 0) {
          ++_attackLoopCounter;
          if (_attackLoopCounter < 3) PrepareNextJump();
          else {
            _retreatDirection = (GlobalPosition - PlayerNode.GlobalPosition).Normalized() with { Y = 0 };
            _retreatTimer = RetreatDuration;
            _currentState = State.Retreating;
          }
        }
        break;
    }
  }

  private void PrepareNextJump() {
    Vector3 toPlayer = (PlayerNode.GlobalPosition - GlobalPosition);
    toPlayer.Y = 0;
    float dist = toPlayer.Length();
    float actualLunge = Mathf.Min(LungeDistance, dist - PlayerAvoidanceDistance);

    if (actualLunge > 0.1f) {
      _jumpStartPosition = GlobalPosition;
      _jumpTargetPosition = GlobalPosition + toPlayer.Normalized() * actualLunge;
      _jumpDuration = Mathf.Max(actualLunge / LungeSpeed, 0.5f);
      _jumpTime = 0;
      _attackSubState = AttackSubState.Jumping;
    } else {
      InitializeFiringState();
    }
  }

  private void InitializeFiringState() {
    _attackSubState = AttackSubState.Firing;
    _fireSubLoopCounter = 0;
    _attackTimer = 0;
  }

  private void FireBulletCircle(PackedScene scene, int count) {
    if (scene == null || count <= 0) return;
    float step = Mathf.Tau / count;
    Vector3 pos = GlobalPosition;
    SoundManager.Instance.Play(SoundEffect.FireSmall);
    for (int i = 0; i < count; ++i) {
      var bullet = scene.Instantiate<SimpleBullet>();
      Vector3 dir = Vector3.Right.Rotated(Vector3.Up, i * step);
      bullet.UpdateFunc = (t) => {
        SimpleBullet.UpdateState s = new();
        s.position = pos + dir * (t * 2.0f);
        return s;
      };
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  public override RewindState CaptureState() {
    var bs = (BaseEnemyState) base.CaptureState();
    return new DrummerState {
      GlobalPosition = bs.GlobalPosition,
      Velocity = bs.Velocity,
      Health = bs.Health,
      HitTimerLeft = bs.HitTimerLeft,
      IsInHitState = bs.IsInHitState,
      CurrentState = _currentState,
      AttackSubState = _attackSubState,
      AttackLoopCounter = _attackLoopCounter,
      FireSubLoopCounter = _fireSubLoopCounter,
      AttackTimer = _attackTimer,
      JumpStartPosition = _jumpStartPosition,
      JumpTargetPosition = _jumpTargetPosition,
      JumpDuration = _jumpDuration,
      JumpTime = _jumpTime,
      AttackCooldown = _attackCooldown,
      RetreatDirection = _retreatDirection,
      RetreatTimer = _retreatTimer
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not DrummerState ds) return;
    _currentState = ds.CurrentState; _attackSubState = ds.AttackSubState;
    _attackLoopCounter = ds.AttackLoopCounter; _fireSubLoopCounter = ds.FireSubLoopCounter;
    _attackTimer = ds.AttackTimer; _jumpStartPosition = ds.JumpStartPosition;
    _jumpTargetPosition = ds.JumpTargetPosition; _jumpDuration = ds.JumpDuration;
    _jumpTime = ds.JumpTime; _attackCooldown = ds.AttackCooldown;
    _retreatDirection = ds.RetreatDirection; _retreatTimer = ds.RetreatTimer;
  }
}
