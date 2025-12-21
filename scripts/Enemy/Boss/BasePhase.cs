using Godot;
using Rewind;

namespace Enemy.Boss;

public class BasePhaseState : RewindState { }

/// <summary>
/// Boss 攻击阶段的基类．
/// 每个阶段都是一个独立的节点，负责管理自己的血量、攻击模式和状态．
/// </summary>
[GlobalClass]
public abstract partial class BasePhase : Node {
  [Signal]
  public delegate void PhaseCompletedEventHandler();

  [ExportGroup("Time")]
  [Export]
  public float TimeScaleSensitivity { get; set; } = 1f;

  private bool _isFinished;
  private float _health;
  private Player _player;

  public virtual float MaxHealth { get; protected set; } = 40f;
  public virtual float DamageReduction {
    get {
      // 满足：
      // EnemyRank = 5 时为 0.6
      // EnemyRank -> +inf 时 -> 1.0
      float x = (float) GameManager.Instance.EnemyRank / 5f;
      return 0.6f + 0.4f * (x - 1) / (x + 1);
    }
  }
  public virtual int TimeShardsOnCompletion { get; protected set; } = 50;

  public float Health {
    get => _health;
    set {
      if (value <= 0) {
        _health = 0;
        var rm = RewindManager.Instance;
        if (!rm.IsPreviewing && !rm.IsRewinding) {
          PhaseEnd();
        }
      } else {
        _health = float.Min(MaxHealth, value);
      }
    }
  }
  protected Boss ParentBoss { get; private set; }
  protected Node3D PlayerNode => _player.DecoyTarget ?? _player;

  /// <summary>
  /// 当 Boss 控制器启动此阶段时调用．
  /// </summary>
  public virtual void PhaseStart(Boss parent) {
    ParentBoss = parent;
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    Health = MaxHealth;
    SetProcess(true);
    SetPhysicsProcess(true);
  }

  /// <summary>
  /// 当前阶段每一帧调用．
  // 不要重载 _Process，重载这个函数更适合．
  /// </summary>
  public abstract void UpdatePhase(float scaledDelta, float effectiveTimeScale);

  /// <summary>
  /// 当此阶段的逻辑结束时（例如血量耗尽）调用．
  /// </summary>
  public virtual void PhaseEnd() {
    if (_isFinished) return;
    _isFinished = true;
    SetProcess(false);
    SetPhysicsProcess(false);
    EmitSignal(SignalName.PhaseCompleted);
  }

  /// <summary>
  /// 外部（通常是 Boss 节点）调用的受伤处理函数．
  /// </summary>
  public virtual void TakeDamage(float amount) {
    Health -= amount * (1.0f - DamageReduction);
  }

  /// <summary>
  /// 捕获此阶段的内部状态．
  /// </summary>
  public virtual RewindState CaptureInternalState() {
    return new BasePhaseState { };
  }

  /// <summary>
  /// 恢复此阶段的内部状态．
  /// </summary>
  public virtual void RestoreInternalState(RewindState state) {
    if (state is not BasePhaseState bps) return;
  }
}
