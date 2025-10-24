using Bullet;
using Godot;

namespace Enemy;

public partial class Firework : BaseEnemy {
  private enum State {
    Idle,
    Attacking
  }

  private State _currentState = State.Idle;
  private float _attackCooldown;
  private RandomWalkComponent _randomWalkComponent;
  private readonly RandomNumberGenerator _rnd = new();
  private Node3D _activeProjectileVisualizer = null;

  [ExportGroup("Attack Configuration")]
  [Export]
  public PackedScene FireworkProjectileVisualizerScene { get; set; } // A simple 3D scene for the rising projectile
  [Export]
  public PackedScene FallingBulletScene { get; set; } // The FallingBullet scene we just created
  [Export]
  public float AttackInterval { get; set; } = 5.0f;
  [Export(PropertyHint.Range, "10, 200, 1")]
  public int ExplosionBulletCount { get; set; } = 40;
  [Export(PropertyHint.Range, "100, 2000, 10")]
  public float ExplosionHeight { get; set; } = 600.0f; // Height in 2D units where the firework explodes
  [Export(PropertyHint.Range, "10, 500, 5")]
  public float LandingSpreadSigma { get; set; } = 100.0f; // Standard deviation for the bullet landing positions
  [Export(PropertyHint.Range, "100, 3000, 10")]
  public float ProjectileAcceleration { get; set; } = 2000.0f; // How fast the projectile flies upwards

  public override void _Ready() {
    base._Ready();
    _randomWalkComponent = GetNode<RandomWalkComponent>("RandomWalkComponent");
    _attackCooldown = (float) _rnd.RandfRange(1.0f, AttackInterval);
  }

  // 重写 Die 方法以处理清理逻辑
  public override void Die() {
    // 在执行基类的死亡逻辑之前，先清理我们自己的东西
    if (IsInstanceValid(_activeProjectileVisualizer)) {
      _activeProjectileVisualizer.QueueFree();
      _activeProjectileVisualizer = null; // 确保引用被清空
    }
    base.Die(); // 调用父类的 Die 方法
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    switch (_currentState) {
      case State.Idle:
        HandleIdleState(scaledDelta);
        break;
      case State.Attacking:
        // While attacking, the enemy itself doesn't move.
        Velocity = Vector2.Zero;
        break;
    }
    MoveAndSlide();
  }

  private void HandleIdleState(float scaledDelta) {
    Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;
    _attackCooldown -= scaledDelta;
    if (_attackCooldown <= 0) {
      if (_player != null && IsInstanceValid(_player)) {
        AttackSequence();
      }
    }
  }

  private async void AttackSequence() {
    if (_currentState != State.Idle) return;
    _currentState = State.Attacking;

    // --- 1. Launch Phase: A visual-only projectile flies up ---
    if (FireworkProjectileVisualizerScene == null) {
      GD.PrintErr("Firework: FireworkProjectileVisualizerScene is not set!");
      await GetTree().CreateTimeScaleTimer(1.5f);
    } else {
      // 创建实例并将其存入成员变量
      _activeProjectileVisualizer = FireworkProjectileVisualizerScene.Instantiate<Node3D>();
      GetTree().Root.AddChild(_activeProjectileVisualizer);

      Vector2 startPos = GlobalPosition;
      Vector2 targetPos = _player.GlobalPosition;
      float distance = startPos.DistanceTo(targetPos);
      float flightDuration = Mathf.Sqrt(2 * distance / ProjectileAcceleration);

      float time = 0;
      while (time < flightDuration) {
        // 每次循环检查自身是否存活，如果死亡，异步方法会自动停止，Die()方法会负责清理
        if (!IsInstanceValid(this)) return;

        time += (float) GetProcessDeltaTime() * TimeManager.Instance.TimeScale;
        float progress = Mathf.Min(1.0f, time / flightDuration);

        Vector2 current2DPos = startPos.Lerp(targetPos, progress);
        float currentHeight = Mathf.Ease(progress, 0.5f) * ExplosionHeight;

        var pos3D = new Vector3(
          current2DPos.X * GameConstants.WorldScaleFactor,
          GameConstants.GamePlaneY + currentHeight * GameConstants.WorldScaleFactor,
          current2DPos.Y * GameConstants.WorldScaleFactor
        );
        _activeProjectileVisualizer.GlobalPosition = pos3D;

        await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
      }

      // 攻击正常结束，手动清理并清空引用
      if (IsInstanceValid(_activeProjectileVisualizer)) {
        _activeProjectileVisualizer.QueueFree();
      }
      _activeProjectileVisualizer = null;
    }

    // 如果在飞行过程中被打死，就不再继续执行爆炸
    if (!IsInstanceValid(this)) return;

    // --- 2. Explosion Phase: Spawn falling bullets ---
    if (FallingBulletScene == null) {
      GD.PrintErr("Firework: FallingBulletScene is not set!");
    } else {
      Vector2 explosionCenter = _player.GlobalPosition;

      for (int i = 0; i < ExplosionBulletCount; i++) {
        var bullet = FallingBulletScene.Instantiate<FallingBullet>();
        float offsetX = (float) _rnd.Randfn(0, LandingSpreadSigma);
        float offsetY = (float) _rnd.Randfn(0, LandingSpreadSigma);
        Vector2 landingPosition = explosionCenter + new Vector2(offsetX, offsetY);
        bullet.GlobalPosition = landingPosition;
        bullet.Initialize(ExplosionHeight);
        GetTree().Root.AddChild(bullet);
      }
    }

    // --- 3. Cooldown Phase ---
    _attackCooldown = AttackInterval;
    _currentState = State.Idle;
  }
}
