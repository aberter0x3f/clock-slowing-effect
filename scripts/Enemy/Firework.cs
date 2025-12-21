using Bullet;
using Godot;

namespace Enemy;

public partial class Firework : SimpleEnemy {
  [ExportGroup("Attack Configuration")]
  [Export] public PackedScene BulletScene { get; set; }
  [Export] public float ShootInterval { get; set; } = 5.0f;
  [Export] public int ExplosionBulletCount { get; set; } = 40;
  [Export] public float ExplosionHeight { get; set; } = 6.0f;

  private readonly RandomNumberGenerator _rnd = new();

  public override (float nextDelay, bool canWalk) Shoot() {
    var target = PlayerNode;
    if (target == null || BulletScene == null) return (0.1f, true);

    SoundManager.Instance.Play(SoundEffect.FireBig);

    Vector3 explodePos = target.GlobalPosition with { Y = ExplosionHeight };

    for (int i = 0; i < ExplosionBulletCount; ++i) {
      var bullet = BulletScene.Instantiate<SimpleBullet>();

      float angle = _rnd.Randf() * Mathf.Tau;
      float verticalSpread = _rnd.RandfRange(-1f, -5f);
      Vector3 spreadDir = new Vector3(Mathf.Cos(angle), verticalSpread, Mathf.Sin(angle)).Normalized();

      float speed = _rnd.RandfRange(2.0f, 5.0f);
      float gravity = 4.0f;
      var initialOffset = (spreadDir with { Y = 0 }).Normalized() * -1 * GD.Randf();

      bullet.UpdateFunc = (t) => {
        SimpleBullet.UpdateState s = new();

        // p = p0 + v*t + 0.5*a*t^2
        Vector3 currentOffset = (spreadDir * speed * t);
        currentOffset.Y -= 0.5f * gravity * t * t;

        s.position = explodePos + initialOffset + currentOffset;

        if (s.position.Y < 0.5) {
          s.destroy = true;
        }

        return s;
      };

      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }

    return (ShootInterval, true);
  }
}
