using Godot;

namespace Enemy;

public partial class Drummer : BaseEnemy {
  private enum State {
    Idle,
    Attacking,
    Retreating
  }

  private State _currentState = State.Idle;
  private float _attackCooldown;
  private Vector2 _retreatDirection;
  private float _retreatTimer;

  [ExportGroup("Attack Configuration")]
  [Export]
  public PackedScene SmallBulletScene { get; set; }
  [Export]
  public PackedScene LargeBulletScene { get; set; }
  [Export]
  public float AttackInterval { get; set; } = 3.0f; // 攻击序列之间的间隔
  [Export(PropertyHint.Range, "1, 50, 1")]
  public int SmallBulletCount { get; set; } = 24; // 每个小弹幕圈的子弹数量
  [Export(PropertyHint.Range, "1, 50, 1")]
  public int LargeBulletCount { get; set; } = 24; // 大弹幕圈的子弹数量

  [ExportGroup("Movement Configuration")]
  [Export(PropertyHint.Range, "100, 2000, 10")]
  public float LungeSpeed { get; set; } = 400.0f; // 突进速度
  [Export(PropertyHint.Range, "100, 2000, 10")]
  public float LungeDistance { get; set; } = 200.0f; // 每次突进的最大距离
  [Export(PropertyHint.Range, "50, 300, 10")]
  public float PlayerAvoidanceDistance { get; set; } = 150.0f; // 与玩家保持的最小距离
  [Export(PropertyHint.Range, "100, 2000, 10")]
  public float RetreatSpeed { get; set; } = 300.0f; // 撤退速度
  [Export(PropertyHint.Range, "0.5, 5.0, 0.1")]
  public float RetreatDuration { get; set; } = 1.0f; // 撤退持续时间

  public override void _Ready() {
    base._Ready();
    // 随机化初始冷却时间，以错开多个鼓手的攻击
    _attackCooldown = (float) GD.RandRange(1.0f, 2 * AttackInterval);
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    switch (_currentState) {
      case State.Idle:
        HandleIdleState(scaledDelta);
        break;
      case State.Attacking:
        // 移动逻辑由异步的 AttackSequence 方法通过 Tween 处理
        Velocity = Vector2.Zero;
        break;
      case State.Retreating:
        HandleRetreatingState(scaledDelta);
        break;
    }
    MoveAndSlide();
  }

  private void HandleIdleState(float scaledDelta) {
    Velocity = Vector2.Zero;
    _attackCooldown -= scaledDelta;
    if (_attackCooldown <= 0) {
      if (_player != null && IsInstanceValid(_player)) {
        // 启动异步攻击序列，它会自动将状态切换到 Attacking
        AttackSequence();
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

  private async void AttackSequence() {
    if (_currentState != State.Idle) return;
    _currentState = State.Attacking;

    // --- 1. 准备阶段 ---
    // 挑选一个朝向玩家的方向。这个方向在三次突进中保持不变。
    Vector2 attackDirection = (_player.GlobalPosition - GlobalPosition).Normalized();

    // --- 2. 执行三次突进和攻击 ---
    for (int i = 0; i < 3; i++) {
      // --- 突进 ---
      Vector2 startPos = GlobalPosition;
      // 计算到玩家的距离，以确保突进不会撞到玩家
      float distanceToPlayer = startPos.DistanceTo(_player.GlobalPosition);
      // 实际突进距离不能超过（到玩家的距离 - 安全距离）
      float actualLungeDistance = Mathf.Min(LungeDistance, distanceToPlayer - PlayerAvoidanceDistance);

      // 如果我们已经太近，就不再向前突进
      if (actualLungeDistance > 0) {
        Vector2 finalTargetPos = startPos + attackDirection * actualLungeDistance;
        float duration = actualLungeDistance / LungeSpeed;

        // 使用 Tween 来平滑地移动到目标位置
        var tween = CreateTween().SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.Out);
        tween.TweenProperty(this, "global_position", finalTargetPos, duration);
        await ToSignal(tween, Tween.SignalName.Finished);
      }

      // --- 敲鼓 (发射子弹) ---
      if (i < 2) {
        // 前两次敲鼓：发射两圈小弹
        FireBulletCircle(SmallBulletScene, SmallBulletCount);
        await GetTree().CreateTimeScaleTimer(0.1f); // 两圈之间短暂的延迟
        FireBulletCircle(SmallBulletScene, SmallBulletCount);
      } else {
        // 最后一次敲鼓：发射一圈大弹
        FireBulletCircle(LargeBulletScene, LargeBulletCount);
      }

      // 在下次突进前稍作等待
      await GetTree().CreateTimeScaleTimer(0.4f);
    }

    // --- 3. 转换到撤退状态 ---
    // 计算一次撤退方向，远离玩家当前位置
    _retreatDirection = (GlobalPosition - _player.GlobalPosition).Normalized();
    _retreatTimer = RetreatDuration;
    _currentState = State.Retreating;
  }

  private void FireBulletCircle(PackedScene bulletScene, int count) {
    if (bulletScene == null) {
      GD.PrintErr($"Drummer: Bullet scene is not set!");
      return;
    }
    if (count <= 0) return;

    float angleStep = Mathf.Tau / count; // Tau is 2 * PI

    for (int i = 0; i < count; i++) {
      float angle = i * angleStep;
      Vector2 direction = Vector2.Right.Rotated(angle);

      // 假设子弹场景是 SimpleBullet 或其派生类
      var bullet = bulletScene.Instantiate<Bullet.SimpleBullet>();
      bullet.GlobalPosition = GlobalPosition;
      // SimpleBullet 的 _Ready() 函数会根据其旋转和 InitialSpeed 设置速度
      bullet.Rotation = direction.Angle();

      GetTree().Root.AddChild(bullet);
    }
  }
}
