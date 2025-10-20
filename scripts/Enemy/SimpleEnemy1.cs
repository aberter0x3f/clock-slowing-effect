using Bullet;
using Godot;

namespace Enemy;

public partial class SimpleEnemy1 : BaseEnemy {
  private const float SHOOT_INTERVAL = 1.0f;

  private float _shootTimer = SHOOT_INTERVAL;

  [Export]
  public PackedScene Bullet { get; set; }

  public override void _Process(double delta) {

    _shootTimer -= TimeManager.Instance.TimeScale * (float) delta;
    if (_shootTimer <= 0) {
      Shoot();
      _shootTimer = SHOOT_INTERVAL;
    }
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
