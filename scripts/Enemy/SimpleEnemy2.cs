using Bullet;
using Godot;
using Rewind;

namespace Enemy;

public class SimpleEnemy2State : BaseEnemyState {
  public float ShootTimer;
}

public partial class SimpleEnemy2 : BaseEnemy {
  private float _shootTimer;

  [Export]
  public PackedScene BulletScene { get; set; }
  [Export]
  public float ShootInterval { get; set; } = 2.5f;
  [Export]
  public int ShootCount { get; set; } = 40;
  [Export]
  public float SafeAngle { get; set; } = 0.25f;

  private RandomWalkComponent _randomWalkComponent;

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
    _shootTimer = ShootInterval;
  }

  public override void _Process(double delta) {
    base._Process(delta);
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    _shootTimer -= TimeManager.Instance.TimeScale * (float) delta;
    if (_shootTimer <= 0) {
      Shoot();
      _shootTimer = ShootInterval;
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
    var target = PlayerNode;
    if (target == null || !IsInstanceValid(target)) return;

    PlayAttackSound();

    var baseDirection = (target.GlobalPosition - GlobalPosition).Normalized();
    for (int i = 0; i < ShootCount; ++i) {
      var rotationAngle = Mathf.Tau / ShootCount * i;
      if (rotationAngle > Mathf.Pi) {
        rotationAngle -= Mathf.Tau;
      }
      if (float.Abs(rotationAngle) < SafeAngle) {
        continue;
      }
      var dir = baseDirection.Rotated(rotationAngle);
      var bullet = BulletScene.Instantiate<SimpleBullet>();
      bullet.GlobalPosition = GlobalPosition;
      bullet.Velocity = dir * bullet.InitialSpeed;
      bullet.Rotation = dir.Angle();
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  public override RewindState CaptureState() {
    var baseState = (BaseEnemyState) base.CaptureState();
    return new SimpleEnemy2State {
      GlobalPosition = baseState.GlobalPosition,
      Velocity = baseState.Velocity,
      Health = baseState.Health,
      HitTimerLeft = baseState.HitTimerLeft,
      IsInHitState = baseState.IsInHitState,
      ShootTimer = this._shootTimer
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not SimpleEnemy2State ses) return;
    this._shootTimer = ses.ShootTimer;
  }
}
