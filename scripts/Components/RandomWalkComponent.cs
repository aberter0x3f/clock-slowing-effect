using Godot;

// 将其注册为全局类，这样在 Godot 编辑器中添加节点时可以直接搜索到它
[GlobalClass]
public partial class RandomWalkComponent : Node {
  [Export(PropertyHint.Range, "0, 500, 1")]
  public float MoveSpeedMean { get; set; } = 80.0f;

  [Export(PropertyHint.Range, "0, 200, 1")]
  public float MoveSpeedSigma { get; set; } = 25.0f;

  [Export(PropertyHint.Range, "0.5, 10.0, 0.1")]
  public float MoveDuration { get; set; } = 2.0f;

  /// <summary>
  /// 计算出的当前期望速度，供父节点读取．
  /// </summary>
  public Vector2 TargetVelocity { get; private set; } = Vector2.Zero;

  private float _moveTimer;
  private Vector2 _currentMoveDirection;
  private float _currentMoveSpeed;
  private CharacterBody2D _parentBody; // 父节点的引用

  private readonly RandomNumberGenerator _rnd = new();

  public override void _Ready() {
    // 获取父节点，确保它是一个 CharacterBody2D
    _parentBody = GetParent<CharacterBody2D>();
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

  /// <summary>
  /// 随机选择一个新的移动方向和速度，并重置计时器．
  /// 父节点可以在撞墙时调用此方法．
  /// </summary>
  public void PickNewMovement() {
    _moveTimer = MoveDuration;
    _currentMoveSpeed = Mathf.Max(0, (float) _rnd.Randfn(MoveSpeedMean, MoveSpeedSigma));
    float randomAngle = (float) _rnd.RandfRange(0, Mathf.Tau);
    _currentMoveDirection = Vector2.Right.Rotated(randomAngle);
  }
}
