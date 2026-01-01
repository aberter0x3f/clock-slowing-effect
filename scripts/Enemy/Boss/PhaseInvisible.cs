using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseInvisibleState : BasePhaseState {
  public PhaseInvisible.State CurrentState;
  public float Timer;
  public float MovementT;
  public float DecoyFireTimer;
  public float DecoyRotationInternalTime;
}

public partial class PhaseInvisible : BasePhase {
  public enum State { Waiting, Fighting }

  private State _currentState = State.Waiting;
  private float _timer;
  private float _movementT = 0;
  private float _decoyFireTimer;
  private float _decoyRotationInternalTime;

  [ExportGroup("Phase Timing")]
  [Export] public float WaitDuration { get; set; } = 2.0f;
  [Export] public float BossFireInterval { get; set; } = 0.2f;
  [Export] public int BossFireCount { get; set; } = 6;

  [ExportGroup("Boss Movement")]
  [Export] public float InfinitySpeed { get; set; } = 0.5f;
  [Export] public float AmplitudeX { get; set; } = 3f;
  [Export] public float AmplitudeZ { get; set; } = 1.5f;
  [Export] public float BowlCurvature { get; set; } = 0.1f;

  [ExportGroup("Decoy Behavior")]
  [Export] public PackedScene DecoyBulletBaseScene { get; set; }
  [Export] public PackedScene DecoyProjectileScene { get; set; }
  [Export] public float DecoyFireInterval { get; set; } = 0.06f;
  [Export] public int DecoyFireCount { get; set; } = 4;
  [Export] public float DecoyQuadraticK { get; set; } = 3f;
  [Export] public float DecoyProjectileSpeed { get; set; } = 2.5f;
  [Export] public float DecoyProjectileGravity { get; set; } = 6f;

  [ExportGroup("Boss Attacks")]
  [Export] public PackedScene InvisibleBulletScene { get; set; }
  [Export] public float InvisibleMinSpeed { get; set; } = 2.5f;
  [Export] public float InvisibleMaxSpeed { get; set; } = 5.0f;

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _timer = WaitDuration;

    var rank = GameManager.Instance.EnemyRank;
    BossFireInterval /= (rank + 5) / 10f;
    DecoyFireInterval /= (rank + 5) / 10f;
    DecoyProjectileSpeed *= (rank + 5) / 10f;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    switch (_currentState) {
      case State.Waiting:
        _timer -= scaledDelta;
        if (_timer <= 0) {
          _currentState = State.Fighting;
          _timer = BossFireInterval;
          _decoyFireTimer = DecoyFireInterval;
          var decoyBullet = DecoyBulletBaseScene.Instantiate<SimpleBullet>();
          decoyBullet.UpdateFunc = (t) => new SimpleBullet.UpdateState { position = CalculatePosition(-_movementT) };
          GameRootProvider.CurrentGameRoot.AddChild(decoyBullet);
        }
        break;

      case State.Fighting:
        UpdateSystemMovement(scaledDelta);
        UpdateAttacks(scaledDelta);
        break;
    }
  }

  private void UpdateSystemMovement(float scaledDelta) {
    _movementT += InfinitySpeed * scaledDelta;
    _movementT %= Mathf.Tau;

    // 更新 Boss 位置
    ParentBoss.GlobalPosition = CalculatePosition(_movementT);
  }

  private Vector3 CalculatePosition(float t) {
    t += Mathf.Pi / 2;
    float x = AmplitudeX * Mathf.Cos(t);
    float z = AmplitudeZ * Mathf.Sin(2.0f * t);
    float y = BowlCurvature * (x * x + z * z);
    return new Vector3(x, y, z);
  }

  private void UpdateAttacks(float scaledDelta) {
    // Boss 攻击：隐形弹
    _timer -= scaledDelta;
    if (_timer <= 0) {
      for (int i = 0; i < BossFireCount; ++i) {
        SpawnInvisible();
      }
      _timer = BossFireInterval;
    }

    // 分身攻击：旋转弹幕
    _decoyFireTimer -= scaledDelta;
    _decoyRotationInternalTime += scaledDelta;

    if (_decoyFireTimer <= 0) {
      Vector3 spawnPos = CalculatePosition(-_movementT);
      for (int i = 0; i < DecoyFireCount; ++i) {
        float ang = i * Mathf.Tau / DecoyFireCount;
        SpawnDecoyProjectile(spawnPos, ang);
      }
      _decoyFireTimer = DecoyFireInterval;
    }
  }

  private void SpawnInvisible() {
    var bullet = InvisibleBulletScene.Instantiate<PhaseInvisibleBullet>();
    bullet.InitialPosition = ParentBoss.GlobalPosition;

    float angle = GD.Randf() * Mathf.Tau;
    float speed = (float) GD.RandRange(InvisibleMinSpeed, InvisibleMaxSpeed);
    bullet.InitialVelocity = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * speed;

    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  private void SpawnDecoyProjectile(Vector3 spawnPos, float offsetAngle) {
    float t = _decoyRotationInternalTime;
    float angle = DecoyQuadraticK * t * t;
    Vector3 direction = new Vector3(Mathf.Cos(angle + offsetAngle), 0, Mathf.Sin(angle + offsetAngle));

    var bullet = DecoyProjectileScene.Instantiate<SimpleBullet>();
    float gravity = DecoyProjectileGravity;
    float speed = DecoyProjectileSpeed;

    bullet.UpdateFunc = (elapsed) => {
      SimpleBullet.UpdateState s = new();
      float currentY = Mathf.Max(0, spawnPos.Y - 0.5f * gravity * elapsed * elapsed);
      s.position = spawnPos + direction * (speed * elapsed);
      s.position.Y = currentY;
      return s;
    };

    GameRootProvider.CurrentGameRoot.AddChild(bullet);
    SoundManager.Instance.Play(SoundEffect.FireSmall);
  }

  public override RewindState CaptureInternalState() => new PhaseInvisibleState {
    CurrentState = _currentState,
    Timer = _timer,
    MovementT = _movementT,
    DecoyFireTimer = _decoyFireTimer,
    DecoyRotationInternalTime = _decoyRotationInternalTime
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseInvisibleState s) return;
    _currentState = s.CurrentState;
    _timer = s.Timer;
    _movementT = s.MovementT;
    _decoyFireTimer = s.DecoyFireTimer;
    _decoyRotationInternalTime = s.DecoyRotationInternalTime;
  }
}
