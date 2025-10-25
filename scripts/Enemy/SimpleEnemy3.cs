using Bullet;
using Godot;

namespace Enemy;

public partial class SimpleEnemy3 : BaseEnemy {
  // 攻击状态机
  private float _attackTimer;
  private int _attackOuterLoopCounter;
  private Vector2 _attackBaseDirection;

  [Export]
  public PackedScene Bullet { get; set; }

  [Export]
  public float ShootInterval { get; set; } = 4f;

  private RandomWalkComponent _randomWalkComponent;
  private bool _isAttacking = false;
  private float _shootTimer;

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
    _shootTimer = ShootInterval;
  }

  public override void _Process(double delta) {
    base._Process(delta);
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    if (!_isAttacking) {
      _shootTimer -= scaledDelta;
      if (_shootTimer <= 0) {
        StartAttackSequence();
        _shootTimer = ShootInterval;
      }
    } else {
      HandleAttackState(scaledDelta);
    }
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);
    if (_isAttacking) {
      Velocity = Vector2.Zero;
    } else {
      Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;
      MoveAndSlide();
    }
  }

  private void StartAttackSequence() {
    _isAttacking = true;
    _attackOuterLoopCounter = 1;
    _attackTimer = 0; // 立即开始
    _attackBaseDirection = (_player.GlobalPosition - GlobalPosition).Normalized();
  }

  private void HandleAttackState(float scaledDelta) {
    _attackTimer -= scaledDelta;
    if (_attackTimer > 0) return;

    // 检查攻击是否完成
    if (_attackOuterLoopCounter > 10) {
      _isAttacking = false;
      return;
    }

    const float len = 10f;
    int i = _attackOuterLoopCounter;

    for (int j = 0; j < i; ++j) {
      for (int k = 0; k < 6; ++k) {
        var direction = _attackBaseDirection.Rotated(Mathf.Tau * k / 6);
        var unit = direction.Rotated(Mathf.Pi / 2);
        var startPosition = GlobalPosition - unit * ((i - 1) * len / 2);
        var position = startPosition + unit * (j * len);
        var bullet = Bullet.Instantiate<SimpleBullet>();
        bullet.GlobalPosition = position;
        bullet.Rotation = direction.Angle();
        GetTree().Root.AddChild(bullet);
      }
    }

    _attackOuterLoopCounter++;
    _attackTimer = 0.1f; // 重置计时器
  }
}
