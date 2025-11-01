using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseArcsAndLinesState : BasePhaseState {
  public PhaseArcsAndLines.AttackState CurrentState;
  public float Timer;
  public int VolleyCounter;
  public bool IsCurrentPhaseInverted;
  public float Theta0;
}

public partial class PhaseArcsAndLines : BasePhase {
  /// <summary>
  /// Boss 在此阶段的攻击状态机．
  /// </summary>
  public enum AttackState {
    /// <summary>
    /// 正在移动到攻击起始位置．
    /// </summary>
    MovingToPosition,
    /// <summary>
    /// 在一轮齐射 (Volley) 的间隙．
    /// </summary>
    BetweenVolleys,
    /// <summary>
    /// 在两个攻击阶段 (Phase) 之间的间隙．
    /// </summary>
    BetweenPhases
  }

  [ExportGroup("Movement")]
  [Export]
  public Vector2 TargetPosition { get; set; } = new(0, -300);
  [Export(PropertyHint.Range, "10, 2000, 10")]
  public float MoveSpeed { get; set; } = 500f; // Boss 的移动速度

  [ExportGroup("Attack Pattern")]
  [Export(PropertyHint.Range, "1, 100, 1")]
  public int BulletCount { get; set; } = 10;
  [Export(PropertyHint.Range, "1, 20, 1")]
  public int VolleyCount { get; set; } = 10;
  [Export(PropertyHint.Range, "0.05, 1.0, 0.05")]
  public float VolleyInterval { get; set; } = 0.05f;
  [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
  public float PhaseInterval { get; set; } = 0.05f;

  [ExportGroup("Bullet Path")]
  [Export(PropertyHint.Range, "10, 500, 1")]
  public float PrimaryRadius { get; set; } = 300f;
  [Export(PropertyHint.Range, "10, 500, 1")]
  public float SecondaryRadius { get; set; } = 300f;
  [Export(PropertyHint.Range, "0.1, 5.0, 0.05")]
  public float QuarterArcDuration { get; set; } = 0.8f;
  [Export(PropertyHint.Range, "0.1, 5.0, 0.05")]
  public float SecondaryArcDuration { get; set; } = 0.8f;
  [Export(PropertyHint.Range, "100, 1000, 10")]
  public float FinalLinearSpeed { get; set; } = 600f;

  [ExportGroup("Scene Reference")]
  [Export]
  public PackedScene BulletScene { get; set; }

  // 状态机变量
  private AttackState _currentState;
  private float _timer;
  private int _volleyCounter;
  private bool _isCurrentPhaseInverted;
  private float _theta0;
  private readonly RandomNumberGenerator _rng = new();

  public override void StartPhase(Boss parent) {
    base.StartPhase(parent);

    // 初始化状态机，首先进入移动状态
    _currentState = AttackState.MovingToPosition;
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    switch (_currentState) {
      case AttackState.MovingToPosition:
        // 将 Boss 移向目标位置
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(TargetPosition, MoveSpeed * scaledDelta);

        // 检查 Boss 是否已到达目标位置
        if (ParentBoss.GlobalPosition.IsEqualApprox(TargetPosition)) {
          // 到达后开始攻击模式
          StartAttackPattern();
        }
        break;

      case AttackState.BetweenVolleys:
        _timer -= scaledDelta;
        if (_timer > 0) {
          return;
        }

        if (_volleyCounter < VolleyCount) {
          // 当前阶段还有更多齐射
          FireVolley();
          _volleyCounter++;
          _timer = VolleyInterval;
        } else {
          // 当前阶段齐射完毕，进入阶段间隔
          _currentState = AttackState.BetweenPhases;
          _timer = PhaseInterval;
        }
        break;

      case AttackState.BetweenPhases:
        _timer -= scaledDelta;
        if (_timer > 0) {
          return;
        }

        // 阶段间隔结束，开始下一阶段
        _isCurrentPhaseInverted = !_isCurrentPhaseInverted;
        _volleyCounter = 0;
        _theta0 = _rng.Randf() * Mathf.Tau;

        FireVolley();
        _volleyCounter++;

        _currentState = AttackState.BetweenVolleys;
        _timer = VolleyInterval;
        break;
    }
  }

  /// <summary>
  /// 初始化攻击模式的状态并开始发射子弹．
  /// </summary>
  private void StartAttackPattern() {
    // 初始化攻击相关的状态
    _isCurrentPhaseInverted = false;
    _volleyCounter = 0;
    _theta0 = _rng.Randf() * Mathf.Tau;

    // 立即发射第一波
    FireVolley();
    _volleyCounter++;

    _currentState = AttackState.BetweenVolleys;
    _timer = VolleyInterval;
  }

  private void FireVolley() {
    if (BulletScene == null) {
      GD.PrintErr("PhaseArcsAndLines: BulletScene is not set!");
      return;
    }

    for (int i = 0; i < BulletCount; ++i) {
      float baseAngle = (Mathf.Tau / BulletCount * i) + _theta0;
      var bullet = BulletScene.Instantiate<ArcsAndLinesBullet>();

      // 初始化子弹的路径参数
      bullet.BasePosition = new Vector3(TargetPosition.X, TargetPosition.Y, 0);
      bullet.RawPosition = bullet.BasePosition;
      bullet.BaseAngle = baseAngle;
      bullet.PrimaryRadius = PrimaryRadius;
      bullet.SecondaryRadius = SecondaryRadius;
      bullet.QuarterArcDuration = QuarterArcDuration;
      bullet.SecondaryArcDuration = SecondaryArcDuration;
      bullet.FinalLinearSpeed = FinalLinearSpeed;
      bullet.InvertHorizontalPlaneY = _isCurrentPhaseInverted;

      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  public override RewindState CaptureInternalState() {
    return new PhaseArcsAndLinesState {
      CurrentState = this._currentState,
      Timer = this._timer,
      VolleyCounter = this._volleyCounter,
      IsCurrentPhaseInverted = this._isCurrentPhaseInverted,
      Theta0 = this._theta0
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseArcsAndLinesState pals) return;
    this._currentState = pals.CurrentState;
    this._timer = pals.Timer;
    this._volleyCounter = pals.VolleyCounter;
    this._isCurrentPhaseInverted = pals.IsCurrentPhaseInverted;
    this._theta0 = pals.Theta0;
  }
}
