using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseGeometricState : BasePhaseState {
  public float Timer;
  public int VolleysFiredThisCycle;
}

public partial class PhaseGeometric : BasePhase {
  // --- 状态变量 ---
  private float _timer;
  private int _volleysFiredThisCycle;
  private readonly RandomNumberGenerator _rng = new();

  [ExportGroup("Scene References")]
  [Export]
  public PackedScene BulletScene { get; set; }

  [ExportGroup("Pattern Configuration")]
  [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
  public float ActivePhaseStartTime { get; set; } = 1f;
  [Export(PropertyHint.Range, "0.05, 1.0, 0.01")]
  public float VolleyInterval { get; set; } = 0.3f;
  [Export(PropertyHint.Range, "1, 20, 1")]
  public int VolleysPerCycle { get; set; } = 10;
  [Export(PropertyHint.Range, "1, 100, 1")]
  public int BulletsPerVolley { get; set; } = 40;

  [ExportGroup("Bullet Properties")]
  [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
  public float BaseSpeed { get; set; } = 0.5f;
  [Export(PropertyHint.Range, "1, 1000, 1")]
  public float BulletSpeed { get; set; } = 150f;

  [ExportGroup("")]
  [Export]
  public float TimeScaleSensitivity { get; set; } = 1f;

  public override void StartPhase(Boss parent) {
    base.StartPhase(parent);
    _volleysFiredThisCycle = 0;
    // 设置第一次攻击前的初始延迟
    _timer = ActivePhaseStartTime;

    var rank = GameManager.Instance.EnemyRank;
    TimeScaleSensitivity = 3f / (rank + 3);
    BulletsPerVolley = Mathf.RoundToInt(40f * rank / 5f);
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;

    if (_timer <= 0) {
      // ID 根据已发射的数量计算，从 (VolleysPerCycle - 1) 倒数到 0
      int id = (VolleysPerCycle - 1) - _volleysFiredThisCycle;
      FireVolley(id);

      ++_volleysFiredThisCycle;

      // 检查当前攻击序列是否完成
      if (_volleysFiredThisCycle >= VolleysPerCycle) {
        _volleysFiredThisCycle = 0;
      } else {
        _timer += VolleyInterval;
      }
    } else {
      _timer -= scaledDelta;
    }
  }

  private void FireVolley(int id) {
    if (BulletScene == null) {
      GD.PrintErr("PhaseGeometric: BulletScene is not set!");
      return;
    }

    float baseAngleDegrees = id * 10f;
    float randomOffsetDegrees = (float) _rng.RandfRange(0, 10.0f);

    for (int i = 0; i < BulletsPerVolley; ++i) {
      // 角度 1: 用于计算速度，它包含基准角度，模拟一个旋转的方形发射源
      var baseRotationDegrees = i * 360f / BulletsPerVolley;
      float angleForSpeedCalcDegrees = baseRotationDegrees + baseAngleDegrees;
      float angleForSpeedCalcRad = Mathf.DegToRad(angleForSpeedCalcDegrees);

      // 角度 2: 用于决定子弹的实际飞行方向，它不包含基准角度
      float finalAngleDegrees = baseRotationDegrees + randomOffsetDegrees;

      // --- 计算速度 ---
      // 单位正方形边框距离
      float speedFactor = Mathf.Max(Mathf.Abs(Mathf.Sin(angleForSpeedCalcRad)), Mathf.Abs(Mathf.Cos(angleForSpeedCalcRad)));
      float speed = BaseSpeed * (2.0f - speedFactor) * BulletSpeed;

      // --- 实例化并设置子弹 ---
      var bullet = BulletScene.Instantiate<PhaseGeometricBullet>();
      bullet.GlobalPosition = ParentBoss.GlobalPosition;
      bullet.InitialSpeed = speed;
      bullet.RotationDegrees = finalAngleDegrees;
      bullet.TimeScaleSensitivity = TimeScaleSensitivity;
      bullet.VolleyId = id;
      bullet.SpeedAfterReverse = BulletSpeed * 1.5f;

      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  public override RewindState CaptureInternalState() {
    return new PhaseGeometricState {
      Timer = this._timer,
      VolleysFiredThisCycle = this._volleysFiredThisCycle,
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseGeometricState pgs) return;
    this._timer = pgs.Timer;
    this._volleysFiredThisCycle = pgs.VolleysFiredThisCycle;
  }
}
