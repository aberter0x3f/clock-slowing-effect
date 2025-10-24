// ./Enemy/Dna.cs
using Bullet;
using Godot;

namespace Enemy;

public partial class Dna : BaseEnemy {
  [Export]
  public PackedScene Bullet1 { get; set; }

  [Export]
  public PackedScene Bullet2 { get; set; }

  [Export]
  public float ShootInterval { get; set; } = 5f;

  [Export]
  public int BulletCount { get; set; } = 20;

  [Export]
  public float BulletCreationInterval { get; set; } = 0.08f;

  private float _shootTimer;
  private RandomWalkComponent _randomWalkComponent;

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
    _shootTimer = 1.0f;
  }

  public override void _Process(double delta) {
    base._Process(delta);
    _shootTimer -= TimeManager.Instance.TimeScale * (float) delta;
    if (_shootTimer <= 0) {
      CallDeferred(nameof(AttackSequence));
      _shootTimer = ShootInterval;
    }
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;
    Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;

    MoveAndSlide();
  }

  private async void AttackSequence() {
    Vector2 direction = (_player.GlobalPosition - GlobalPosition).Normalized();
    var enmeyPosition = GlobalPosition;

    for (int i = 0; i < BulletCount; i++) {
      var bullet1 = Bullet1.Instantiate<WavyBullet>();
      bullet1.GlobalPosition = enmeyPosition;
      bullet1.GlobalRotation = direction.Angle();
      bullet1.InvertSine = false;
      GetTree().Root.AddChild(bullet1);

      var bullet2 = Bullet2.Instantiate<WavyBullet>();
      bullet2.GlobalPosition = enmeyPosition;
      bullet2.GlobalRotation = direction.Angle();
      bullet2.InvertSine = true;
      GetTree().Root.AddChild(bullet2);

      await GetTree().CreateTimeScaleTimer(BulletCreationInterval);
    }
  }
}
