using Godot;
using Rewind;

namespace Enemy;

public class SimpleEnemyState : BaseEnemyState {
  public float ShootTimer;
  public bool CanWalk;
}

public abstract partial class SimpleEnemy : BaseEnemy {
  [Export]
  public float FireWarmUpTime { get; set; } = 1f;

  protected RandomWalkComponent _randomWalkComponent;
  protected float _shootTimer;
  protected bool _canWalk = true;

  public override void _Ready() {
    _randomWalkComponent = GetNodeOrNull<RandomWalkComponent>("RandomWalkComponent");
    _shootTimer = FireWarmUpTime * (float) GD.RandRange(0.5, 2.0);
    base._Ready();
  }

  public override void UpdateEnemy(float scaledDelta, float effectiveTimeScale) {
    _shootTimer -= scaledDelta;
    if (_shootTimer <= 0) {
      var (nextDelay, canWalk) = Shoot();
      _canWalk = canWalk;
      _shootTimer = nextDelay;
    }

    if (_canWalk && IsInstanceValid(_randomWalkComponent)) {
      Velocity = _randomWalkComponent.TargetVelocity * effectiveTimeScale;
    } else {
      Velocity = Vector3.Zero;
    }
  }

  public override void _PhysicsProcess(double delta) {
    if (RewindManager.Instance.IsPreviewing) return;
    if (RewindManager.Instance.IsRewinding) return;
    if (!_canWalk) return;
    MoveAndSlide();
  }

  public abstract (float nextDelay, bool canWalk) Shoot();

  public override RewindState CaptureState() {
    var baseState = (BaseEnemyState) base.CaptureState();
    return new SimpleEnemyState {
      GlobalPosition = baseState.GlobalPosition,
      Velocity = baseState.Velocity,
      Health = baseState.Health,
      HitTimerLeft = baseState.HitTimerLeft,
      IsInHitState = baseState.IsInHitState,
      ShootTimer = this._shootTimer,
      CanWalk = this._canWalk,
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not SimpleEnemyState ses) return;
    this._shootTimer = ses.ShootTimer;
    this._canWalk = ses.CanWalk;
  }
}
