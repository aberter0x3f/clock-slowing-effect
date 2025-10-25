using Bullet;
using Godot;

namespace Enemy;

public partial class Firework : BaseEnemy {
  private enum State {
    Idle,
    Attacking
  }
  // --- 攻击子状态 ---
  private enum AttackSubState {
    None,
    Launching
  }
  private AttackSubState _attackSubState = AttackSubState.None;
  private float _launchTime;
  private float _launchDuration;
  private Vector2 _launchStartPosition;
  private Vector2 _launchTargetPosition;
  private Vector2 _explosionCenter;

  private State _currentState = State.Idle;
  private float _attackCooldown;
  private RandomWalkComponent _randomWalkComponent;
  private readonly RandomNumberGenerator _rnd = new();
  private Node3D _activeProjectileVisualizer = null;

  [ExportGroup("Attack Configuration")]
  [Export]
  public PackedScene FireworkProjectileVisualizerScene { get; set; }
  [Export]
  public PackedScene FallingBulletScene { get; set; }
  [Export]
  public float AttackInterval { get; set; } = 5.0f;
  [Export(PropertyHint.Range, "10, 200, 1")]
  public int ExplosionBulletCount { get; set; } = 40;
  [Export(PropertyHint.Range, "100, 2000, 10")]
  public float ExplosionHeight { get; set; } = 600.0f;
  [Export(PropertyHint.Range, "10, 500, 5")]
  public float LandingSpreadSigma { get; set; } = 100.0f;
  [Export(PropertyHint.Range, "100, 3000, 10")]
  public float ProjectileAcceleration { get; set; } = 2000.0f;

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
    _attackCooldown = (float) _rnd.RandfRange(1.0f, AttackInterval);
  }

  public override void Die() {
    if (IsInstanceValid(_activeProjectileVisualizer)) {
      _activeProjectileVisualizer.QueueFree();
      _activeProjectileVisualizer = null;
    }
    base.Die();
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    switch (_currentState) {
      case State.Idle:
        HandleIdleState(scaledDelta);
        break;
      case State.Attacking:
        Velocity = Vector2.Zero;
        HandleAttackingState(scaledDelta);
        break;
    }
    MoveAndSlide();
  }

  private void HandleIdleState(float scaledDelta) {
    Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;
    _attackCooldown -= scaledDelta;
    if (_attackCooldown <= 0) {
      if (_player != null && IsInstanceValid(_player)) {
        StartAttackSequence();
      }
    }
  }

  private void StartAttackSequence() {
    if (_currentState != State.Idle) return;
    _currentState = State.Attacking;

    if (FireworkProjectileVisualizerScene == null) {
      GD.PrintErr("Firework: FireworkProjectileVisualizerScene is not set!");
      // 如果没有可视化对象，直接爆炸
      Explode();
      return;
    }

    _activeProjectileVisualizer = FireworkProjectileVisualizerScene.Instantiate<Node3D>();
    GetTree().Root.AddChild(_activeProjectileVisualizer);

    _launchStartPosition = GlobalPosition;
    _launchTargetPosition = _player.GlobalPosition;
    float distance = _launchStartPosition.DistanceTo(_launchTargetPosition);
    _launchDuration = Mathf.Sqrt(2 * distance / ProjectileAcceleration);
    _launchTime = 0;
    _attackSubState = AttackSubState.Launching;
    _explosionCenter = _player.GlobalPosition;

    HandleAttackingState(0);
  }

  private void HandleAttackingState(float scaledDelta) {
    if (_attackSubState != AttackSubState.Launching) return;

    _launchTime += scaledDelta;
    float progress = Mathf.Min(1.0f, _launchTime / _launchDuration);

    Vector2 current2DPos = _launchStartPosition.Lerp(_launchTargetPosition, progress);
    float currentHeight = Mathf.Ease(progress, 0.5f) * ExplosionHeight;

    var pos3D = new Vector3(
      current2DPos.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY + currentHeight * GameConstants.WorldScaleFactor,
      current2DPos.Y * GameConstants.WorldScaleFactor
    );
    if (IsInstanceValid(_activeProjectileVisualizer)) {
      _activeProjectileVisualizer.GlobalPosition = pos3D;
    }

    if (progress >= 1.0f) {
      // 飞行结束
      if (IsInstanceValid(_activeProjectileVisualizer)) {
        _activeProjectileVisualizer.QueueFree();
        _activeProjectileVisualizer = null;
      }
      Explode();
    }
  }

  private void Explode() {
    if (FallingBulletScene == null) {
      GD.PrintErr("Firework: FallingBulletScene is not set!");
    } else {
      for (int i = 0; i < ExplosionBulletCount; i++) {
        var bullet = FallingBulletScene.Instantiate<FallingBullet>();
        float offsetX = (float) _rnd.Randfn(0, LandingSpreadSigma);
        float offsetY = (float) _rnd.Randfn(0, LandingSpreadSigma);
        Vector2 landingPosition = _explosionCenter + new Vector2(offsetX, offsetY);
        bullet.GlobalPosition = landingPosition;
        bullet.Initialize(ExplosionHeight);
        GetTree().Root.AddChild(bullet);
      }
    }
    // 切换回冷却阶段
    _attackCooldown = AttackInterval;
    _currentState = State.Idle;
    _attackSubState = AttackSubState.None;
  }
}
