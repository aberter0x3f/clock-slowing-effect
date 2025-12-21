using Godot;

namespace Bullet;

public partial class WavyBullet : BaseBullet {
  [ExportGroup("Wavy Movement")]
  [Export]
  public float ForwardSpeed { get; set; } = 3.0f;
  [Export]
  public float Amplitude { get; set; } = 0.5f;
  [Export]
  public float Frequency { get; set; } = 1.0f;
  [Export]
  public bool InvertSine { get; set; } = false;

  public Vector3 InitialPosition { get; set; }

  private Vector3 _forwardVector;
  private Vector3 _sideVector;

  public override void _Ready() {
    GlobalPosition = InitialPosition;
    // 获取由 Dna 敌人通过 LookAt 设置好的 3D 局部坐标系
    // 在 Godot 中，LookAt 默认让 -Z 轴指向目标
    _forwardVector = -GlobalTransform.Basis.Z.Normalized();
    // X 轴即为垂直于前进方向的横向轴，用于正弦偏移
    _sideVector = GlobalTransform.Basis.X.Normalized();
    base._Ready();
  }

  public override void UpdateBullet(float scaledDelta) {
    base.UpdateBullet(scaledDelta);

    // 1. 计算 3D 空间中的位置
    // 沿瞄准线前进的距离
    float forwardDist = ForwardSpeed * TimeAlive;

    // 正弦波偏移值
    float phase = TimeAlive * Frequency * Mathf.Tau;
    float sineVal = Mathf.Sin(phase);
    if (InvertSine) sineVal *= -1;
    float sideDist = Amplitude * sineVal;

    // 最终 3D 位置 = 起点 + (前进方向 * 距离) + (侧向方向 * 偏移)
    GlobalPosition = InitialPosition + (_forwardVector * forwardDist) + (_sideVector * sideDist);

    // 2. 更新模型旋转（使其指向 3D 运动轨迹的切线）
    // 瞬时速度矢量 = (前进速度 * 前进向量) + (正弦波变化率 * 侧向向量)
    // 根据复合函数求导：[A * sin(t*f*2PI)]' = A * f * 2PI * cos(t*f*2PI)
    float cosPart = Amplitude * Frequency * Mathf.Tau * Mathf.Cos(phase);
    if (InvertSine) cosPart *= -1;

    Vector3 currentVelocity = (_forwardVector * ForwardSpeed) + (_sideVector * cosPart);

    if (!currentVelocity.IsZeroApprox()) {
      // 使用 Basis.LookingAt 将模型朝向当前的 3D 速度矢量方向
      // 使用原始 Basis 的 Y 轴作为参考上方向，以保持螺旋轴的连贯性
      GlobalRotation = Basis.LookingAt(currentVelocity, GlobalTransform.Basis.Y).GetEuler();
    }
  }
}
