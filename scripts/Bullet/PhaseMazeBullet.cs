using Godot;
using Rewind;

namespace Bullet;

public class PhaseMazeBulletState : BaseBullet3DState {
  public float TargetZ;
}

/// <summary>
/// PhaseMaze 专用的子弹，具有根据玩家状态改变自身行为的能力．
/// </summary>
public partial class PhaseMazeBullet : BaseBullet3D {
  public enum MazeBulletType {
    Normal, // 无特殊效果
    LowSpeedPhase, // 低速时 Z 轴上移
    HighSpeedPhase, // 高速时 Z 轴上移
    Graze // 擦弹时 Y 轴减速
  }

  [Export]
  public MazeBulletType Type { get; set; } = MazeBulletType.Normal;

  [ExportGroup("Phasing")]
  [Export]
  public float PhaseHeight { get; set; } = 50f;
  [Export]
  public float PhaseSpeed { get; set; } = 400f; // Z 轴移动速度

  public float VelocityY { get; set; }

  private float _targetZ;

  protected override void UpdatePosition(float scaledDelta) {
    RawPosition = RawPosition with { Y = RawPosition.Y + VelocityY * scaledDelta };
  }

  public override void _Process(double delta) {
    // 基类 _Process 会处理回溯检查并调用 UpdatePosition
    base._Process(delta);

    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    // 处理 Z 轴相位移动
    bool isSlowMo = Input.IsActionPressed("time_slow");
    _targetZ = 0f; // 默认在游戏平面

    switch (Type) {
      case MazeBulletType.LowSpeedPhase:
        if (isSlowMo) {
          _targetZ = PhaseHeight;
        }
        break;
      case MazeBulletType.HighSpeedPhase:
        if (!isSlowMo) {
          _targetZ = PhaseHeight;
        }
        break;
      case MazeBulletType.Graze:
        if (WasGrazed) {
          _targetZ = PhaseHeight;
        }
        break;
    }

    // 平滑地移动到目标 Z 轴高度
    if (!Mathf.IsEqualApprox(RawPosition.Z, _targetZ)) {
      float newZ = Mathf.MoveToward(RawPosition.Z, _targetZ, PhaseSpeed * scaledDelta);
      RawPosition = RawPosition with { Z = newZ };
    }
  }

  public override RewindState CaptureState() {
    var baseState = (BaseBullet3DState) base.CaptureState();
    return new PhaseMazeBulletState {
      GlobalPosition = baseState.GlobalPosition,
      GlobalRotation = baseState.GlobalRotation,
      WasGrazed = baseState.WasGrazed,
      Modulate = baseState.Modulate,
      RawPosition = baseState.RawPosition,
      TimeAlive = baseState.TimeAlive,
      LandingIndicatorVisible = baseState.LandingIndicatorVisible,
      LandingIndicatorScale = baseState.LandingIndicatorScale,
      TargetZ = this._targetZ,
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not PhaseMazeBulletState pms) return;
    this._targetZ = pms.TargetZ;
  }
}
