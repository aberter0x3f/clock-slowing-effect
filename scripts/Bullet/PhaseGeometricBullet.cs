using Godot;
using Rewind;

namespace Bullet;

public class PhaseGeometricBulletState : SimpleBulletState {
  public PhaseGeometricBullet.State CurrentState;
}

/// <summary>
/// 一种具有特殊行为的子弹，用于 PhaseGeometric．
/// 它会在飞行途中暂停，然后反向加速．
/// </summary>
public partial class PhaseGeometricBullet : SimpleBullet {
  public enum State {
    Initial,
    Stopped,
    Returning
  }

  private State _currentState = State.Initial;
  private float _stopTime;
  private float _restartTime;

  /// <summary>
  /// 由 Spawner 设置，用于决定子弹的暂停和重启时间．
  /// </summary>
  public int VolleyId { get; set; }

  /// <summary>
  /// 重启后的速度．
  /// </summary>
  public float SpeedAfterReverse { get; set; }

  public override void _Ready() {
    base._Ready();

    // 根据 VB 代码的帧数（@60fps）计算时间点
    _stopTime = (VolleyId * 18f + 5f) / 60f;
    _restartTime = (VolleyId * 18f + 65f) / 60f;
  }

  public override void _Process(double delta) {
    // 在调用基类处理移动之前，先根据时间更新自己的状态和速度
    if (!RewindManager.Instance.IsRewinding && !RewindManager.Instance.IsPreviewing) {
      // 使用 TimeAlive (由基类 SimpleBullet 维护) 来驱动状态机
      if (_currentState == State.Initial && _timeAlive >= _stopTime) {
        _currentState = State.Stopped;
        Velocity = Vector2.Zero;
        // 禁用任何可能存在的加速度，确保完全停止
        SameDirectionAcceleration = 0;
        Acceleration = Vector2.Zero;
      } else if (_currentState == State.Stopped && _timeAlive >= _restartTime) {
        _currentState = State.Returning;
        // 方向反转 (180 度)
        Rotation += Mathf.Pi;
        // 设置新的速度和方向
        Velocity = Vector2.Right.Rotated(Rotation) * SpeedAfterReverse;
      }
    }

    // 调用基类的 _Process 方法来处理实际的移动、生命周期、边界检查等
    base._Process(delta);
  }

  public override RewindState CaptureState() {
    var baseState = (SimpleBulletState) base.CaptureState();
    return new PhaseGeometricBulletState {
      // 继承自 SimpleBulletState
      GlobalPosition = baseState.GlobalPosition,
      GlobalRotation = baseState.GlobalRotation,
      WasGrazed = baseState.WasGrazed,
      Modulate = baseState.Modulate,
      Velocity = baseState.Velocity,
      AngularVelocity = baseState.AngularVelocity,
      TimeAlive = baseState.TimeAlive,
      // 新增状态
      CurrentState = this._currentState
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not PhaseGeometricBulletState gbs) return;
    this._currentState = gbs.CurrentState;
  }
}
