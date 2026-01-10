using Godot;
using Rewind;

namespace Enemy.Boss;

public class BossState : BaseEnemyState {
  public Boss.BossInternalState InternalState;
  public float RestTimerLeft;
  public RewindState ActivePhaseState;
}

public partial class Boss : BaseEnemy {
  [Signal]
  public delegate void FightingPhaseStartedEventHandler();
  [Signal]
  public delegate void FightingPhaseEndedEventHandler();

  public enum BossInternalState {
    Resting,
    Fighting,
    Finished,
  }

  public BossInternalState InternalState { get; private set; } = BossInternalState.Resting;

  public override float MaxHealth { get => _activePhaseInstance?.MaxHealth ?? float.MaxValue; protected set { } }

  public override float Health {
    get => _activePhaseInstance?.Health ?? float.MaxValue;
    protected set {
      if (IsInstanceValid(_activePhaseInstance)) {
        _activePhaseInstance.Health = value;
      }
      UpdateHealthLabel();
    }
  }

  [ExportGroup("Phase Data")]
  [Export]
  public BossPhaseData PhaseData { get; set; }

  [ExportGroup("Phase Transition")]
  [Export]
  public float RestDuration { get; set; } = 5f; // 阶段间的休息时间

  private int _currentPhaseIndex = 0;
  private BasePhase _activePhaseInstance;
  private float _restTimerLeft;
  private PlayerState _playerPhaseStartState;
  private CollisionShape3D _collisionShape;
  private Vector3 _startPosition;
  private Godot.Collections.Array<PackedScene> _activePhaseSet;

  public override void _Ready() {
    base._Ready();
    _startPosition = GlobalPosition;
    _collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
    _restTimerLeft = RestDuration / 2; // 首次休息时间

    SetCollisionEnabled(false);
  }

  /// <summary>
  /// 由外部（例如 BossCombat 场景）调用，用于根据当前游戏状态设置本次战斗要使用的阶段组合．
  /// </summary>
  public void SetActivePhases(Godot.Collections.Array<PackedScene> phases) {
    _activePhaseSet = phases;
  }

  /// <summary>
  /// 在练习模式下，直接开始指定的阶段．
  /// </summary>
  public void StartSpecificPhase(int phaseIndex) {
    if (_activePhaseSet == null || phaseIndex < 0 || phaseIndex >= _activePhaseSet.Count) {
      GD.PrintErr($"Cannot start specific phase: Invalid index {phaseIndex} or phase set not configured.");
      return;
    }
    _currentPhaseIndex = phaseIndex;
  }

