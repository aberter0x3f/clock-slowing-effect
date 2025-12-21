using Godot;

// 将其注册为全局类，这样在 Godot 编辑器中添加节点时可以直接搜索到它
[GlobalClass]
public partial class RandomWalkComponent : Node {
  [Export(PropertyHint.Range, "0, 500, 1")]
  public float MoveSpeedMean { get; set; } = 0.8f;

  [Export(PropertyHint.Range, "0, 200, 1")]
  public float MoveSpeedSigma { get; set; } = 0.25f;

  [Export(PropertyHint.Range, "0.5, 10.0, 0.1")]
  public float MoveDuration { get; set; } = 2.0f;

  /// <summary>
  /// 计算出的当前期望速度，供父节点读取．
  /// </summary>
  public Vector3 TargetVelocity { get; set; } = Vector3.Zero;

  private float _moveTimer;
  private Vector3 _currentMoveDirection;
  private float _currentMoveSpeed;
  private CharacterBody3D _parentBody; // 父节点的引用

  public override void _Ready() {
    // 获取父节点，确保它是一个 CharacterBod32D
    _parentBody = GetParent<CharacterBody3D>();
    if (_parentBody == null) {
      GD.PrintErr("RandomWalkComponent must be a child of a CharacterBody2D.");
      SetProcess(false); // 如果父节点不对，就禁用自己
      return;
    }
    PickNewMovement();
  }

  public override void _Process(double delta) {
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    _moveTimer -= scaledDelta;

    if (_parentBody.IsOnWall() || _moveTimer <= 0) {
      PickNewMovement();
    }

    // 计算目标速度，但不直接应用它
    TargetVelocity = _currentMoveDirection * _currentMoveSpeed;
  }

  public void PickNewMovement() {
    _moveTimer = MoveDuration;
    _currentMoveSpeed = Mathf.Max(0, (float) GD.Randfn(MoveSpeedMean, MoveSpeedSigma));
    float randomAngle = (float) GD.RandRange(0, Mathf.Tau);
    _currentMoveDirection = new Vector3(Mathf.Sin(randomAngle), 0, Mathf.Cos(randomAngle));
  }
}
