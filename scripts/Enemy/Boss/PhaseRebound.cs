using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseReboundState : BasePhaseState {
  public PhaseRebound.AttackState CurrentState;
  public float Timer;
  public Vector3 BossTargetPosition;
  public double AttackCycleStartTime;
  public float EmitterFireTimer;
  public Vector3 PlayerPosition;
}

public partial class PhaseRebound : BasePhase {
  public enum AttackState {
    MovingToStartHeight,
    WaitingToAttack,
    MovingToPlayer,
    Attacking
  }

  private AttackState _currentState;
  private float _timer;
  private Vector3 _bossTargetPosition;
  private double _attackCycleStartTime;
  private float _emitterFireTimer;
  private Vector3 _playerPosition;

  private MapGenerator _mapGenerator;
  private Rect2 _reboundBounds;

  [ExportGroup("Movement")]
  [Export] public float StartHeight { get; set; } = 3.0f;
  [Export] public float MoveToHeightSpeed { get; set; } = 6.0f;
  [Export] public float MoveToPlayerSpeed { get; set; } = 10.0f;

  [ExportGroup("Timing")]
  [Export] public float InitialWaitDuration { get; set; } = 2.0f;
  [Export] public float AttackInterval { get; set; } = 10.0f;
  [Export] public float EmitterLifetime { get; set; } = 3.0f;
  [Export] public float EmitterFireInterval { get; set; } = 0.05f;

  [ExportGroup("Emitter Formula")]
  [Export] public PackedScene BulletScene { get; set; }
  [Export] public int EmitterCount { get; set; } = 4;
  [Export] public float EmitterRingRadius { get; set; } = 0.3f;
  [Export] public float HorizontalRotationTimeFactor { get; set; } = 3f;
  [Export] public float VerticalOscillationTimeFactor { get; set; } = 3f;
  [Export] public float VerticalOscillationHeight { get; set; } = 1f;
  [Export] public float QuadraticCoefficient { get; set; } = 0.05f;

  [ExportGroup("Bullet Properties")]
  [Export] public float BulletSpeed { get; set; } = 1.5f;
  [Export] public int MaxRebounds { get; set; } = 2;
  [Export] public float ReboundBoundsScale { get; set; } = 1.0f;

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");

    var rank = GameManager.Instance.EnemyRank;
    BulletSpeed *= (rank + 5) / 10f;
    AttackInterval /= (rank + 10) / 15f;
    EmitterFireInterval /= (rank + 5) / 10f;

    // 计算反弹边界
    float worldWidth = _mapGenerator.MapWidth * _mapGenerator.TileSize;
    float worldHeight = _mapGenerator.MapHeight * _mapGenerator.TileSize;
    float halfWidth = worldWidth / 2.0f * ReboundBoundsScale;
    float halfHeight = worldHeight / 2.0f * ReboundBoundsScale;
    _reboundBounds = new Rect2(-halfWidth, -halfHeight, halfWidth * 2, halfHeight * 2);

    _currentState = AttackState.MovingToStartHeight;
    _bossTargetPosition = ParentBoss.GlobalPosition with { Y = StartHeight };
    _timer = InitialWaitDuration;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    switch (_currentState) {
      case AttackState.MovingToStartHeight:
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(_bossTargetPosition, MoveToHeightSpeed * scaledDelta);
        if (ParentBoss.GlobalPosition.IsEqualApprox(_bossTargetPosition)) {
          _currentState = AttackState.WaitingToAttack;
          _timer = 0;
        }
        break;

      case AttackState.WaitingToAttack:
        _timer -= scaledDelta;
        if (_timer <= 0) {
          _currentState = AttackState.MovingToPlayer;
          _bossTargetPosition = PlayerNode.GlobalPosition with { Y = StartHeight };
        }
        break;

      case AttackState.MovingToPlayer:
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(_bossTargetPosition, MoveToPlayerSpeed * scaledDelta);
        if (ParentBoss.GlobalPosition.IsEqualApprox(_bossTargetPosition)) {
          _currentState = AttackState.Attacking;
          _attackCycleStartTime = TimeManager.Instance.CurrentGameTime;
          _timer = EmitterLifetime;
          _emitterFireTimer = 0f;
          _playerPosition = PlayerNode.GlobalPosition;
        }
        break;

      case AttackState.Attacking:
        _timer -= scaledDelta;
        _emitterFireTimer -= scaledDelta;
        if (_emitterFireTimer <= 0) {
          FireBulletVolley();
          _emitterFireTimer = EmitterFireInterval;
        }
        if (_timer <= 0) {
          _currentState = AttackState.WaitingToAttack;
          _timer = AttackInterval;
        }
        break;
    }
  }

  private void FireBulletVolley() {
    SoundManager.Instance.Play(SoundEffect.FireSmall);

    double timeSinceAttackStart = TimeManager.Instance.CurrentGameTime - _attackCycleStartTime;
    for (int i = 0; i < EmitterCount; ++i) {
      float theta = i * Mathf.Tau / EmitterCount;

      float horizontalAngle = theta + AngleFunc((float) timeSinceAttackStart * HorizontalRotationTimeFactor);
      var horizontalOffset = new Vector3(Mathf.Cos(horizontalAngle), 0, Mathf.Sin(horizontalAngle)) * EmitterRingRadius;

      float verticalAngle = theta + (float) timeSinceAttackStart * VerticalOscillationTimeFactor;
      var verticalOffset = new Vector3(0, Mathf.Abs(Mathf.Sin(verticalAngle)) * VerticalOscillationHeight, 0);

      var emitterPos = _playerPosition + horizontalOffset + verticalOffset;
      var direction = (emitterPos with { Y = 0 } - _playerPosition with { Y = 0 }).Normalized();

      var bullet = BulletScene.Instantiate<PhaseReboundBullet>();
      bullet.InitializeTrajectory(emitterPos, direction, BulletSpeed, _reboundBounds, MaxRebounds);
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  private float AngleFunc(float x) {
    return Mathf.PosMod(QuadraticCoefficient * x * x + 4 * QuadraticCoefficient * x, Mathf.Tau);
  }

  public override RewindState CaptureInternalState() => new PhaseReboundState {
    CurrentState = _currentState,
    Timer = _timer,
    BossTargetPosition = _bossTargetPosition,
    AttackCycleStartTime = _attackCycleStartTime,
    EmitterFireTimer = _emitterFireTimer,
    PlayerPosition = _playerPosition,
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseReboundState prs) return;
    _currentState = prs.CurrentState;
    _timer = prs.Timer;
    _bossTargetPosition = prs.BossTargetPosition;
    _attackCycleStartTime = prs.AttackCycleStartTime;
    _emitterFireTimer = prs.EmitterFireTimer;
    _playerPosition = prs.PlayerPosition;
  }
}
