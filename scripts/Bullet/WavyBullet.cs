using Godot;

namespace Bullet;

public partial class WavyBullet : BaseBullet {
  [ExportGroup("Wavy Movement")]
  [Export]
  public float ForwardSpeed { get; set; } = 250.0f; // 沿主轴前进的速度
  [Export]
  public float Amplitude { get; set; } = 50.0f; // 振幅，即偏离中心的距离
  [Export]
  public float Frequency { get; set; } = 1.0f; // 频率，即每秒完成多少次振荡
  [Export]
  public bool InvertSine { get; set; } = false; // 用于创建镜像弹道（DNA 的另一条链）

  [ExportGroup("Lifetime")]
  [Export]
  public float MaxLifetime { get; set; } = 10.0f;

  private float _timeAlive = 0.0f;
  private Vector2 _initialPosition;
  private Vector2 _forwardDirection;
  private Vector2 _perpendicularDirection;

  // 用于边界检查的变量
  private Rect2 _despawnBounds;
  private bool _boundsInitialized = false;

  public override void _Ready() {
    base._Ready();
    // 记录初始状态，后续所有计算都基于此，以避免浮点误差累积
    _initialPosition = GlobalPosition;
    _forwardDirection = Vector2.Right.Rotated(GlobalRotation);
    _perpendicularDirection = _forwardDirection.Rotated(Mathf.Pi / 2.0f);

    // 初始化销毁边界
    InitializeDespawnBounds();
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
      GD.PrintErr("WavyBullet: MapGenerator not found at 'GameRoot/MapGenerator'. Off-screen despawn check will be disabled.");
    }
  }

  public override void _Process(double delta) {
    base._Process(delta);

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    _timeAlive += scaledDelta;
    if (_timeAlive > MaxLifetime) {
      QueueFree();
      return;
    }

    // 1. 计算沿中心轴前进的位置
    Vector2 centerPosition = _initialPosition + _forwardDirection * ForwardSpeed * _timeAlive;

    // 2. 计算垂直于中心轴的正弦偏移
    // Mathf.Tau 是 2 * PI，这样频率就代表每秒的周期数
    float sineOffset = Amplitude * Mathf.Sin(_timeAlive * Frequency * Mathf.Tau);
    if (InvertSine) {
      sineOffset *= -1;
    }
    Vector2 offset = _perpendicularDirection * sineOffset;

    // 3. 设置最终位置
    GlobalPosition = centerPosition + offset;

    // 边界检查
    // 如果边界已初始化，并且子弹的全局位置不在边界矩形内
    if (_boundsInitialized && !_despawnBounds.HasPoint(GlobalPosition)) {
      QueueFree();
      return;
    }

    // 子弹的朝向也随着波浪路径变化
    Vector2 currentVelocity = (_forwardDirection * ForwardSpeed) + (_perpendicularDirection * Amplitude * Frequency * Mathf.Tau * Mathf.Cos(_timeAlive * Frequency * Mathf.Tau));
    GlobalRotation = currentVelocity.Angle();
  }
}
