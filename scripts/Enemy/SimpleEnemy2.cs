using Bullet;
using Godot;

namespace Enemy;

public partial class SimpleEnemy2 : BaseEnemy {

  private float _shootTimer;

  [Export]
  public PackedScene Bullet { get; set; }

  [Export]
  public float ShootInterval { get; set; } = 2.5f;

  [Export]
  public int ShootCount { get; set; } = 40;

  [Export]
  public float SafeAngle { get; set; } = 0.25f;

  private RandomWalkComponent _randomWalkComponent;

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
    _shootTimer = ShootInterval;
  }

  public override void _Process(double delta) {
    base._Process(delta);
    _shootTimer -= TimeManager.Instance.TimeScale * (float) delta;
    if (_shootTimer <= 0) {
      Shoot();
      _shootTimer = ShootInterval;
    }
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;
    Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;

    MoveAndSlide();

    if (IsOnWall()) {
      _randomWalkComponent.PickNewMovement();
    }
  }

  private void Shoot() {
    if (_player == null) return;

    var baseDirection = (_player.GlobalPosition - GlobalPosition).Normalized();

    for (int i = 0; i < ShootCount; ++i) {
      var rotationAngle = Mathf.Tau / ShootCount * i;
      if (rotationAngle > Mathf.Pi) {
        rotationAngle -= Mathf.Tau;
      }
      if (float.Abs(rotationAngle) < SafeAngle) {
        continue;
      }

      var dir = baseDirection.Rotated(rotationAngle);

      var bullet = Bullet.Instantiate<SimpleBullet>();
      bullet.GlobalPosition = GlobalPosition;
      bullet.Velocity = dir * bullet.InitialSpeed;
      bullet.Rotation = dir.Angle();
      GetTree().Root.AddChild(bullet);
    }
  }
}
