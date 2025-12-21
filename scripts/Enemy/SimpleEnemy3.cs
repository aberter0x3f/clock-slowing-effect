using Bullet;
using Godot;
using Rewind;

namespace Enemy;

public class SimpleEnemy3State : SimpleEnemyState {
  public int AttackOuterLoopCounter;
  public Vector2 AttackBaseDirection;
}

public partial class SimpleEnemy3 : SimpleEnemy {
  [Export] public PackedScene BulletScene { get; set; }
  [Export] public float ShootInterval { get; set; } = 3f;

  private int _attackOuterLoopCounter;
  private Vector2 _attackBaseDirection;

  public override (float nextDelay, bool canWalk) Shoot() {
    var target = PlayerNode;
    if (target == null || !IsInstanceValid(target)) return (0.1f, true);

    if (_attackOuterLoopCounter == 1) {
      var dir = (target.GlobalPosition - GlobalPosition);
      _attackBaseDirection = new Vector2(dir.X, dir.Z).Normalized();
      SoundManager.Instance.Play(SoundEffect.FireBig);
    } else {
      SoundManager.Instance.Play(SoundEffect.FireSmall);
    }

    const float len = 0.1f;
    int i = _attackOuterLoopCounter;
    for (int k = 0; k < 6; ++k) {
      var direction2d = _attackBaseDirection.Rotated(Mathf.Tau * k / 6);
      var direction = new Vector3(direction2d.X, 0, direction2d.Y);
      var unit = direction.Rotated(Vector3.Up, Mathf.Pi / 2);
      var startPosition = GlobalPosition - unit * ((i - 1) * len / 2);

      for (int j = 0; j < i; ++j) {
        var position = startPosition + unit * (j * len);
        var bullet = BulletScene.Instantiate<SimpleBullet>();
        bullet.UpdateFunc = (time) => {
          SimpleBullet.UpdateState state = new();
          state.position = position + direction * (time * 1.7f);
          state.rotation = Basis.LookingAt(direction).GetEuler();
          return state;
        };
        GameRootProvider.CurrentGameRoot.AddChild(bullet);
      }
    }

    ++_attackOuterLoopCounter;

    if (_attackOuterLoopCounter > 10) {
      _attackOuterLoopCounter = 1;
      return (ShootInterval, true);
    }

    return (0.1f, false);
  }

  public override RewindState CaptureState() {
    var baseState = (SimpleEnemyState) base.CaptureState();

    return new SimpleEnemy3State {
      GlobalPosition = baseState.GlobalPosition,
      Velocity = baseState.Velocity,
      Health = baseState.Health,
      HitTimerLeft = baseState.HitTimerLeft,
      IsInHitState = baseState.IsInHitState,
      ShootTimer = baseState.ShootTimer,
      CanWalk = baseState.CanWalk,
      AttackOuterLoopCounter = _attackOuterLoopCounter,
      AttackBaseDirection = _attackBaseDirection
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not SimpleEnemy3State ses) return;

    _attackOuterLoopCounter = ses.AttackOuterLoopCounter;
    _attackBaseDirection = ses.AttackBaseDirection;
  }
}
