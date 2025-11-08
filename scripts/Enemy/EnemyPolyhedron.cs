using Godot;

/// <summary>
/// 一个用于在 MeshInstance3D 上动态生成和控制正多面体视觉效果的脚本．
/// 它可以在编辑器中实时预览形状、大小和颜色的变化．
/// </summary>
[Tool]
public partial class EnemyPolyhedron : MeshInstance3D {
  public enum ShapeType {
    Tetrahedron, // 正四面体
    Hexahedron,  // 正六面体
    Octahedron   // 正八面体
  }

  public static readonly Color HIT_COLOR = new(1.0f, 0.5f, 0.5f);

  private ShapeType _shape = ShapeType.Octahedron;
  private float _size = 1f;
  private Color _color = Colors.White;
  private bool _enableHitColor = false;
  private float _rotationSpeed = 2f;

  private Vector3 _rotationAxis;
  private readonly RandomNumberGenerator _rnd = new();
  private StandardMaterial3D _material;

  [Export]
  public ShapeType Shape {
    get => _shape;
    set {
      _shape = value;
      if (Engine.IsEditorHint()) {
        GenerateMesh();
      }
    }
  }

  [Export]
  public float Size {
    get => _size;
    set {
      _size = value;
      if (Engine.IsEditorHint()) {
        GenerateMesh();
      }
    }
  }

  [Export]
  public Color Color {
    get => _color;
    set {
      _color = value;
      UpdateMaterialColor();
    }
  }

  [Export]
  public bool EnableHitColor {
    get => _enableHitColor;
    set {
      _enableHitColor = value;
      UpdateMaterialColor();
    }
  }

  [Export(PropertyHint.Range, "0, 10, 0.1")]
  public float RotationSpeed {
    get => _rotationSpeed;
    set => _rotationSpeed = value;
  }

  public override void _Ready() {
    GenerateMesh();
    UpdateMaterialColor();

    // 仅在运行时随机化旋转轴
    if (!Engine.IsEditorHint()) {
      _rotationAxis = new Vector3(
        (float) _rnd.RandfRange(-1, 1),
        (float) _rnd.RandfRange(-1, 1),
        (float) _rnd.RandfRange(-1, 1)
      ).Normalized();
    }
  }

  public override void _Process(double delta) {
    // 仅在运行时应用旋转
    if (!Engine.IsEditorHint() && _rotationSpeed > 0) {
      Rotate(_rotationAxis, RotationSpeed * (float) delta);
    }
  }

  /// <summary>
  /// 设置是否处于「被击中」的视觉状态．
  /// </summary>
  public void SetHitState(bool isHit) {
    if (isHit) {
      EnableHitColor = true;
    } else {
      EnableHitColor = false;
    }
  }

  private void UpdateMaterialColor() {
    // 确保我们有一个有效的材质实例
    if (!IsInstanceValid(_material)) {
      if (GetActiveMaterial(0) is StandardMaterial3D existingMaterial) {
        _material = existingMaterial;
      } else {
        _material = new StandardMaterial3D();
        SetSurfaceOverrideMaterial(0, _material);
      }
    }

    var finalColor = _color;
    if (EnableHitColor) {
      finalColor *= HIT_COLOR;
    }
    _material.AlbedoColor = finalColor;
  }

  private void GenerateMesh() {
    var st = new SurfaceTool();
    st.Begin(Mesh.PrimitiveType.Triangles);

    switch (_shape) {
      case ShapeType.Tetrahedron:
        GenerateTetrahedron(st);
        break;
      case ShapeType.Hexahedron:
        GenerateHexahedron(st);
        break;
      case ShapeType.Octahedron:
        GenerateOctahedron(st);
        break;
    }

    st.GenerateNormals();
    var arrayMesh = st.Commit();
    this.Mesh = arrayMesh;
  }

