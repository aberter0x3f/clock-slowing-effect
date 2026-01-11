using Godot;

namespace Weapon;

[Tool]
public partial class AimingCone : MeshInstance3D {
  [Export(PropertyHint.Range, "0, 360, 0.1")]
  public float AngleDeg {
    get => _angleDeg;
    set {
      _angleDeg = value;
      if (IsInsideTree()) GenerateMesh();
    }
  }

  [Export]
  public float Radius {
    get => _radius;
    set {
      _radius = value;
      if (IsInsideTree()) GenerateMesh();
    }
  }

  [Export]
  public Color Color {
    get => _color;
    set {
      _color = value;
      UpdateMaterial();
    }
  }

  private float _angleDeg = 30f;
  private float _radius = 5f;
  private Color _color;
  private StandardMaterial3D _material;

  public override void _Ready() {
    // 初始化材质：半透明、无光照、双面渲染
    _material = new StandardMaterial3D {
      Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
      ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
      CullMode = BaseMaterial3D.CullModeEnum.Disabled,
      AlbedoColor = _color,
      VertexColorUseAsAlbedo = false
    };
    this.MaterialOverride = _material;

    GenerateMesh();
  }

  private void UpdateMaterial() {
    if (_material != null) {
      _material.AlbedoColor = _color;
    }
  }

  private void GenerateMesh() {
    var st = new SurfaceTool();
    st.Begin(Mesh.PrimitiveType.Triangles);

    // 中心点
    Vector3 center = Vector3.Zero;

    // 扇形朝向：Godot 中 Forward 是 -Z
    // 我们希望扇形的中心线指向 -Z
    // 角度范围从 +Angle/2 到 -Angle/2
    float halfAngleRad = Mathf.DegToRad(_angleDeg) / 2f;

    // 分段数：角度越大，分段越多，保证边缘平滑
    int segments = Mathf.Max(1, Mathf.CeilToInt(_angleDeg / 5.0f));

    // 生成顶点
    // 顶点顺序：中心点 -> 弧上点 1 -> 弧上点 2
    // 在 XZ 平面上绘制 (Y=0)

    // 起始向量 (旋转 +halfAngle)
    // 基础向量是 (0, 0, -Radius)

    for (int i = 0; i < segments; ++i) {
      float t1 = (float) i / segments;
      float t2 = (float) (i + 1) / segments;

      // 插值角度：从 +half 到 -half
      float a1 = Mathf.Lerp(halfAngleRad, -halfAngleRad, t1);
      float a2 = Mathf.Lerp(halfAngleRad, -halfAngleRad, t2);

      // 计算圆弧上的点 (基于 -Z 轴旋转)
      // Forward (-Z) 旋转 a 弧度
      // x = -sin(a), z = -cos(a)  (Godot 坐标系顺时针旋转)
      Vector3 p1 = new Vector3(-Mathf.Sin(a1), 0, -Mathf.Cos(a1)) * _radius;
      Vector3 p2 = new Vector3(-Mathf.Sin(a2), 0, -Mathf.Cos(a2)) * _radius;

      // 添加三角形
      st.AddVertex(center);
      st.AddVertex(p1);
      st.AddVertex(p2);
    }

    st.GenerateNormals();
    this.Mesh = st.Commit();
  }
}
