using Godot;

namespace Bullet;

/// <summary>
/// Represents a bullet that "falls" from a certain height in 3D space.
/// It only activates its 2D collision shape when it gets close to the ground plane (Z=0).
/// It now includes a landing indicator for better player feedback.
/// </summary>
public partial class FallingBullet : BaseBullet {
  private enum State {
    Falling,
    Landed
  }

  private State _currentState = State.Falling;
  private CollisionShape2D _collisionShape;
  private Node3D _landingIndicator;

  private float _currentHeight;
  private float _verticalVelocity;
  private float _startHeight;
  private RandomNumberGenerator _rnd = new RandomNumberGenerator();

  [ExportGroup("Falling Behavior")]
  [Export]
  public float GravityZ { get; set; } = 300.0f;
  [Export]
  public float GravitySigma { get; set; } = 100.0f;
  [Export]
  public float CollisionActivationHeight { get; set; } = 1.0f;
  [Export]
  public float LifetimeOnGround { get; set; } = 0.5f;

  public override void _Ready() {
    _collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
    _landingIndicator = GetNode<Node3D>("LandingIndicator");

    if (_collisionShape != null) {
      _collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
    }

    // 确保指示器初始可见
    if (_landingIndicator != null) {
      _landingIndicator.Visible = true;
    }

    GravityZ += float.Abs(_rnd.Randfn(0, GravitySigma));

    base._Ready();
  }

  /// <summary>
  /// Initializes the bullet's starting height.
  /// </summary>
  /// <param name="startHeight">The height from which the bullet will start falling.</param>
  public void Initialize(float startHeight) {
    _currentHeight = startHeight;
    _startHeight = startHeight;
    _verticalVelocity = 0;
  }

  public override void _Process(double delta) {
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    switch (_currentState) {
      case State.Falling:
        // 应用重力
        _verticalVelocity -= GravityZ * scaledDelta;
        _currentHeight += _verticalVelocity * scaledDelta;

        UpdateLandingIndicator();

        // 检查是否「落地」
        if (_currentHeight <= CollisionActivationHeight) {
          _currentHeight = 0;
          _currentState = State.Landed;

          if (_collisionShape != null) {
            _collisionShape.Disabled = false;
          }

          // 落地后隐藏指示器
          if (_landingIndicator != null) {
            _landingIndicator.Visible = false;
          }

          var timer = GetTree().CreateTimer(LifetimeOnGround);
          timer.Timeout += QueueFree;
        }
        break;

      case State.Landed:
        // 逻辑由落地时设置的计时器处理
        break;
    }

    // 必须调用基类的 _Process 来更新 3D 可视化对象
    base._Process(delta);
  }

  /// <summary>
  /// 根据当前高度更新指示器的外观
  /// </summary>
  private void UpdateLandingIndicator() {
    if (_landingIndicator == null || _startHeight <= 0) {
      return;
    }

    // 计算一个从 0.0 到 1.0 的进度值，表示下落了多少
    // Clamp 确保值在范围内，避免除零或负数等问题
    float fallProgress = Mathf.Clamp(1.0f - (_currentHeight / _startHeight), 0.0f, 1.0f);

    // 根据下落进度，让指示器从一个较小尺寸线性插值到最终尺寸（1.0）
    // 例如，从 20% 大小变到 100% 大小
    float initialScale = 0.2f;
    float finalScale = 1.0f;
    float currentScale = Mathf.Lerp(initialScale, finalScale, fallProgress);
    _landingIndicator.Scale = new Vector3(currentScale, currentScale, currentScale);

    // 你也可以改变透明度
    // _landingIndicator.Modulate = new Color(1, 1, 1, fallProgress);
  }

  /// <summary>
  /// Overrides the default visualizer update to incorporate the Z-axis (Y in 3D) height.
  /// </summary>
  protected override void UpdateVisualizer() {
    if (_visualizer != null) {
      var position3D = new Vector3(
        GlobalPosition.X * GameConstants.WorldScaleFactor,
        GameConstants.GamePlaneY,
        GlobalPosition.Y * GameConstants.WorldScaleFactor
      );

      _landingIndicator.GlobalPosition = position3D;

      position3D.Y += _currentHeight * GameConstants.WorldScaleFactor;
      _visualizer.GlobalPosition = position3D;

      _visualizer.Rotation = new Vector3(0, 0, -GlobalRotation);
    }
  }
}
