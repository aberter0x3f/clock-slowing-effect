using Godot;
using Rewind;

namespace Bullet;

public partial class PhaseStellarJetBullet : SimpleBullet3D {
  [ExportGroup("Detonation")]
  [Export]
  public float MaxZHeight { get; set; } = 200f;
  [Export]
  public PackedScene RelativisticBulletScene { get; set; }
  [Export]
  public int RelativisticBulletCount { get; set; } = 12;

  public override void _Process(double delta) {
    // 调用基类 _Process 来处理回溯检查和移动
    base._Process(delta);

    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    if (RawPosition.Z >= MaxZHeight) {
      Detonate();
    }
  }

  private void Detonate() {
    if (RelativisticBulletScene == null) {
      GD.PrintErr("PhaseStellarJetBullet: RelativisticBulletScene is not set!");
      Destroy();
      return;
    }

    var player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    if (!IsInstanceValid(player)) {
      Destroy();
      return;
    }

    var playerTargetPos3D = new Vector3(player.GlobalPosition.X, player.GlobalPosition.Y, 0);
    var direction = (playerTargetPos3D - RawPosition).Normalized();

    for (int i = 0; i < RelativisticBulletCount; ++i) {
      var bullet = RelativisticBulletScene.Instantiate<SimpleBullet3D>();
      bullet.RawPosition = RawPosition + new Vector3((float) GD.Randfn(0, 30), (float) GD.Randfn(0, 30), (float) GD.Randfn(0, 30));
      bullet.Velocity = direction;
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }

    Destroy();
  }
}
