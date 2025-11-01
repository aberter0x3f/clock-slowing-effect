using Bullet;
using Godot;
using Rewind;

namespace Enemy;

public class SimpleEnemy5State : BaseEnemyState {
  public SimpleEnemy5.State CurrentState;
  public float StateTimer;
  public float ShootTimer;
  public Vector2 JumpStartPosition;
  public Vector2 JumpTargetPosition;
  public float JumpDuration;
  public float JumpTime;
  public float CurrentJumpHeight;
}

public partial class SimpleEnemy5 : BaseEnemy {
  public enum State {
    RandomWalk,
    Jumping
  }

  private State _currentState;
  private float _stateTimer; // 当前状态的剩余时间
  private float _shootTimer; // 攻击状态下，距离下次射击的时间

  // --- 跳跃状态变量 ---
  private Vector2 _jumpStartPosition;
  private Vector2 _jumpTargetPosition;
  private float _jumpDuration;
  private float _jumpTime;
  private float _currentJumpHeight = 0f;

  private RandomWalkComponent _randomWalkComponent;
  private readonly RandomNumberGenerator _rnd = new();

  [ExportGroup("State Durations")]
  [Export(PropertyHint.Range, "0.1, 20.0, 0.1")]
  public float RandomWalkDuration { get; set; } = 4.0f;

  [ExportGroup("Attack Behavior")]
  [Export]
  public PackedScene BulletScene { get; set; }
  [Export(PropertyHint.Range, "50, 500, 5")]
  public float JumpSpeed { get; set; } = 400.0f;
  [Export(PropertyHint.Range, "0.01, 2.0, 0.01")]
  public float ShootInterval { get; set; } = 0.08f;
  [Export(PropertyHint.Range, "50, 500, 10")]
  public float JumpHeight { get; set; } = 150.0f;
  [Export(PropertyHint.Range, "50, 1000, 10")]
  public float MinJumpDistance { get; set; } = 300.0f;
  [Export(PropertyHint.Range, "50, 1000, 10")]
  public float MinPlayerAvoidanceDistance { get; set; } = 200.0f;

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");

    // 初始化状态机，并为第一次随机游走设置一个随机时长
    _currentState = State.RandomWalk;
    _stateTimer = (float) _rnd.RandfRange(RandomWalkDuration / 5, RandomWalkDuration);
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
      case State.Jumping:
        HandleJumpingState(scaledDelta);
        break;
    }

    UpdateVisualizer();
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    // 仅在游走时移动．跳跃移动在 _Process 中处理．
    if (_currentState == State.RandomWalk) {
      MoveAndSlide();
    }
  }

  protected override void UpdateVisualizer() {
    var position3D = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY + _currentJumpHeight * GameConstants.WorldScaleFactor,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );
    _visualizer.GlobalPosition = position3D;
  }

  private void HandleRandomWalkState() {
    // 使用 RandomWalkComponent 来决定速度
    Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;

    // 检查是否需要切换到攻击状态
    if (_stateTimer <= 0) {
      SwitchToJumpingState();
    }
  }

  private void HandleJumpingState(float scaledDelta) {
    Velocity = Vector2.Zero; // 移动由 Lerp 处理
    _jumpTime += scaledDelta;
    float progress = Mathf.Min(1.0f, _jumpTime / _jumpDuration);

    GlobalPosition = _jumpStartPosition.Lerp(_jumpTargetPosition, progress);
    _currentJumpHeight = Mathf.Sin(progress * Mathf.Pi) * JumpHeight;

    // 处理射击逻辑
    _shootTimer -= scaledDelta;
    if (_shootTimer <= 0) {
      Shoot();
      _shootTimer = ShootInterval;
    }

    // 检查跳跃是否结束
    if (progress >= 1.0f) {
      GlobalPosition = _jumpTargetPosition;
      _currentJumpHeight = 0f;
      SwitchToRandomWalkState();
    }
  }

  private void SwitchToJumpingState() {
    if (_mapGenerator == null || _player == null || !IsInstanceValid(_player)) {
      // 无法跳跃，继续游走
      _stateTimer = RandomWalkDuration;
      return;
    }

    Vector2 targetPos = Vector2.Zero;
    bool foundTarget = false;
    int attempts = 0;
    while (attempts < 50 && !foundTarget) {
      ++attempts;
      int randomIndex = _rnd.RandiRange(0, _mapGenerator.WalkableTiles.Count - 1);
      Vector2I cell = _mapGenerator.WalkableTiles[randomIndex];
      Vector2 worldPos = _mapGenerator.MapToWorld(cell);

      if (worldPos.DistanceTo(GlobalPosition) >= MinJumpDistance &&
          worldPos.DistanceTo(_player.GlobalPosition) >= MinPlayerAvoidanceDistance) {
        targetPos = worldPos;
        foundTarget = true;
      }
    }

    if (!foundTarget) {
      // 找不到合适的落点，继续游走
      _stateTimer = RandomWalkDuration;
      return;
    }

    _currentState = State.Jumping;
    _jumpStartPosition = GlobalPosition;
    _jumpTargetPosition = targetPos;
    float distance = _jumpStartPosition.DistanceTo(_jumpTargetPosition);
    _jumpDuration = Mathf.Max(distance / JumpSpeed, 0.5f);
    _jumpTime = 0f;
    _shootTimer = 0; // 立即射击
  }

  private void SwitchToRandomWalkState() {
    _currentState = State.RandomWalk;
    // 后续的随机游走都使用完整的设定时长
    _stateTimer = RandomWalkDuration;
  }

  private void Shoot() {
    if (_player == null || !IsInstanceValid(_player) || BulletScene == null) return;

    var bullet = BulletScene.Instantiate<Bullet.SimpleBullet3D>();

    // 敌人的当前 3D 位置（在子弹的坐标系中）
    var enemyPos3D = new Vector3(GlobalPosition.X, GlobalPosition.Y, _currentJumpHeight);
    // 玩家的 3D 位置（在游戏平面上，Z=0）
    var playerPos3D = new Vector3(_player.GlobalPosition.X, _player.GlobalPosition.Y, 0);

    var direction = (playerPos3D - enemyPos3D).Normalized();

    bullet.RawPosition = enemyPos3D;
    bullet.Velocity = direction;

    GameRootProvider.CurrentGameRoot.AddChild(bullet);
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
      JumpStartPosition = this._jumpStartPosition,
      JumpTargetPosition = this._jumpTargetPosition,
      JumpDuration = this._jumpDuration,
      JumpTime = this._jumpTime,
      CurrentJumpHeight = this._currentJumpHeight
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not SimpleEnemy5State ses) return;
    this._currentState = ses.CurrentState;
    this._stateTimer = ses.StateTimer;
    this._shootTimer = ses.ShootTimer;
    this._jumpStartPosition = ses.JumpStartPosition;
    this._jumpTargetPosition = ses.JumpTargetPosition;
    this._jumpDuration = ses.JumpDuration;
    this._jumpTime = ses.JumpTime;
    this._currentJumpHeight = ses.CurrentJumpHeight;
  }
}
