using Godot;
using Rewind;

namespace Bullet;

public class PhaseWaveBulletState : BaseBullet3DState {
  public float WaveTime;
}

public partial class PhaseWaveBullet : BaseBullet3D {
  // --- 由 Phase 设置的属性 ---
  public float ForwardSpeed { get; set; }
  public float MaxHeight { get; set; }
  public float T1 { get; set; } // 余弦拱形持续时间
  public float T2 { get; set; } // 地面平移持续时间
  public float InitialPhase { get; set; } // 初始相位
  public bool InvertWave { get; set; } // 是否反转波形传播方向
  public Vector2 Direction { get; set; } // 子弹前进方向

  private float _waveTime;

  public override void _Ready() {
    base._Ready();
    // 应用初始相位，这样一排子弹才能形成波浪效果
    _waveTime = InitialPhase;
  }

  protected override void UpdatePosition(float scaledDelta) {
    if (InvertWave) {
      _waveTime -= scaledDelta;
    } else {
      _waveTime += scaledDelta;
    }

    // --- XY 平面（前进方向）运动 ---
    // 使用 Direction 属性来控制前进
    var moveDirection = new Vector3(Direction.X, Direction.Y, 0);
    RawPosition += moveDirection * ForwardSpeed * scaledDelta;

    // --- Z 轴（高度）周期性运动 ---
    float period = T1 + T2;
    if (period <= 0) return;

    float timeInPeriod = _waveTime % period;
    // 将负相位转换为正相位，以简化计算
    if (timeInPeriod < 0) {
      timeInPeriod += period;
    }


    if (timeInPeriod <= T1) {
      // 在余弦拱形阶段
      float progress = timeInPeriod / T1;
      // 将 [0, 1] 的进度映射到 [-PI/2, PI/2] 的余弦输入
      float angle = Mathf.Lerp(-Mathf.Pi / 2, Mathf.Pi / 2, progress);
      RawPosition = RawPosition with { Z = MaxHeight * Mathf.Cos(angle) };
    } else {
      // 在地面平移阶段
      RawPosition = RawPosition with { Z = 0 };
    }
  }

  public override RewindState CaptureState() {
    var baseState = (BaseBullet3DState) base.CaptureState();
    return new PhaseWaveBulletState {
      GlobalPosition = baseState.GlobalPosition,
      GlobalRotation = baseState.GlobalRotation,
      WasGrazed = baseState.WasGrazed,
      Modulate = baseState.Modulate,
      RawPosition = baseState.RawPosition,
      TimeAlive = baseState.TimeAlive,
      LandingIndicatorVisible = baseState.LandingIndicatorVisible,
      LandingIndicatorScale = baseState.LandingIndicatorScale,
      WaveTime = this._waveTime
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not PhaseWaveBulletState wbs) return;
    this._waveTime = wbs.WaveTime;
  }
}
