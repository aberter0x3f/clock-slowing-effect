using Bullet;
using Godot;
using Rewind;

namespace Enemy;

public class DnaState : SimpleEnemyState {
  public int BulletsFired;
  public Vector3 AttackDirection;
}

public partial class Dna : SimpleEnemy {
  [Export] public PackedScene Bullet1Scene { get; set; }
  [Export] public PackedScene Bullet2Scene { get; set; }
  [Export] public float ShootInterval { get; set; } = 5f;
  [Export] public int BulletCount { get; set; } = 20;
  [Export] public float BulletCreationInterval { get; set; } = 0.08f;

  private int _bulletsFired = 0;
  private Vector3 _attackDirection;

  public override (float nextDelay, bool canWalk) Shoot() {
    var target = PlayerNode;
    if (target == null || !IsInstanceValid(target)) return (0.1f, true);

    if (_bulletsFired == 0) {
      _attackDirection = (target.GlobalPosition - GlobalPosition).Normalized();
      GD.Print(_attackDirection);
    }

    SoundManager.Instance.Play(SoundEffect.FireSmall);
    SpawnWavyPair();
    ++_bulletsFired;

    if (_bulletsFired >= BulletCount) {
      _bulletsFired = 0;
      return (ShootInterval, true); // 序列结束，进入大冷却
    }

    return (BulletCreationInterval, false); // 序列中，禁止移动并快速触发下一次 Shoot
  }

  private void SpawnWavyPair() {
    Vector3 pos = GlobalPosition;

    void SetupBullet(PackedScene scene, bool invert) {
      if (scene == null) return;
      var bullet = scene.Instantiate<WavyBullet>();
      bullet.InitialPosition = pos;
      bullet.Basis = Basis.LookingAt(_attackDirection);
      bullet.InvertSine = invert;
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }

    SetupBullet(Bullet1Scene, false);
    SetupBullet(Bullet2Scene, true);
  }

  public override RewindState CaptureState() {
    var baseState = (SimpleEnemyState) base.CaptureState();
    return new DnaState {
      GlobalPosition = baseState.GlobalPosition,
      Velocity = baseState.Velocity,
      Health = baseState.Health,
      HitTimerLeft = baseState.HitTimerLeft,
      IsInHitState = baseState.IsInHitState,
      ShootTimer = baseState.ShootTimer,
      CanWalk = baseState.CanWalk,
      BulletsFired = _bulletsFired,
      AttackDirection = _attackDirection
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not DnaState ds) return;
    _bulletsFired = ds.BulletsFired;
    _attackDirection = ds.AttackDirection;
  }
}
