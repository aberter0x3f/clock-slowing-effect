using Godot;

namespace Enemy;

public partial class Violinist : BaseEnemy {
  private const float MAIN_ATTACK_INTERVAL = 3.0f;
  private const float NOTE_ANGLE_SIGMA_DEGREES = 40.0f;

  // --- 攻击状态机 ---
  private enum AttackState {
    Idle,
    CreatingStaff,
    FiringNotes
  }
  private AttackState _attackState = AttackState.Idle;
  private float _attackTimer;
  // 状态变量，用于在帧之间保存攻击信息
  private float _staffCreationDist;
  private int _notesFiredCount;
  private Vector2 _attackDirection;
  private float _attackLineLength;
  private Vector2 _attackPerpendicularDir;
  private Vector2 _attackStartPosition;

  [ExportGroup("Attack Configuration")]
  [Export]
  public PackedScene StaffLineBulletScene { get; set; }
  [Export]
  public PackedScene NoteBulletScene { get; set; }
  [Export(PropertyHint.Range, "0.01, 2.0, 0.01")]
  public float StaffLineCreationInterval { get; set; } = 0.05f;
  [Export(PropertyHint.Range, "0.01, 2.0, 0.01")]
  public float NoteCreationInterval { get; set; } = 0.02f;
  [Export(PropertyHint.Range, "10, 100, 1")]
  public float StaffLineSpacing { get; set; } = 15.0f;
  [Export(PropertyHint.Range, "10, 200, 1")]
  public float StaffBulletSpacing { get; set; } = 20.0f;
  [Export(PropertyHint.Range, "1, 100, 1")]
  public int NoteBulletCount { get; set; } = 50;
  public float NoteBulletAcceleration { get; set; } = 100.0f;

  private float _attackCooldown = MAIN_ATTACK_INTERVAL;
  private bool _isAttacking = false;
  private RandomWalkComponent _randomWalkComponent;
  private readonly RandomNumberGenerator _rnd = new();

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
    _attackCooldown = (float) _rnd.RandfRange(1.0f, 2 * MAIN_ATTACK_INTERVAL);
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    if (!_isAttacking) {
      Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;
      _attackCooldown -= scaledDelta;
      if (_attackCooldown <= 0) {
        _attackCooldown = MAIN_ATTACK_INTERVAL;
        StartAttackSequence();
      }
    } else {
      Velocity = Vector2.Zero;
      HandleAttackState(scaledDelta);
    }
    MoveAndSlide();
  }

  private void StartAttackSequence() {
    if (_player == null || !IsInstanceValid(_player)) {
      GD.PrintErr("Violinist: Player not found, cannot attack.");
      return;
    }
    if (_isAttacking) return;

    _isAttacking = true;
    _attackState = AttackState.CreatingStaff;

    // --- 初始化攻击参数 ---
    _attackStartPosition = GlobalPosition;
    Vector2 playerPos = _player.GlobalPosition;
    _attackDirection = (playerPos - _attackStartPosition).Normalized();
    float distanceToPlayer = _attackStartPosition.DistanceTo(playerPos);
    _attackLineLength = Mathf.Max(500.0f, 1.2f * distanceToPlayer);
    _attackPerpendicularDir = _attackDirection.Rotated(Mathf.Pi / 2.0f);
    _staffCreationDist = 0;
    _attackTimer = 0; // 立即开始
  }

  private void HandleAttackState(float scaledDelta) {
    _attackTimer -= scaledDelta;
    if (_attackTimer > 0) return;

    switch (_attackState) {
      case AttackState.CreatingStaff:
        if (_staffCreationDist > _attackLineLength) {
          // --- 阶段 1 结束，进入阶段 2 ---
          _attackState = AttackState.FiringNotes;
          _notesFiredCount = 0;
          _attackTimer = 0; // 立即开始
          return;
        }
        // --- 执行创建五线谱的逻辑 ---
        for (int lineIndex = -2; lineIndex <= 2; lineIndex++) {
          Vector2 lineOffset = _attackPerpendicularDir * lineIndex * StaffLineSpacing;
          var staffLineBullet = StaffLineBulletScene.Instantiate<Node2D>();
          staffLineBullet.GlobalPosition = _attackStartPosition + _attackDirection * _staffCreationDist + lineOffset;
          staffLineBullet.GlobalRotation = _attackDirection.Angle();
          GetTree().Root.AddChild(staffLineBullet);
        }
        _staffCreationDist += StaffBulletSpacing;
        _attackTimer = StaffLineCreationInterval;
        break;

      case AttackState.FiringNotes:
        long noteCount = 1L * NoteBulletCount * (long) _attackLineLength / 500;
        if (_notesFiredCount >= noteCount) {
          // --- 阶段 2 结束，重置状态 ---
          _isAttacking = false;
          _attackState = AttackState.Idle;
          return;
        }
        // --- 执行发射音符的逻辑 ---
        if (NoteBulletScene != null) {
          float sigmaRadians = Mathf.DegToRad(NOTE_ANGLE_SIGMA_DEGREES);
          int randomLineIndex = _rnd.RandiRange(-2, 2);
          Vector2 randomLineOffset = _attackPerpendicularDir * randomLineIndex * StaffLineSpacing;
          float randomDistOnLine = (float) _rnd.RandfRange(0, _attackLineLength);
          Vector2 spawnPos = _attackStartPosition + _attackDirection * randomDistOnLine + randomLineOffset;
          bool flipDirection = _rnd.Randf() > 0.5f;
          Vector2 launchPerpendicularDir = _attackDirection.Rotated(Mathf.Pi / 2.0f * (flipDirection ? 1.0f : -1.0f));
          float angleOffset = (float) _rnd.Randfn(0, sigmaRadians);
          Vector2 finalDirection = launchPerpendicularDir.Rotated(angleOffset);
          var noteBullet = NoteBulletScene.Instantiate<Bullet.SimpleBullet>();
          noteBullet.GlobalPosition = spawnPos;
          noteBullet.Rotation = finalDirection.Angle();
          noteBullet.InitialSpeed = 1.0f;
          noteBullet.Acceleration = finalDirection.Normalized() * NoteBulletAcceleration;
          noteBullet.MaxSpeed = 300f;
          GetTree().Root.AddChild(noteBullet);
          _notesFiredCount++;
          _attackTimer = NoteCreationInterval;
        } else {
          GD.PrintErr("Violinist: NoteBulletScene is not set in the editor.");
          // 避免死循环
          _isAttacking = false;
          _attackState = AttackState.Idle;
        }
        break;
    }
  }
}
