using Godot;

namespace Enemy;

public partial class Drummer : BaseEnemy {
  private enum State {
    Idle,
    Attacking,
    Retreating
  }

  // 攻击子状态机
  private enum AttackSubState {
    None,
    Jumping,
    Firing, // 这个状态现在会处理带间隔的连续发射
    Pausing // 攻击循环之间的大停顿
  }
  private AttackSubState _attackSubState = AttackSubState.None;
  private int _attackLoopCounter; // 主攻击循环计数 (0, 1, 2)
  private int _fireSubLoopCounter; // Firing 状态下的子循环计数 (0, 1, 2)
  private float _attackTimer; // 用于所有攻击相关的计时

  // --- 跳跃状态变量 ---
  private Vector2 _jumpStartPosition;
  private Vector2 _jumpTargetPosition;
  private float _jumpDuration;
  private float _jumpTime;

  private State _currentState = State.Idle;
  private float _attackCooldown;
  private Vector2 _retreatDirection;
  private float _retreatTimer;
  private float _currentJumpHeight = 0f;

  private MapGenerator _mapGenerator;
  private CollisionShape2D _bodyCollisionShape;

  [ExportGroup("Attack Configuration")]
  [Export]
  public PackedScene SmallBulletScene { get; set; }
  [Export]
  public PackedScene LargeBulletScene { get; set; }
  [Export]
  public float AttackInterval { get; set; } = 3.0f;
  [Export(PropertyHint.Range, "1, 50, 1")]
  public int SmallBulletCount { get; set; } = 24;
  [Export(PropertyHint.Range, "1, 50, 1")]
  public int LargeBulletCount { get; set; } = 24;

  [ExportGroup("Movement Configuration")]
  [Export(PropertyHint.Range, "100, 2000, 10")]
  public float LungeSpeed { get; set; } = 400.0f;
  [Export(PropertyHint.Range, "100, 2000, 10")]
  public float LungeDistance { get; set; } = 200.0f;
  [Export(PropertyHint.Range, "50, 500, 10")]
  public float JumpHeight { get; set; } = 150.0f;
  [Export(PropertyHint.Range, "50, 300, 10")]
  public float PlayerAvoidanceDistance { get; set; } = 150.0f;
  [Export(PropertyHint.Range, "100, 2000, 10")]
  public float RetreatSpeed { get; set; } = 300.0f;
  [Export(PropertyHint.Range, "0.5, 5.0, 0.1")]
  public float RetreatDuration { get; set; } = 1.0f;

