using Godot;
using Rewind;

namespace Bullet;

public class BaseBullet3DState : BaseBulletState {
  public Vector3 RawPosition;
  public float TimeAlive;
  public bool LandingIndicatorVisible;
  public Vector3 LandingIndicatorScale;
}

public abstract partial class BaseBullet3D : BaseBullet {
  [ExportGroup("3D Movement")]
  [Export]
  public Vector3 RawPosition { get; set; }
  [Export]
  public float MinZ { get; set; } = -100.0f; // 如果 Z 低于此值则销毁
  [Export]
  public float CollisionHeight { get; set; } = 5.0f; // 在此 Z 距离内激活碰撞

  [ExportGroup("Lifetime")]
  [Export]
  public float MaxLifetime { get; set; } = 10.0f;

  [ExportGroup("Indicator")]
  [Export]
  public float IndicatorStartHeight { get; set; } = 400.0f; // 指示器开始放大的 Z 轴高度．
  [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
  public float IndicatorMinScale { get; set; } = 0.2f; // 指示器的最小缩放比例．

  [ExportGroup("Time")]
  [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
  public float TimeScaleSensitivity { get; set; } = 1.0f; // 时间缩放敏感度．0=完全忽略, 1=完全受影响．

  protected float _timeAlive = 0.0f;
  protected Rect2 _despawnBounds;
  protected bool _boundsInitialized = false;
  protected CollisionShape2D _collisionShape;
  protected Node3D _landingIndicator;
  protected bool _hasIndicator = false;

  public override void _Ready() {
    base._Ready();
    _collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
    _landingIndicator = GetNodeOrNull<Node3D>("LandingIndicator");
    _hasIndicator = IsInstanceValid(_landingIndicator);

    GlobalPosition = new Vector2(RawPosition.X, RawPosition.Y);

    InitializeDespawnBounds();

    if (_hasIndicator) {
      UpdateLandingIndicator();
    }
    UpdateVisualizer();
  }

  private void InitializeDespawnBounds() {
    var mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (mapGenerator != null) {
      float worldWidth = mapGenerator.MapWidth * mapGenerator.TileSize;
      float worldHeight = mapGenerator.MapHeight * mapGenerator.TileSize;
      float halfWidth = worldWidth / 2.0f;
      float halfHeight = worldHeight / 2.0f;
      float despawnHalfWidth = halfWidth * 1.5f;
      float despawnHalfHeight = halfHeight * 1.5f;
      _despawnBounds = new Rect2(
        -despawnHalfWidth,
        -despawnHalfHeight,
        despawnHalfWidth * 2,
        despawnHalfHeight * 2
      );
      _boundsInitialized = true;
    } else {
      GD.PrintErr("Bullet3D: MapGenerator not found. Off-screen despawn check will be disabled.");
    }
  }

  protected abstract void UpdatePosition(float scaledDelta);

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing) {
      // 在预览时也更新指示器，以确保视觉效果正确
      if (_hasIndicator) UpdateLandingIndicator();
      UpdateVisualizer();
      return;
    }
    if (RewindManager.Instance.IsRewinding) return;

    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;

    // Lifetime Check
    _timeAlive += scaledDelta;
    if (_timeAlive > MaxLifetime) {
      Destroy();
      return;
    }

    UpdatePosition(scaledDelta);

    // 更新 2D 位置以用于碰撞和其他 2D 系统
    this.GlobalPosition = new Vector2(RawPosition.X, RawPosition.Y);

    // Despawn Checks
    if (RawPosition.Z < MinZ) {
      Destroy();
      return;
    }
    if (_boundsInitialized && !_despawnBounds.HasPoint(this.GlobalPosition)) {
      Destroy();
      return;
    }

    // Collision Check
    _collisionShape.Disabled = Mathf.Abs(RawPosition.Z) >= CollisionHeight;

    // 更新指示器
    if (_hasIndicator) {
      UpdateLandingIndicator();
    }

    UpdateVisualizer();
  }

  protected void UpdateLandingIndicator() {
    if (!_hasIndicator) return;

    // 只有当子弹在游戏平面上方时，指示器才可见
    _landingIndicator.Visible = RawPosition.Z > CollisionHeight;
    if (!_landingIndicator.Visible) return;

    // 计算从 IndicatorStartHeight 到 0 的下落进度．
    // 当 Z >= IndicatorStartHeight 时，进度为 0；当 Z <= CollisionHeight 时，进度为 1．
    float progress = 1.0f - Mathf.Clamp((RawPosition.Z - CollisionHeight) / (IndicatorStartHeight - CollisionHeight), 0.0f, 1.0f);

    // 根据进度在最小和最大 (1.0) 缩放之间进行插值
    float currentScale = Mathf.Lerp(IndicatorMinScale, 1.0f, progress);
    _landingIndicator.Scale = new Vector3(currentScale, currentScale, currentScale);

    // 将指示器放置在游戏平面上，与子弹的 XY 坐标对齐
    _landingIndicator.GlobalPosition = new Vector3(
      RawPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY,
      RawPosition.Y * GameConstants.WorldScaleFactor
    );
  }

  protected override void UpdateVisualizer() {
    if (_visualizer != null) {
      _visualizer.GlobalPosition = new Vector3(
        RawPosition.X * GameConstants.WorldScaleFactor,
        GameConstants.GamePlaneY + RawPosition.Z * GameConstants.WorldScaleFactor,
        RawPosition.Y * GameConstants.WorldScaleFactor
      );
    }
  }

  public override RewindState CaptureState() {
    var baseState = (BaseBulletState) base.CaptureState();
    return new SimpleBullet3DState {
      GlobalPosition = baseState.GlobalPosition,
      GlobalRotation = baseState.GlobalRotation,
      WasGrazed = baseState.WasGrazed,
      Modulate = baseState.Modulate,
      RawPosition = this.RawPosition,
      TimeAlive = this._timeAlive,
      // 捕获指示器状态
      LandingIndicatorVisible = _hasIndicator && _landingIndicator.Visible,
      LandingIndicatorScale = _hasIndicator ? _landingIndicator.Scale : Vector3.One
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not BaseBullet3DState b3s) return;
    this.RawPosition = b3s.RawPosition;
    this._timeAlive = b3s.TimeAlive;
    // 恢复指示器状态
    if (_hasIndicator) {
      _landingIndicator.Visible = b3s.LandingIndicatorVisible;
      _landingIndicator.Scale = b3s.LandingIndicatorScale;
    }
  }

  public override void Destroy() {
    if (IsDestroyed) return;
    base.Destroy();
    // 销毁时隐藏指示器
    if (_hasIndicator) {
      _landingIndicator.Visible = false;
    }
  }

  public override void Resurrect() {
    if (!IsDestroyed) return;
    base.Resurrect();
    // 复活时，指示器的可见性将由 RestoreState 恢复
  }
}