  public override void _Process(double delta) {
    base._Process(delta);
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    // 手动处理休息计时器
    if (InternalState == BossInternalState.Resting) {
      _restTimerLeft -= scaledDelta;
      if (_restTimerLeft <= 0) {
        OnPhaseStarted();
      }
    }

    if (InternalState == BossInternalState.Fighting) {
      float phaseEffectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, _activePhaseInstance.TimeScaleSensitivity);
      var phaseScaledDelta = (float) delta * phaseEffectiveTimeScale;
      _activePhaseInstance.UpdatePhase(phaseScaledDelta, phaseEffectiveTimeScale);
    }
  }

  /// <summary>
  /// 玩家请求从当前阶段重新开始．正常情况下玩家只能在 Fighting 状态下重新开始．
  /// </summary>
  public void RestartFromCurrentPhase() {
    if (InternalState == BossInternalState.Resting) {
      // 正常情况下不可能在 Resting 状态下重新开始．
      GD.PrintErr("Could not restart from phase while resting.");
      return;
    }

    if (_playerPhaseStartState == null) {
      GD.PrintErr("Cannot restart from phase: No saved state found.");
      return;
    }

    GD.Print($"Restarting from phase {_currentPhaseIndex}.");

    // 清理当前阶段
    if (IsInstanceValid(_activePhaseInstance)) {
      _activePhaseInstance.QueueFree();
      _activePhaseInstance = null;
    }

    // 清理场上所有子弹和掉落物等临时物品
    ClearAllBullets();
    ClearAllPickups();
    // 将 Boss 移回中心
    SetCollisionEnabled(false);
    GlobalPosition = _startPosition;

    // 恢复玩家状态
    GD.Print("Restoring player state...");
    _player.RestoreState(_playerPhaseStartState);
    GameManager.Instance.CurrentPlayerHealth = _playerPhaseStartState.Health;
    GameManager.Instance.TimeBond = _playerPhaseStartState.TimeBond;
    _player.IsPermanentlyDead = false;

    // 重新开始当前阶段的准备流程
    OnPhaseStarted();
  }

  private void OnPhaseStarted() {
    if (_activePhaseSet == null || _activePhaseSet.Count == 0) {
      GD.PrintErr("Boss has no active phase set configured. Cannot start phase.");
      InternalState = BossInternalState.Finished;
      Die();
      return;
    }

    GD.Print($"Starting boss phase {_currentPhaseIndex}.");

    SoundManager.Instance.Play(SoundEffect.PowerUp);

    // 启用碰撞，让玩家可以攻击
    SetCollisionEnabled(true);

    // 实例化并启动新阶段
    var phaseScene = _activePhaseSet[_currentPhaseIndex];
    _activePhaseInstance = phaseScene.Instantiate<BasePhase>();
    AddChild(_activePhaseInstance);
    _activePhaseInstance.PhaseCompleted += OnPhaseEnded;
    _activePhaseInstance.PhaseStart(this);

    // 重置回溯历史，防止跨阶段回溯引起状态混乱
    RewindManager.Instance.ResetHistory();

    InternalState = BossInternalState.Fighting;

    // 保存玩家状态，用于「从当前阶段重来」
    _playerPhaseStartState = (PlayerState) _player.CaptureState();

    EmitSignal(SignalName.FightingPhaseStarted);
  }

  private void OnPhaseEnded() {
    GD.Print($"Boss Phase {_currentPhaseIndex} completed!");

    SoundManager.Instance.Play(SoundEffect.BossDeath);

    CallDeferred(nameof(SetCollisionEnabled), false);
    ClearAllBullets();
    SpawnTimeShards(_activePhaseInstance.TimeShardsOnCompletion);

    // 清理当前阶段的实例
    if (IsInstanceValid(_activePhaseInstance)) {
      _activePhaseInstance.QueueFree();
    }
    _activePhaseInstance = null;

    // 重置回溯历史，防止跨阶段回溯引起状态混乱
    RewindManager.Instance.ResetHistory();

    ++_currentPhaseIndex;

    GlobalPosition = _startPosition;
    InternalState = BossInternalState.Resting;
    _restTimerLeft = RestDuration;

    EmitSignal(SignalName.FightingPhaseEnded);

    if (_currentPhaseIndex >= _activePhaseSet.Count) {
      GD.Print("Boss defeated!");
      InternalState = BossInternalState.Finished;
      // 调用基类的 Die，这会触发 Died 信号，让 BossRoom 生成传送门
      Die();
    }
  }

  private void ClearAllBullets() {
    foreach (IRewindable bullet in GetTree().GetNodesInGroup("bullets")) {
      bullet.Destroy();
    }
    foreach (IRewindable bullet in GetTree().GetNodesInGroup("enemy_creations")) {
      bullet.Destroy();
    }
  }

  private void ClearAllPickups() {
    foreach (IRewindable bullet in GetTree().GetNodesInGroup("pickups")) {
      bullet.Destroy();
    }
  }

  public void SetCollisionEnabled(bool enabled) {
    _collisionShape.Disabled = !enabled;
  }

  public override void TakeDamage(float damage) {
    // 这里不调用基类的 TakeDamage，因为 Boss 的生命值由其阶段控制
    // 将伤害传递给当前激活的阶段
    _enemyPolyhedron.SetHitState(true);
    _hitTimer.Start();
    if (_activePhaseInstance != null && !IsDestroyed) {
      _activePhaseInstance.TakeDamage(damage);
    }
  }

  public override RewindState CaptureState() {
    var baseState = (BaseEnemyState) base.CaptureState();
    return new BossState {
      GlobalPosition = baseState.GlobalPosition,
      Velocity = baseState.Velocity,
      Health = baseState.Health,
      HitTimerLeft = baseState.HitTimerLeft,
      IsInHitState = baseState.IsInHitState,
      InternalState = this.InternalState,
      RestTimerLeft = this._restTimerLeft,
      ActivePhaseState = _activePhaseInstance?.CaptureInternalState(),
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not BossState bs) return;

    this.InternalState = bs.InternalState;
    this._restTimerLeft = bs.RestTimerLeft;

    if (_activePhaseInstance != null && IsInstanceValid(_activePhaseInstance)) {
      _activePhaseInstance.RestoreInternalState(bs.ActivePhaseState);
    }
  }
}