  public override void _Ready() {
    base._Ready();
    _attackCooldown = (float) GD.RandRange(1.0f, 2 * AttackInterval);
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr("Drummer: MapGenerator not found! Jump validation will be disabled.");
    }
    _bodyCollisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    switch (_currentState) {
      case State.Idle:
        HandleIdleState(scaledDelta);
        break;
      case State.Attacking:
        HandleAttackingState(scaledDelta);
        break;
      case State.Retreating:
        HandleRetreatingState(scaledDelta);
        break;
    }
    MoveAndSlide();
    UpdateVisualizer();
  }

  protected override void UpdateVisualizer() {
    var position3D = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );
    position3D.Y += _currentJumpHeight * GameConstants.WorldScaleFactor;
    _visualizer.GlobalPosition = position3D;
  }

  private void HandleIdleState(float scaledDelta) {
    Velocity = Vector2.Zero;
    _attackCooldown -= scaledDelta;
    if (_attackCooldown <= 0) {
      if (_player != null && IsInstanceValid(_player)) {
        StartAttackSequence();
      }
    }
  }

  private void HandleRetreatingState(float scaledDelta) {
    Velocity = _retreatDirection * RetreatSpeed * TimeManager.Instance.TimeScale;
    _retreatTimer -= scaledDelta;
    if (_retreatTimer <= 0) {
      _currentState = State.Idle;
      _attackCooldown = AttackInterval;
    }
  }

  private void StartAttackSequence() {
    if (_currentState != State.Idle) return;
    _currentState = State.Attacking;
    _attackLoopCounter = 0;
    PrepareNextJump();
  }

  private void HandleAttackingState(float scaledDelta) {
    Velocity = Vector2.Zero;
    _attackTimer -= scaledDelta;

    switch (_attackSubState) {
      case AttackSubState.Jumping:
        ProcessJump(scaledDelta);
        break;

      case AttackSubState.Firing:
        if (_attackTimer <= 0) {
          // --- 检查是小弹幕还是大弹幕 ---
          if (_attackLoopCounter < 2) {
            // --- 小弹幕循环 ---
            FireBulletCircle(SmallBulletScene, SmallBulletCount);
            _fireSubLoopCounter++;

            if (_fireSubLoopCounter < 3) {
              // 还没射完 3 波，设置 0.1 秒的间隔
              _attackTimer = 0.1f;
            } else {
              // 3 波射完了，进入大停顿
              _attackSubState = AttackSubState.Pausing;
              _attackTimer = 0.4f;
            }
          } else {
            // --- 大弹幕（最后一次） ---
            FireBulletCircle(LargeBulletScene, LargeBulletCount);
            // 射完了，进入大停顿
            _attackSubState = AttackSubState.Pausing;
            _attackTimer = 0.4f;
          }
        }
        break;

      case AttackSubState.Pausing:
        if (_attackTimer <= 0) {
          _attackLoopCounter++;
          if (_attackLoopCounter < 3) {
            PrepareNextJump(); // 准备下一次跳跃
          } else {
            // 3 次攻击循环全部结束，转换到撤退状态
            _retreatDirection = (GlobalPosition - _player.GlobalPosition).Normalized();
            _retreatTimer = RetreatDuration;
            _currentState = State.Retreating;
            _attackSubState = AttackSubState.None;
          }
        }
        break;
    }
  }

  private void PrepareNextJump() {
    Vector2 attackDirection = (_player.GlobalPosition - GlobalPosition).Normalized();
    Vector2 startPos = GlobalPosition;
    float distanceToPlayer = startPos.DistanceTo(_player.GlobalPosition);
    float actualLungeDistance = Mathf.Min(LungeDistance, distanceToPlayer - PlayerAvoidanceDistance);

    if (actualLungeDistance > 0) {
      Vector2 targetPos = startPos + attackDirection * actualLungeDistance;
      bool canJump = _mapGenerator == null || _mapGenerator.IsWalkable(_mapGenerator.WorldToMap(targetPos));

      if (canJump) {
        _jumpStartPosition = GlobalPosition;
        _jumpTargetPosition = targetPos;
        float distance = _jumpStartPosition.DistanceTo(_jumpTargetPosition);
        _jumpDuration = distance / LungeSpeed;
        _jumpTime = 0f;
        _attackSubState = AttackSubState.Jumping;
        SetCollisionsEnabled(false);
        return;
      }
    }

    // 如果不能跳跃，直接进入开火阶段
    InitializeFiringState();
  }

  private void ProcessJump(float scaledDelta) {
    // 注意：跳跃动画不依赖 _attackTimer，它有自己的计时器 _jumpTime
    _jumpTime += scaledDelta;
    float progress = Mathf.Min(1.0f, _jumpTime / _jumpDuration);

    GlobalPosition = _jumpStartPosition.Lerp(_jumpTargetPosition, progress);
    _currentJumpHeight = Mathf.Sin(progress * Mathf.Pi) * JumpHeight;

    if (progress >= 1.0f) {
      GlobalPosition = _jumpTargetPosition;
      _currentJumpHeight = 0f;
      SetCollisionsEnabled(true);
      // 跳跃结束，初始化并进入开火状态
      InitializeFiringState();
    }
  }

  /// <summary>
  /// 重置开火状态的计数器和计时器，并切换到 Firing 状态．
  /// </summary>
  private void InitializeFiringState() {
    _attackSubState = AttackSubState.Firing;
    _fireSubLoopCounter = 0;
    _attackTimer = 0; // 立即发射第一波
  }

  private void SetCollisionsEnabled(bool enabled) {
    if (_bodyCollisionShape != null) {
      _bodyCollisionShape.Disabled = !enabled;
    }
  }

  private void FireBulletCircle(PackedScene bulletScene, int count) {
    if (bulletScene == null) {
      GD.PrintErr($"Drummer: Bullet scene is not set!");
      return;
    }
    if (count <= 0) return;

    float angleStep = Mathf.Tau / count;

    for (int i = 0; i < count; i++) {
      float angle = i * angleStep;
      Vector2 direction = Vector2.Right.Rotated(angle);

      var bullet = bulletScene.Instantiate<Bullet.SimpleBullet>();
      bullet.GlobalPosition = GlobalPosition;
      bullet.Rotation = direction.Angle();

      GetTree().Root.AddChild(bullet);
    }
  }
}
