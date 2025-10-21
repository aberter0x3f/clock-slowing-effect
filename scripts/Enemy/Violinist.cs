using Godot;

namespace Enemy;

public partial class Violinist : BaseEnemy {
  // 主攻击间隔
  private const float MAIN_ATTACK_INTERVAL = 3.0f;
  // 音符子弹角度偏移的正态分布标准差（单位：度）
  private const float NOTE_ANGLE_SIGMA_DEGREES = 40.0f;

  [ExportGroup("Attack Configuration")]
  [Export]
  public PackedScene StaffLineBulletScene { get; set; }

  [Export]
  public PackedScene NoteBulletScene { get; set; }

  [Export(PropertyHint.Range, "0.01, 2.0, 0.01")]
  public float StaffLineCreationInterval { get; set; } = 0.05f; // 谱线子弹生成间隔

  [Export(PropertyHint.Range, "0.01, 2.0, 0.01")]
  public float NoteCreationInterval { get; set; } = 0.02f; // 音符子弹发射间隔

  [Export(PropertyHint.Range, "10, 100, 1")]
  public float StaffLineSpacing { get; set; } = 15.0f;

  [Export(PropertyHint.Range, "10, 200, 1")]
  public float StaffBulletSpacing { get; set; } = 20.0f;

  [Export(PropertyHint.Range, "1, 100, 1")]
  public int NoteBulletCount { get; set; } = 50; // 音符子弹数量

  public float NoteBulletAcceleration { get; set; } = 100.0f; // 音符子弹加速度

  // --- 攻击状态 ---
  private float _attackCooldown = MAIN_ATTACK_INTERVAL;
  private bool _isAttacking = false;

  private RandomWalkComponent _randomWalkComponent;

  private readonly RandomNumberGenerator _rnd = new();

  public override void _Ready() {
    base._Ready();

    // 初始化随机行走组件
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");

    // 为了让多个小提琴家的攻击错开，随机化初始冷却时间
    _attackCooldown = (float) _rnd.RandfRange(1.0f, 2 * MAIN_ATTACK_INTERVAL);
  }

  // 使用 _PhysicsProcess 来处理移动和计时，因为它与物理帧同步，更适合移动和碰撞
  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    // 将组件计算出的速度应用到自己的 Velocity 属性上
    // 注意：速度应该受 TimeScale 影响
    Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;

    // --- 攻击冷却逻辑 ---
    // 如果正在攻击，则跳过计时
    if (!_isAttacking) {
      // 攻击冷却计时，受时间缩放影响
      _attackCooldown -= scaledDelta;
      if (_attackCooldown <= 0) {
        _attackCooldown = MAIN_ATTACK_INTERVAL;
        // 使用 CallDeferred 启动异步攻击序列，避免在 _Process 中直接启动复杂逻辑
        CallDeferred(nameof(StartAttackSequence));
      }
    }

    MoveAndSlide();

    if (IsOnWall()) {
      _randomWalkComponent.PickNewMovement();
    }
  }

  /// <summary>
  /// 检查攻击条件并启动攻击序列。
  /// </summary>
  private void StartAttackSequence() {
    // 确保玩家存在且有效
    if (_player == null || !IsInstanceValid(_player)) {
      GD.PrintErr("Violinist: Player not found, cannot attack.");
      return;
    }
    // 异步执行攻击，不会阻塞游戏主循环
    AttackSequence();
  }

  /// <summary>
  /// 完整的攻击序列，包含五线谱和音符两个阶段。
  /// </summary>
  private async void AttackSequence() {
    if (_isAttacking) return;
    _isAttacking = true;

    // --- 阶段 0: 准备 ---
    Vector2 enemyPos = GlobalPosition;
    Vector2 playerPos = _player.GlobalPosition;
    Vector2 direction = (playerPos - enemyPos).Normalized();
    float distanceToPlayer = enemyPos.DistanceTo(playerPos);
    float lineLength = Mathf.Max(500.0f, 1.2f * distanceToPlayer);
    Vector2 perpendicularDir = direction.Rotated(Mathf.Pi / 2.0f);

    // --- 阶段 1: 创建静态的「五线谱」子弹 ---
    if (StaffLineBulletScene != null) {
      for (float dist = 0; dist <= lineLength; dist += StaffBulletSpacing) {
        for (int lineIndex = -2; lineIndex <= 2; lineIndex++) {
          Vector2 lineOffset = perpendicularDir * lineIndex * StaffLineSpacing;
          var staffLineBullet = StaffLineBulletScene.Instantiate<Node2D>();
          staffLineBullet.GlobalPosition = enemyPos + direction * dist + lineOffset;
          staffLineBullet.GlobalRotation = direction.Angle();
          GetTree().Root.AddChild(staffLineBullet);
        }
        await GetTree().CreateTimeScaleTimer(StaffLineCreationInterval);
      }
    } else {
      GD.PrintErr("Violinist: StaffLineBulletScene is not set in the editor.");
    }

    // --- 阶段 2: 从「五线谱」上发射「音符」子弹 ---
    if (NoteBulletScene != null) {
      float sigmaRadians = Mathf.DegToRad(NOTE_ANGLE_SIGMA_DEGREES);

      for (int i = 0; i < NoteBulletCount; i++) {
        int randomLineIndex = _rnd.RandiRange(-2, 2);
        Vector2 randomLineOffset = perpendicularDir * randomLineIndex * StaffLineSpacing;
        float randomDistOnLine = (float) _rnd.RandfRange(0, lineLength);
        Vector2 spawnPos = enemyPos + direction * randomDistOnLine + randomLineOffset;
        bool flipDirection = _rnd.Randf() > 0.5f;
        Vector2 launchPerpendicularDir = direction.Rotated(Mathf.Pi / 2.0f * (flipDirection ? 1.0f : -1.0f));
        float angleOffset = (float) _rnd.Randfn(0, sigmaRadians);
        Vector2 finalDirection = launchPerpendicularDir.Rotated(angleOffset);
        var noteBullet = NoteBulletScene.Instantiate<Bullet.SimpleBullet>();
        noteBullet.GlobalPosition = spawnPos;
        noteBullet.Rotation = finalDirection.Angle();
        noteBullet.InitialSpeed = 1.0f;
        noteBullet.Acceleration = finalDirection.Normalized() * NoteBulletAcceleration;
        noteBullet.MaxSpeed = 300f;
        GetTree().Root.AddChild(noteBullet);
        await GetTree().CreateTimeScaleTimer(NoteCreationInterval);
      }
    } else {
      GD.PrintErr("Violinist: NoteBulletScene is not set in the editor.");
    }

    _isAttacking = false;
  }
}
