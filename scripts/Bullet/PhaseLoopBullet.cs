using Godot;

namespace Bullet;

/// <summary>
/// 一种 3D 子弹，其高度 (Z 轴) 根据正弦波振荡．
/// 它的 XY 位置由外部控制．
/// </summary>
public partial class PhaseLoopBullet : BaseBullet3D {
  [ExportGroup("Sinusoidal Movement")]
  [Export]
  public float H { get; set; }
  [Export]
  public float K { get; set; }
  [Export]
  public float Phi { get; set; }

  protected override void UpdatePosition(float scaledDelta) {
    RawPosition = RawPosition with { Z = H * Mathf.Max(0f, Mathf.Sin(K * _timeAlive + Phi)) };
  }
}
