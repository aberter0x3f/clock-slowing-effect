using Godot;
using Rewind;

namespace Bullet;

public class FallingBulletState : BaseBulletState {
  public FallingBullet.State CurrentState;
  public float CurrentHeight;
  public float VerticalVelocity;
  public bool LandingIndicatorVisible;
  public Vector3 LandingIndicatorScale;
  public float TimeOnGround;
}

/// <summary>
/// Represents a bullet that "falls" from a certain height in 3D space.
/// It only activates its 2D collision shape when it gets close to the ground plane (Z=0).
/// It now includes a landing indicator for better player feedback.
/// </summary>
public partial class FallingBullet : BaseBullet {
  public enum State {
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
  private float _timeOnGround;

  [ExportGroup("Falling Behavior")]
  [Export]
  public float GravityZ { get; set; } = 300.0f;
  [Export]
  public float GravitySigma { get; set; } = 100.0f;
  [Export]
  public float CollisionActivationHeight { get; set; } = 0.3f;
  [Export]
  public float LifetimeOnGround { get; set; } = 0.5f;

  public override void _Ready() {
    base._Ready();

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

    UpdateLandingIndicator();
    UpdateVisualizer();
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
    base._Process(delta);

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    switch (_currentState) {
      case State.Falling:
        // 应用重力
        _verticalVelocity -= GravityZ * scaledDelta;
        _currentHeight += _verticalVelocity * scaledDelta;

        UpdateLandingIndicator();

        // 检查是否「落地」
        if (_currentHeight <= CollisionActivationHeight + GameConstants.GamePlaneY) {
          _currentHeight = 0;
          _currentState = State.Landed;

          if (_collisionShape != null) {
            _collisionShape.Disabled = false;
          }

          // 落地后隐藏指示器
          if (_landingIndicator != null) {
            _landingIndicator.Visible = false;
          }

          _timeOnGround = LifetimeOnGround;
        }
        break;

      case State.Landed:
        _timeOnGround -= scaledDelta;
        if (_timeOnGround <= 0) {
          Destroy();
          return;
        }
        break;
    }

    UpdateVisualizer();
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
  }

  /// <summary>
  /// Overrides the default visualizer update to incorporate the Z-axis (Y in 3D) height.
  /// </summary>
  protected override void UpdateVisualizer() {
    var position3D = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );

    if (_landingIndicator != null) {
      _landingIndicator.GlobalPosition = position3D;
    }

    if (_visualizer != null) {
      position3D.Y += _currentHeight * GameConstants.WorldScaleFactor;
      _visualizer.GlobalPosition = position3D;

      _visualizer.Rotation = new Vector3(0, 0, -GlobalRotation);
    }
  }

  public override RewindState CaptureState() {
    var baseState = (BaseBulletState) base.CaptureState();
    return new FallingBulletState {
      GlobalPosition = baseState.GlobalPosition,
      GlobalRotation = baseState.GlobalRotation,
      WasGrazed = baseState.WasGrazed,
      Modulate = baseState.Modulate,
      CurrentState = this._currentState,
      CurrentHeight = this._currentHeight,
      VerticalVelocity = this._verticalVelocity,
      LandingIndicatorVisible = _landingIndicator?.Visible ?? false,
      LandingIndicatorScale = _landingIndicator?.Scale ?? Vector3.One,
      TimeOnGround = this._timeOnGround,
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not FallingBulletState fbs) return;
    this._currentState = fbs.CurrentState;
    this._currentHeight = fbs.CurrentHeight;
    this._verticalVelocity = fbs.VerticalVelocity;
    if (_landingIndicator != null) {
      _landingIndicator.Visible = fbs.LandingIndicatorVisible;
      _landingIndicator.Scale = fbs.LandingIndicatorScale;
    }
    // 恢复碰撞体状态
    if (_collisionShape != null) {
      _collisionShape.Disabled = _currentState == State.Falling;
    }
    this._timeOnGround = fbs.TimeOnGround;
  }


  public override void Destroy() {
    base.Destroy();
    if (IsDestroyed) return;
    _landingIndicator.Visible = false;
  }

  public override void Resurrect() {
    base.Resurrect();
    if (!IsDestroyed) return;
    _landingIndicator.Visible = true;
  }
}