  private void GenerateTetrahedron(SurfaceTool st) {
    float r = _size;
    var vertices = new Vector3[] {
      new Vector3(1, 1, 1).Normalized() * r,
      new Vector3(1, -1, -1).Normalized() * r,
      new Vector3(-1, 1, -1).Normalized() * r,
      new Vector3(-1, -1, 1).Normalized() * r
    };

    st.AddVertex(vertices[0]); st.AddVertex(vertices[2]); st.AddVertex(vertices[1]);
    st.AddVertex(vertices[0]); st.AddVertex(vertices[1]); st.AddVertex(vertices[3]);
    st.AddVertex(vertices[0]); st.AddVertex(vertices[3]); st.AddVertex(vertices[2]);
    st.AddVertex(vertices[1]); st.AddVertex(vertices[2]); st.AddVertex(vertices[3]);
  }

  private void GenerateHexahedron(SurfaceTool st) {
    float s = _size / Mathf.Sqrt(3); // 计算立方体半边长，使其外接球半径为 _size
    var vertices = new Vector3[] {
      new(-s, -s, -s), new(s, -s, -s), new(s, s, -s), new(-s, s, -s),
      new(-s, -s, s), new(s, -s, s), new(s, s, s), new(-s, s, s)
    };

    // Front
    st.AddVertex(vertices[0]); st.AddVertex(vertices[1]); st.AddVertex(vertices[2]);
    st.AddVertex(vertices[0]); st.AddVertex(vertices[2]); st.AddVertex(vertices[3]);
    // Back
    st.AddVertex(vertices[4]); st.AddVertex(vertices[6]); st.AddVertex(vertices[5]);
    st.AddVertex(vertices[4]); st.AddVertex(vertices[7]); st.AddVertex(vertices[6]);
    // Left
    st.AddVertex(vertices[0]); st.AddVertex(vertices[3]); st.AddVertex(vertices[7]);
    st.AddVertex(vertices[0]); st.AddVertex(vertices[7]); st.AddVertex(vertices[4]);
    // Right
    st.AddVertex(vertices[1]); st.AddVertex(vertices[5]); st.AddVertex(vertices[6]);
    st.AddVertex(vertices[1]); st.AddVertex(vertices[6]); st.AddVertex(vertices[2]);
    // Top
    st.AddVertex(vertices[3]); st.AddVertex(vertices[2]); st.AddVertex(vertices[6]);
    st.AddVertex(vertices[3]); st.AddVertex(vertices[6]); st.AddVertex(vertices[7]);
    // Bottom
    st.AddVertex(vertices[0]); st.AddVertex(vertices[4]); st.AddVertex(vertices[5]);
    st.AddVertex(vertices[0]); st.AddVertex(vertices[5]); st.AddVertex(vertices[1]);
  }

  private void GenerateOctahedron(SurfaceTool st) {
    float r = _size;
    var vertices = new Vector3[] {
      new(r, 0, 0), new(-r, 0, 0),
      new(0, r, 0), new(0, -r, 0),
      new(0, 0, r), new(0, 0, -r)
    };

    st.AddVertex(vertices[0]); st.AddVertex(vertices[4]); st.AddVertex(vertices[2]);
    st.AddVertex(vertices[0]); st.AddVertex(vertices[2]); st.AddVertex(vertices[5]);
    st.AddVertex(vertices[0]); st.AddVertex(vertices[5]); st.AddVertex(vertices[3]);
    st.AddVertex(vertices[0]); st.AddVertex(vertices[3]); st.AddVertex(vertices[4]);
    st.AddVertex(vertices[1]); st.AddVertex(vertices[2]); st.AddVertex(vertices[4]);
    st.AddVertex(vertices[1]); st.AddVertex(vertices[5]); st.AddVertex(vertices[2]);
    st.AddVertex(vertices[1]); st.AddVertex(vertices[3]); st.AddVertex(vertices[5]);
    st.AddVertex(vertices[1]); st.AddVertex(vertices[4]); st.AddVertex(vertices[3]);
  }
}
