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

  [ExportGroup("Scene References")]
  [Export] public PackedScene BulletScene { get; set; }

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
  public float BulletSpeed { get; set; } = 1.5f;

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _volleysFiredThisCycle = 0;
    _timer = ActivePhaseStartTime;

    var rank = GameManager.Instance.EnemyRank;
    TimeScaleSensitivity = 3f / (rank + 3);
    BulletsPerVolley = Mathf.RoundToInt(40f * rank / 5f);
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    if (_timer <= 0) {
      // ID 从 (VolleysPerCycle - 1) 倒数到 0
      int id = (VolleysPerCycle - 1) - _volleysFiredThisCycle;
      FireVolley(id);

      ++_volleysFiredThisCycle;

      if (_volleysFiredThisCycle >= VolleysPerCycle) {
        _volleysFiredThisCycle = 0;
        _timer = VolleyInterval * 2f; // 一个循环后的额外停顿
      } else {
        _timer += VolleyInterval;
      }
    } else {
      _timer -= scaledDelta;
    }
  }

  private void FireVolley(int id) {
    if (BulletScene == null) return;

    SoundManager.Instance.Play(SoundEffect.FireSmall);

    Vector3 bossPos = ParentBoss.GlobalPosition;
    float baseAngleDeg = id * 10f;
    float randomOffsetDeg = (float) GD.RandRange(0, 10.0f);

    // 预计算 lambda 需要的常量
    float stopTime = (id * 18f + 5f) / 60f;
    float restartTime = (id * 18f + 65f) / 60f;
    float speedReturn = BulletSpeed * 1.5f;

    var target = PlayerNode;

    for (int i = 0; i < BulletsPerVolley; ++i) {
      var bullet = BulletScene.Instantiate<SimpleBullet>();
      bullet.TimeScaleSensitivity = TimeScaleSensitivity;

      // 角度 1: 用于计算速度分布（正方形外观）
      float baseRotationDeg = i * 360f / BulletsPerVolley;
      float angleForSpeedRad = Mathf.DegToRad(baseRotationDeg + baseAngleDeg);

      // 角度 2: 实际发射角度
      float finalAngleRad = Mathf.DegToRad(baseRotationDeg + randomOffsetDeg);
      Vector3 direction = new Vector3(Mathf.Cos(finalAngleRad), 0, Mathf.Sin(finalAngleRad));

      // 原始正方形速度因子逻辑
      float speedFactor = Mathf.Max(Mathf.Abs(Mathf.Sin(angleForSpeedRad)), Mathf.Abs(Mathf.Cos(angleForSpeedRad)));
      float initialSpeed = BaseSpeed * (2.0f - speedFactor) * BulletSpeed;

      bullet.UpdateFunc = (t) => {
        SimpleBullet.UpdateState s = new();
        Vector3 pos;

        if (t < stopTime) {
          // 初始阶段：匀速前进
          pos = bossPos + direction * (initialSpeed * t);
        } else if (t < restartTime) {
          // 停止阶段：保持在停止点
          pos = bossPos + direction * (initialSpeed * stopTime);
        } else {
          // 返回阶段：反向加速（此处使用位移公式：停止点 - 方向 * 速度 * 时间）
          float dt = t - restartTime;
          Vector3 stopPoint = bossPos + direction * (initialSpeed * stopTime);
          pos = stopPoint - direction * (speedReturn * dt);
        }

        s.position = pos with { Y = target.GlobalPosition.Y };
        // 旋转：始终朝向当前移动轴（返回时 180 度翻转）
        float visualAngle = (t < restartTime) ? finalAngleRad : finalAngleRad + Mathf.Pi;
        s.rotation = new Vector3(0, -visualAngle, 0); // 对应 3D 坐标系

        return s;
      };

      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  public override RewindState CaptureInternalState() => new PhaseGeometricState {
    Timer = _timer,
    VolleysFiredThisCycle = _volleysFiredThisCycle,
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseGeometricState pgs) return;
    this._timer = pgs.Timer;
    this._volleysFiredThisCycle = pgs.VolleysFiredThisCycle;
  }
}
