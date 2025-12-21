using Bullet;
using Godot;

namespace Enemy;

public partial class SimpleEnemy2 : SimpleEnemy {
  [Export]
  public PackedScene BulletScene { get; set; }
  [Export]
  public float ShootInterval { get; set; } = 2.5f;
  [Export]
  public int ShootCount { get; set; } = 40;
  [Export]
  public float SafeAngle { get; set; } = 0.25f;

  public override (float, bool) Shoot() {
    var target = PlayerNode;
    if (target == null || !IsInstanceValid(target)) return (0.1f, true);

    SoundManager.Instance.Play(SoundEffect.FireBig);

    var baseDirection = (target.GlobalPosition - GlobalPosition).Normalized();
    for (int i = 0; i < ShootCount; ++i) {
      var rotationAngle = Mathf.Tau / ShootCount * i;
      if (rotationAngle > Mathf.Pi) {
        rotationAngle -= Mathf.Tau;
      }
      if (float.Abs(rotationAngle) < SafeAngle) {
        continue;
      }
      var direction = baseDirection.Rotated(Vector3.Up, rotationAngle);
      var startPos = GlobalPosition;
      var bullet = BulletScene.Instantiate<SimpleBullet>();
      bullet.UpdateFunc = (time) => {
        SimpleBullet.UpdateState state = new();
        state.position = startPos + direction * (time * 2.5f);
        return state;
      };
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }

    return (ShootInterval, true);
  }
}
