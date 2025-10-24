using Bullet;
using Godot;

namespace Enemy;

public partial class SimpleEnemy1 : BaseEnemy {
  private const float SHOOT_INTERVAL = 1.0f;

  [Export]
  public PackedScene Bullet { get; set; }

  private float _shootTimer = SHOOT_INTERVAL;
  private RandomWalkComponent _randomWalkComponent;

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
  }

  public override void _Process(double delta) {
    base._Process(delta);
    _shootTimer -= TimeManager.Instance.TimeScale * (float) delta;
    if (_shootTimer <= 0) {
      Shoot();
      _shootTimer = SHOOT_INTERVAL;
    }
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;
    Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;

    MoveAndSlide();
  }

  private void Shoot() {
    if (_player == null) return;

    var bullet = Bullet.Instantiate<SimpleBullet>();

    var direction = (_player.GlobalPosition - GlobalPosition).Normalized();

    bullet.GlobalPosition = GlobalPosition;
    bullet.Velocity = direction * bullet.InitialSpeed;
    bullet.Rotation = direction.Angle();

    GetTree().Root.AddChild(bullet);
  }
}
