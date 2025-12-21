using Bullet;
using Godot;

namespace Enemy;

public partial class SimpleEnemy1 : SimpleEnemy {
  [Export]
  public PackedScene BulletScene { get; set; }

  public override (float, bool) Shoot() {
    var target = PlayerNode;
    if (target == null || !IsInstanceValid(target)) return (0.1f, true);
    SoundManager.Instance.Play(SoundEffect.FireBig);
    var bullet = BulletScene.Instantiate<SimpleBullet>();
    var direction = (target.GlobalPosition - GlobalPosition).Normalized();
    var startPos = GlobalPosition;
    bullet.UpdateFunc = (time) => new SimpleBullet.UpdateState { position = startPos + direction * (time * 2.5f) };
    GameRootProvider.CurrentGameRoot.AddChild(bullet);
    return (1f, true);
  }
}
