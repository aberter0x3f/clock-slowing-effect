using Bullet;
using Godot;

namespace Enemy;

public partial class SimpleEnemy4 : BaseEnemy {

  private float _shootTimer;

  [Export]
  public PackedScene Bullet { get; set; }

  [Export]
  public float ShootInterval { get; set; } = 1.5f;

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
  }

  private void Shoot() {
    var direction = (_player.GlobalPosition - GlobalPosition).Normalized();

    for (int i = 0; i < 15; ++i) {
      var bullet = Bullet.Instantiate<SimpleBullet>();
      bullet.GlobalPosition = GlobalPosition;
      bullet.InitialSpeed += i * 20;
      bullet.Rotation = direction.Angle();
      GetTree().Root.AddChild(bullet);
    }
  }
}
