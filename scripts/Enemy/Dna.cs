using Bullet;
using Godot;
using Rewind;

namespace Enemy;

public class DnaState : BaseEnemyState {
  public Dna.AttackState CurrentAttackState;
  public float AttackTimer;
  public int BulletsFiredInSequence;
  public Vector2 AttackDirection;
  public float ShootTimer;
}

public partial class Dna : BaseEnemy {
  // 攻击状态机
  public enum AttackState {
    Idle,
    Attacking
  }
  private AttackState _attackState = AttackState.Idle;
  private float _attackTimer;
  private int _bulletsFiredInSequence;
  private Vector2 _attackDirection;

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
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    if (_attackState == AttackState.Idle) {
      _shootTimer -= scaledDelta;
      if (_shootTimer <= 0) {
        StartAttackSequence();
        _shootTimer = ShootInterval;
      }
    } else {
      // 如果正在攻击，则处理攻击状态机
      HandleAttackState(scaledDelta);
    }
    UpdateVisualizer();
  }

  public override void _PhysicsProcess(double delta) {
    base._PhysicsProcess(delta);
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    // 攻击时敌人不移动
    if (_attackState == AttackState.Idle) {
      Velocity = _randomWalkComponent.TargetVelocity * TimeManager.Instance.TimeScale;
      MoveAndSlide();
    } else {
      Velocity = Vector2.Zero;
    }
  }

  /// <summary>
  /// 初始化并启动攻击状态机，取代了原先的 async 方法．
  /// </summary>
  private void StartAttackSequence() {
    _attackState = AttackState.Attacking;
    _attackDirection = (_player.GlobalPosition - GlobalPosition).Normalized();
    _bulletsFiredInSequence = 0;
    _attackTimer = 0; // 立即发射第一对子弹
  }

  /// <summary>
  /// 在 _Process 中每帧调用，处理攻击逻辑．
  /// </summary>
  private void HandleAttackState(float scaledDelta) {
    if (_attackState != AttackState.Attacking) return;

    _attackTimer -= scaledDelta;

    if (_attackTimer <= 0) {
      // 检查是否已完成攻击序列
      if (_bulletsFiredInSequence >= BulletCount) {
        _attackState = AttackState.Idle; // 攻击结束，返回 Idle 状态
        return;
      }

      var enemyPosition = GlobalPosition;

      var bullet1 = Bullet1.Instantiate<WavyBullet>();
      bullet1.GlobalPosition = enemyPosition;
      bullet1.GlobalRotation = _attackDirection.Angle();
      bullet1.InvertSine = false;
      GetTree().Root.AddChild(bullet1);

      var bullet2 = Bullet2.Instantiate<WavyBullet>();
      bullet2.GlobalPosition = enemyPosition;
      bullet2.GlobalRotation = _attackDirection.Angle();
      bullet2.InvertSine = true;
      GetTree().Root.AddChild(bullet2);

      _bulletsFiredInSequence++;

      // 重置计时器，等待下一次发射
      _attackTimer = BulletCreationInterval;
    }
  }

  public override RewindState CaptureState() {
    var baseState = (BaseEnemyState) base.CaptureState();
    return new DnaState {
      GlobalPosition = baseState.GlobalPosition,
      Velocity = baseState.Velocity,
      Health = baseState.Health,
      HitTimerLeft = baseState.HitTimerLeft,
      SpriteModulate = baseState.SpriteModulate,
      CurrentAttackState = this._attackState,
      AttackTimer = this._attackTimer,
      BulletsFiredInSequence = this._bulletsFiredInSequence,
      AttackDirection = this._attackDirection,
      ShootTimer = this._shootTimer
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not DnaState ds) return;
    this._attackState = ds.CurrentAttackState;
    this._attackTimer = ds.AttackTimer;
    this._bulletsFiredInSequence = ds.BulletsFiredInSequence;
    this._attackDirection = ds.AttackDirection;
    this._shootTimer = ds.ShootTimer;
  }
}
