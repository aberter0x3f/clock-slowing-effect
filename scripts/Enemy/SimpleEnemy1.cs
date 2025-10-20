using Godot;

namespace Enemy;

public partial class SimpleEnemy1 : BaseEnemy {
  private const float SHOOT_INTERVAL = 1.0f;

  private float _shootTimer = SHOOT_INTERVAL;

  [Export]
  public PackedScene Bullet { get; set; }

  public override void _Process(double delta) {
    base._Process(delta);

    _shootTimer -= TimeManager.Instance.TimeScale * (float) delta;
    if (_shootTimer <= 0) {
      Shoot();
      _shootTimer = SHOOT_INTERVAL;
    }
  }

  private void Shoot() {
    if (_player == null) return;

    var bullet = Bullet.Instantiate<Bullet>();

    bullet.Position = GlobalPosition;
    bullet.Direction = (_player.GlobalPosition - GlobalPosition).Normalized();

    GetTree().Root.AddChild(bullet);
  }
}
