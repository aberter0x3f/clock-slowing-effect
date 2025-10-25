using Bullet;
using Godot;
using Rewind;

namespace Enemy;

public class SimpleEnemy5State : BaseEnemyState {
  public SimpleEnemy5.State CurrentState;
  public float StateTimer;
  public float ShootTimer;
  public Vector2 AttackMoveDirection;
}

public partial class SimpleEnemy5 : BaseEnemy {
  public enum State {
    RandomWalk,
    Attacking
  }

  private State _currentState;
  private float _stateTimer; // 当前状态的剩余时间
  private float _shootTimer; // 攻击状态下，距离下次射击的时间
  private Vector2 _attackMoveDirection; // 攻击状态下的扫射移动方向

  private RandomWalkComponent _randomWalkComponent;
  private readonly RandomNumberGenerator _rnd = new();

  [ExportGroup("State Durations")]
  [Export(PropertyHint.Range, "0.1, 20.0, 0.1")]
  public float RandomWalkDuration { get; set; } = 4.0f;
  [Export(PropertyHint.Range, "0.1, 20.0, 0.1")]
  public float AttackDuration { get; set; } = 1.0f;

  [ExportGroup("Attack Behavior")]
  [Export]
  public PackedScene Bullet { get; set; }
  [Export(PropertyHint.Range, "50, 500, 5")]
  public float AttackMoveSpeed { get; set; } = 600.0f;
  [Export(PropertyHint.Range, "0.1, 5.0, 0.05")]
  public float ShootInterval { get; set; } = 0.08f;

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");

    // 初始化状态机，并为第一次随机游走设置一个随机时长
    _currentState = State.RandomWalk;
    _stateTimer = (float) _rnd.RandfRange(RandomWalkDuration / 5, RandomWalkDuration * 1.2f);
    _shootTimer = ShootInterval;
  }

  public override void _Process(double delta) {
    base._Process(delta);
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;
    _stateTimer -= scaledDelta;

    // 根据当前状态处理逻辑和状态转换
    switch (_currentState) {
      case State.RandomWalk:
        HandleRandomWalkState();
        break;
      case State.Attacking:
        HandleAttackingState(scaledDelta);
        break;
    }

    UpdateVisualizer();
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    MoveAndSlide();

    // 根据当前状态处理撞墙逻辑
    if (_currentState == State.Attacking && IsOnWall()) {
      _attackMoveDirection *= -1; // 攻击时撞墙则反向移动
    }
  }

  private void HandleRandomWalkState() {
    // 使用 RandomWalkComponent 来决定速度
    Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;

    // 检查是否需要切换到攻击状态
    if (_stateTimer <= 0) {
      SwitchToAttackingState();
    }
  }

  private void HandleAttackingState(float scaledDelta) {
    // 沿垂直于玩家的方向移动
    Velocity = _attackMoveDirection * AttackMoveSpeed * TimeManager.Instance.TimeScale;

    // 处理射击逻辑
    _shootTimer -= scaledDelta;
    if (_shootTimer <= 0) {
      Shoot();
      _shootTimer = ShootInterval;
    }

    // 检查是否需要切换回随机游走状态
    if (_stateTimer <= 0) {
      SwitchToRandomWalkState();
    }
  }

  private void SwitchToAttackingState() {
    _currentState = State.Attacking;
    _stateTimer = AttackDuration;
    _shootTimer = ShootInterval; // 为新的攻击阶段重置射击计时器

    if (_player == null || !IsInstanceValid(_player)) return;

    // 随机选择一个与玩家连线垂直的方向
    Vector2 toPlayer = (_player.GlobalPosition - GlobalPosition).Normalized();
    Vector2 perpendicular = toPlayer.Rotated(Mathf.Pi / 2.0f);
    _attackMoveDirection = _rnd.Randf() > 0.5f ? perpendicular : -perpendicular;
  }

  private void SwitchToRandomWalkState() {
    _currentState = State.RandomWalk;
    // 后续的随机游走都使用完整的设定时长
    _stateTimer = RandomWalkDuration;
  }

  private void Shoot() {
    if (_player == null || !IsInstanceValid(_player) || Bullet == null) return;

    var bullet = Bullet.Instantiate<SimpleBullet>();
    // 每次射击都重新瞄准玩家当前位置
    var direction = (_player.GlobalPosition - GlobalPosition).Normalized();

    bullet.GlobalPosition = GlobalPosition;
    bullet.Rotation = direction.Angle();
    // 子弹的 _Ready() 函数会根据其 Rotation 和 InitialSpeed 设置初始速度

    GetTree().Root.AddChild(bullet);
  }

  public override RewindState CaptureState() {
    var baseState = (BaseEnemyState) base.CaptureState();
    return new SimpleEnemy5State {
      GlobalPosition = baseState.GlobalPosition,
      Velocity = baseState.Velocity,
      Health = baseState.Health,
      HitTimerLeft = baseState.HitTimerLeft,
      SpriteModulate = baseState.SpriteModulate,
      CurrentState = this._currentState,
      StateTimer = this._stateTimer,
      ShootTimer = this._shootTimer,
      AttackMoveDirection = this._attackMoveDirection
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not SimpleEnemy5State ses) return;
    this._currentState = ses.CurrentState;
    this._stateTimer = ses.StateTimer;
    this._shootTimer = ses.ShootTimer;
    this._attackMoveDirection = ses.AttackMoveDirection;
  }
}
