using System.Threading.Tasks;
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
  private float _currentJumpHeight = 0f; // 用于 3D 跳跃的当前高度

  // --- 场景引用 ---
  private MapGenerator _mapGenerator;
  private CollisionShape2D _bodyCollisionShape;

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
  public float LungeSpeed { get; set; } = 400.0f; // 跳跃的水平速度
  [Export(PropertyHint.Range, "100, 2000, 10")]
  public float LungeDistance { get; set; } = 200.0f; // 每次跳跃的最大水平距离
  [Export(PropertyHint.Range, "50, 500, 10")]
  public float JumpHeight { get; set; } = 150.0f; // 跳跃的 3D 高度
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

    // 获取对 MapGenerator 的引用以进行寻路检查
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr("Drummer: MapGenerator not found! Jump validation will be disabled.");
    }

    // 获取碰撞体引用，以便在跳跃时禁用它们
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
        // 移动逻辑由异步的 AttackSequence 方法处理
        Velocity = Vector2.Zero;
        break;
      case State.Retreating:
        HandleRetreatingState(scaledDelta);
        break;
    }
    MoveAndSlide();
    // 持续更新 3D 可视化对象的位置，包括跳跃高度
    UpdateVisualizer();
  }

  protected override void UpdateVisualizer() {
    // 重写基类方法以加入高度
    var position3D = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );
    // 加上跳跃的垂直位移
    position3D.Y += _currentJumpHeight * GameConstants.WorldScaleFactor;
    _visualizer.GlobalPosition = position3D;
  }

  private void HandleIdleState(float scaledDelta) {
    Velocity = Vector2.Zero;
    _attackCooldown -= scaledDelta;
    if (_attackCooldown <= 0) {
      if (_player != null && IsInstanceValid(_player)) {
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

    Vector2 attackDirection = (_player.GlobalPosition - GlobalPosition).Normalized();

    for (int i = 0; i < 3; i++) {
      // 计算并验证跳跃目标
      Vector2 startPos = GlobalPosition;
      float distanceToPlayer = startPos.DistanceTo(_player.GlobalPosition);
      float actualLungeDistance = Mathf.Min(LungeDistance, distanceToPlayer - PlayerAvoidanceDistance);

      if (actualLungeDistance > 0) {
        Vector2 targetPos = startPos + attackDirection * actualLungeDistance;

        // 验证目标位置是否可通行
        bool canJump = false;
        if (_mapGenerator != null) {
          Vector2I mapCoords = _mapGenerator.WorldToMap(targetPos);
          if (_mapGenerator.IsWalkable(mapCoords)) {
            canJump = true;
          } else {
            GD.Print($"Drummer jump target {targetPos} (grid: {mapCoords}) is not walkable. Skipping jump.");
          }
        } else {
          canJump = true; // 如果找不到地图生成器，则跳过检查
        }

        if (canJump) {
          await PerformJump(targetPos);
        }
      }

      // 敲鼓 (发射子弹)
      if (i < 2) {
        FireBulletCircle(SmallBulletScene, SmallBulletCount);
        await GetTree().CreateTimeScaleTimer(0.1f);
        FireBulletCircle(SmallBulletScene, SmallBulletCount);
        await GetTree().CreateTimeScaleTimer(0.1f);
        FireBulletCircle(SmallBulletScene, SmallBulletCount);
      } else {
        FireBulletCircle(LargeBulletScene, LargeBulletCount);
      }

      await GetTree().CreateTimeScaleTimer(0.4f);
    }

    // 转换到撤退状态
    _retreatDirection = (GlobalPosition - _player.GlobalPosition).Normalized();
    _retreatTimer = RetreatDuration;
    _currentState = State.Retreating;
  }

  private async Task PerformJump(Vector2 targetPosition) {
    SetCollisionsEnabled(false);

    Vector2 startPosition = GlobalPosition;
    float distance = startPosition.DistanceTo(targetPosition);
    float duration = distance / LungeSpeed;
    float time = 0f;

    while (time < duration) {
      // 检查实例是否仍然有效，以防在跳跃中被杀死
      if (!IsInstanceValid(this)) return;

      time += (float) GetProcessDeltaTime() * TimeManager.Instance.TimeScale;
      float progress = Mathf.Min(1.0f, time / duration);

      // 更新 2D 平面位置
      GlobalPosition = startPosition.Lerp(targetPosition, progress);

      // 计算并更新 3D 高度 (使用 sin 曲线模拟抛物线)
      _currentJumpHeight = Mathf.Sin(progress * Mathf.Pi) * JumpHeight;

      // 等待下一物理帧
      await ToSignal(GetTree(), SceneTree.SignalName.PhysicsFrame);
    }

    // 确保最终状态正确
    GlobalPosition = targetPosition;
    _currentJumpHeight = 0f;
    SetCollisionsEnabled(true);
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
