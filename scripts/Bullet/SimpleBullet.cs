using System;
using Godot;

namespace Bullet;

public partial class SimpleBullet : BaseBullet {
  // 用于 update lambda 的返回值
  public struct UpdateState {
    public UpdateState() { }

    public Vector3 position = Vector3.Zero;
    public Vector3 rotation = Vector3.Zero;
    public Color modulate = Colors.White;
    public bool destroy = false; // 是否要在这一帧之后标记为销毁
  }

  [ExportGroup("Movement")]
  [Export] public bool EnableBorderCheck { get; set; } = true;

  public Func<float, UpdateState> UpdateFunc;

  protected Rect2 _despawnBounds;
  protected bool _boundsInitialized = false;

  public override void _Ready() {
    InitializeDespawnBounds();
    base._Ready();
  }

  /// <summary>
  /// 获取 MapGenerator 并计算销毁边界．
  /// </summary>
  private void InitializeDespawnBounds() {
    var mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (mapGenerator == null) {
      GD.PrintErr("SimpleBullet: MapGenerator not found at 'GameRoot/MapGenerator'. Off-screen despawn check will be disabled.");
      return;
    }

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
  }

  public override void UpdateBullet(float scaledDelta) {
    base.UpdateBullet(scaledDelta);

    UpdateState state = UpdateFunc(TimeAlive);
    GlobalPosition = state.position;
    GlobalRotation = state.rotation;
    _sprite.Modulate = state.modulate;
    if (state.destroy) {
      Destroy();
      return;
    }

    if (EnableBorderCheck && _boundsInitialized && !_despawnBounds.HasPoint(new Vector2(GlobalPosition.X, GlobalPosition.Z))) {
      Destroy();
      return;
    }
  }
}
