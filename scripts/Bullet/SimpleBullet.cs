using Godot;
using Rewind;

namespace Bullet;

public class SimpleBulletState : BaseBulletState {
  public Vector2 Velocity;
  public float AngularVelocity;
  public float TimeAlive;
}

public partial class SimpleBullet : BaseBullet {
  [ExportGroup("Movement")]
  [Export]
  public float InitialSpeed { get; set; } = 300.0f;
  [Export]
  public float MaxSpeed { get; set; } = -1.0f; // 负数表示无限制
  [Export]
  public float SameDirectionAcceleration { get; set; } = 0.0f;
  [Export]
  public Vector2 Acceleration { get; set; } = Vector2.Zero;

  [ExportGroup("Rotation")]
  [Export]
  public float AngularVelocity { get; set; } = 0.0f; // 角速度 (弧度/秒)
  [Export]
  public float AngularAcceleration { get; set; } = 0.0f; // 角加速度

  [ExportGroup("Time")]
  [Export]
  public float MaxLifetime { get; set; } = 10.0f;
  [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
  public float TimeScaleSensitivity { get; set; } = 1.0f; // 时间缩放敏感度．0=完全忽略, 1=完全受影响．

  public Vector2 Velocity { get; set; }
  protected float _timeAlive = 0.0f;

  // 用于边界检查的变量
  protected Rect2 _despawnBounds;
  protected bool _boundsInitialized = false;

  public override void _Ready() {
    base._Ready();
    // 基于初始方向和速度设置初始速度向量
    Velocity = Vector2.Right.Rotated(Rotation) * InitialSpeed;
    // 初始化销毁边界
    InitializeDespawnBounds();
    UpdateVisualizer();
  }

  /// <summary>
  /// 获取 MapGenerator 并计算销毁边界．
  /// </summary>
  private void InitializeDespawnBounds() {
    // 尝试从场景树中获取 MapGenerator 节点
    var mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (mapGenerator != null) {
      float worldWidth = mapGenerator.MapWidth * mapGenerator.TileSize;
      float worldHeight = mapGenerator.MapHeight * mapGenerator.TileSize;

      // 地图中心是 (0,0)，所以边界是半宽/半高
      float halfWidth = worldWidth / 2.0f;
      float halfHeight = worldHeight / 2.0f;

      // 创建一个比地图大 1.5 倍的销毁矩形区域
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
      // 如果找不到 MapGenerator，打印一个错误，但不影响游戏运行
      GD.PrintErr("SimpleBullet: MapGenerator not found at 'GameRoot/MapGenerator'. Off-screen despawn check will be disabled.");
    }
  }

  public override void _Process(double delta) {
    base._Process(delta);

    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;

    // --- Lifetime Check ---
    _timeAlive += scaledDelta;
    if (_timeAlive > MaxLifetime) {
      Destroy();
      return;
    }

    // --- Update Velocity & Position ---
    Velocity += Acceleration * scaledDelta;
    if (!Velocity.IsZeroApprox()) {
      Velocity += Velocity.Normalized() * SameDirectionAcceleration * scaledDelta;
    }
    if (MaxSpeed > 0) {
      var length = Velocity.Length();
      if (length > MaxSpeed) {
        Velocity = Velocity * (MaxSpeed / length);
      }
    }
    Position += Velocity * scaledDelta;

    // --- Update Angular Velocity & Rotation ---
    AngularVelocity += AngularAcceleration * scaledDelta;
    Rotation += AngularVelocity * scaledDelta;

    // 边界检查
    // 如果边界已初始化，并且子弹的全局位置不在边界矩形内
    if (_boundsInitialized && !_despawnBounds.HasPoint(GlobalPosition)) {
      Destroy(); // 使用 Destroy
      return;
    }

    UpdateVisualizer();
  }

  public override RewindState CaptureState() {
    var baseState = (BaseBulletState) base.CaptureState();
    return new SimpleBulletState {
      GlobalPosition = baseState.GlobalPosition,
      GlobalRotation = baseState.GlobalRotation,
      WasGrazed = baseState.WasGrazed,
      Modulate = baseState.Modulate,
      Velocity = this.Velocity,
      AngularVelocity = this.AngularVelocity,
      TimeAlive = this._timeAlive
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not SimpleBulletState sbs) return;
    this.Velocity = sbs.Velocity;
    this.AngularVelocity = sbs.AngularVelocity;
    this._timeAlive = sbs.TimeAlive;
  }
}
