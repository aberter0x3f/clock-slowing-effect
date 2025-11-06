using Godot;

namespace UI;

public partial class MapMenuNode : TextureButton {
  // 在 Godot 编辑器中暴露这些属性，方便调整
  [Export]
  public Color BorderColor { get; set; } = new Color(1, 1, 1, 1);

  [Export]
  public float BorderWidth { get; set; } = 2.0f;

  [Export]
  public float BorderOffset { get; set; } = 5.0f; // 边框距离原始六边形的距离

  public override void _Ready() {
    FocusEntered += OnFocusChanged;
    FocusExited += OnFocusChanged;
  }

  private void OnFocusChanged() {
    QueueRedraw();
  }

  public override void _Draw() {
    if (!HasFocus()) {
      return;
    }

    Vector2 center = Size / 2.0f;
    float baseRadius = Mathf.Min(Size.X, Size.Y) / 2.0f;
    float borderRadius = baseRadius + BorderOffset;

    // 获取六边形的顶点坐标
    Vector2[] points = GetHexagonPoints(center, borderRadius);

    // 绘制多边形线条作为边框
    // 最后一个参数 true 表示启用抗锯齿，让线条更平滑
    DrawPolyline(points, BorderColor, BorderWidth, true);
  }

  /// <summary>
  /// 计算一个平顶正六边形的顶点．
  /// </summary>
  /// <param name="center">中心点</param>
  /// <param name="radius">半径（从中心到顶点的距离）</param>
  /// <returns>包含6个顶点的数组</returns>
  private Vector2[] GetHexagonPoints(Vector2 center, float radius) {
    // +1 使其闭合
    Vector2[] points = new Vector2[6 + 1];
    for (int i = 0; i < 6 + 1; ++i) {
      float angle_deg = 60 * i + 90;
      float angle_rad = Mathf.DegToRad(angle_deg);
      points[i] = new Vector2(
          center.X + radius * Mathf.Cos(angle_rad),
          center.Y + radius * Mathf.Sin(angle_rad)
      );
    }
    return points;
  }
}
