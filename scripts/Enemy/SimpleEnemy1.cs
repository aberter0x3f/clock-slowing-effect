using Bullet;
using Godot;
using Rewind;

namespace Enemy;

public class SimpleEnemy1State : BaseEnemyState {
  public float ShootTimer;
}

public partial class SimpleEnemy1 : BaseEnemy {
  private const float SHOOT_INTERVAL = 1.0f;

  [Export]
  public PackedScene BulletScene { get; set; }

  private float _shootTimer = SHOOT_INTERVAL;
  private RandomWalkComponent _randomWalkComponent;

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
  }

  public override void _Process(double delta) {
    base._Process(delta);
    // GD.Print($"SimpleEnemy1 _Process: {RewindManager.Instance.IsPreviewing} {RewindManager.Instance.IsRewinding}");
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    _shootTimer -= TimeManager.Instance.TimeScale * (float) delta;
    if (_shootTimer <= 0) {
      Shoot();
      _shootTimer = SHOOT_INTERVAL;
    }

    UpdateVisualizer();
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;
    MoveAndSlide();
  }

  private void Shoot() {
    PlayAttackSound();
    var target = PlayerNode;
    if (target == null || !IsInstanceValid(target)) return;
    var bullet = BulletScene.Instantiate<SimpleBullet>();
    var direction = (target.GlobalPosition - GlobalPosition).Normalized();
    bullet.GlobalPosition = GlobalPosition;
    bullet.Velocity = direction * bullet.InitialSpeed;
    bullet.Rotation = direction.Angle();
    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  public override RewindState CaptureState() {
    var baseState = (BaseEnemyState) base.CaptureState();
    return new SimpleEnemy1State {
      GlobalPosition = baseState.GlobalPosition,
      Velocity = baseState.Velocity,
      Health = baseState.Health,
      HitTimerLeft = baseState.HitTimerLeft,
      SpriteModulate = baseState.SpriteModulate,
      ShootTimer = this._shootTimer
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not SimpleEnemy1State ses) return;
    this._shootTimer = ses.ShootTimer;
  }
}
