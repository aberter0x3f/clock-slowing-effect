using Bullet;
using Godot;

namespace Enemy;

public partial class SimpleEnemy3 : BaseEnemy {

  private float _shootTimer;

  [Export]
  public PackedScene Bullet { get; set; }

  [Export]
  public float ShootInterval { get; set; } = 4f;

  private RandomWalkComponent _randomWalkComponent;
  private bool _isAttacking = false;

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
    _shootTimer = ShootInterval;
  }

  public override void _Process(double delta) {
    base._Process(delta);
    _shootTimer -= TimeManager.Instance.TimeScale * (float) delta;
    if (_shootTimer <= 0) {
      CallDeferred(nameof(AttackSequence));
      _shootTimer = ShootInterval;
    }
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    if (_isAttacking) {
      Velocity = Vector2.Zero;
    } else {
      Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;
      MoveAndSlide();
    }
  }

  private async void AttackSequence() {
    _isAttacking = true;
    var baseDirection = (_player.GlobalPosition - GlobalPosition).Normalized();
    const float len = 10f;

    for (int i = 1; i <= 10; ++i) {
      for (int j = 0; j < i; ++j) {
        for (int k = 0; k < 6; ++k) {
          var direction = baseDirection.Rotated(Mathf.Tau * k / 6);
          var unit = direction.Rotated(Mathf.Pi / 2);
          var startPosition = GlobalPosition - unit * ((i - 1) * len / 2);
          var position = startPosition + unit * (j * len);
          var bullet = Bullet.Instantiate<SimpleBullet>();
          bullet.GlobalPosition = position;
          bullet.Rotation = direction.Angle();
          GetTree().Root.AddChild(bullet);
        }
      }
      await GetTree().CreateTimeScaleTimer(0.1f);
    }

    _isAttacking = false;
  }
}
