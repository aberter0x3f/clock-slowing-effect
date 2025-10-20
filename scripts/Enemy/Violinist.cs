using Godot;

namespace Enemy;

public partial class Violinist : BaseEnemy {
  // 主攻击间隔
  private const float MAIN_ATTACK_INTERVAL = 5.0f;
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

  /// <summary>
  /// 五线谱中，每条谱线之间的垂直距离。
  /// </summary>
  [Export(PropertyHint.Range, "10, 100, 1")]
  public float StaffLineSpacing { get; set; } = 15.0f;

  /// <summary>
  /// 在单条谱线上，构成谱线的子弹之间的距离。
  /// </summary>
  [Export(PropertyHint.Range, "10, 200, 1")]
  public float StaffBulletSpacing { get; set; } = 20.0f;

  [Export(PropertyHint.Range, "1, 100, 1")]
  public int NoteBulletCount { get; set; } = 50; // 音符子弹数量

  public float NoteBulletAcceleration { get; set; } = 100.0f; // 音符子弹加速度

  // --- 内部状态 ---
  private float _attackCooldown = MAIN_ATTACK_INTERVAL;
  private bool _isAttacking = false;
  private readonly RandomNumberGenerator _rnd = new();

  public override void _Ready() {
    base._Ready();
    // 为了让多个小提琴家的攻击错开，随机化初始冷却时间
    _attackCooldown = (float) _rnd.RandfRange(1.0f, MAIN_ATTACK_INTERVAL);
  }

  public override void _Process(double delta) {
    // 如果正在攻击，则跳过计时
    if (_isAttacking) {
      return;
    }

    // 攻击冷却计时，受时间缩放影响
    _attackCooldown -= (float) delta * TimeManager.Instance.TimeScale;
    if (_attackCooldown <= 0) {
      _attackCooldown = MAIN_ATTACK_INTERVAL;
      // 使用 CallDeferred 启动异步攻击序列，避免在 _Process 中直接启动复杂逻辑
      CallDeferred(nameof(StartAttackSequence));
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
        // 循环创建五条谱线。lineIndex 从 -2 到 2，代表五条线相对于中心线的位置。
        // lineIndex = 0 是中心线
        // lineIndex = -1, -2 是上方的两条线
        // lineIndex = 1, 2 是下方的两条线
        for (int lineIndex = -2; lineIndex <= 2; lineIndex++) {
          // 计算当前谱线相对于中心线的偏移向量
          Vector2 lineOffset = perpendicularDir * lineIndex * StaffLineSpacing;

          // 沿谱线方向，逐个生成构成谱线的子弹
          var staffLineBullet = StaffLineBulletScene.Instantiate<Node2D>();
          // 子弹位置 = 敌人位置 + 沿谱线方向的距离 + 当前谱线的偏移
          staffLineBullet.GlobalPosition = enemyPos + direction * dist + lineOffset;
          staffLineBullet.GlobalRotation = direction.Angle();
          GetTree().Root.AddChild(staffLineBullet);
        }
        await ToSignal(GetTree().CreateTimer(StaffLineCreationInterval, processInPhysics: false, ignoreTimeScale: false), "timeout");
      }
    } else {
      GD.PrintErr("Violinist: StaffLineBulletScene is not set in the editor.");
    }

    // --- 阶段 2: 从「五线谱」上发射「音符」子弹 ---
    if (NoteBulletScene != null) {
      float sigmaRadians = Mathf.DegToRad(NOTE_ANGLE_SIGMA_DEGREES);

      for (int i = 0; i < NoteBulletCount; i++) {
        // 1. 修改：随机选择五条谱线中的一条
        int randomLineIndex = _rnd.RandiRange(-2, 2);
        Vector2 randomLineOffset = perpendicularDir * randomLineIndex * StaffLineSpacing;

        // 2. 在选定的谱线上随机选择一个点
        float randomDistOnLine = (float) _rnd.RandfRange(0, lineLength);
        Vector2 spawnPos = enemyPos + direction * randomDistOnLine + randomLineOffset;

        // 3. 随机从两个垂直方向里选择一个
        bool flipDirection = _rnd.Randf() > 0.5f;
        Vector2 launchPerpendicularDir = direction.Rotated(Mathf.Pi / 2.0f * (flipDirection ? 1.0f : -1.0f));

        // 4. 以这个方向偏移一个正态分布的角度
        float angleOffset = (float) _rnd.Randfn(0, sigmaRadians);
        Vector2 finalDirection = launchPerpendicularDir.Rotated(angleOffset);

        // 5. 创建「音符」并发射
        var noteBullet = NoteBulletScene.Instantiate<Bullet.SimpleBullet>();
        noteBullet.GlobalPosition = spawnPos;
        noteBullet.Rotation = finalDirection.Angle();
        noteBullet.InitialSpeed = 1.0f;
        noteBullet.Acceleration = finalDirection.Normalized() * NoteBulletAcceleration;
        noteBullet.MaxSpeed = 300f;

        GetTree().Root.AddChild(noteBullet);

        // 等待 t_2 时间
        await ToSignal(GetTree().CreateTimer(NoteCreationInterval, processInPhysics: false, ignoreTimeScale: false), "timeout");
      }
    } else {
      GD.PrintErr("Violinist: NoteBulletScene is not set in the editor.");
    }

    _isAttacking = false;
  }
}
