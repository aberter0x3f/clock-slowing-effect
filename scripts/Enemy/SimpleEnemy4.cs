using Bullet;
using Godot;

namespace Enemy;

public partial class SimpleEnemy4 : SimpleEnemy {
  [Export]
  public PackedScene BulletScene { get; set; }

  [Export]
  public float ShootInterval { get; set; } = 1.5f;

  [Export]
  public float BulletBaseSpeed { get; set; } = 1.5f;

  public override (float nextDelay, bool canWalk) Shoot() {
    var target = PlayerNode;
    if (target == null || !IsInstanceValid(target) || BulletScene == null) return (0.1f, true);

    SoundManager.Instance.Play(SoundEffect.FireBig);

    Vector3 direction = (target.GlobalPosition - GlobalPosition).Normalized();
    Vector3 startPos = GlobalPosition;

    for (int i = 0; i < 15; ++i) {
      var bullet = BulletScene.Instantiate<SimpleBullet>();
      float speed = BulletBaseSpeed + i * 0.2f;

      bullet.UpdateFunc = (time) => {
        SimpleBullet.UpdateState state = new();
        state.position = startPos + direction * (time * speed);
        Vector3 upVector = Vector3.Up;
        if (direction.Cross(upVector).IsZeroApprox()) {
          upVector = Vector3.Forward;
        }
        state.rotation = Basis.LookingAt(direction, upVector).GetEuler();
        return state;
      };

      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }

    return (ShootInterval, true);
  }
}
