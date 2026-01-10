using Godot;
using Rewind;

namespace Bullet;

public class PhaseStellarSpecialBulletState : BaseBulletState {
  public bool HasSlowed;
  public float CurrentSpeed;
}

[GlobalClass]
public partial class PhaseStellarSpecialBullet : BaseBullet {
  public float BaseSpeed { get; set; }
  public float Acceleration { get; set; }
  public Vector3 Direction { get; set; }

  private bool _hasSlowed = false;
  private float _currentSpeed;
  private Player _player;
  protected Rect2 _despawnBounds;

  public override void _Ready() {
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    _currentSpeed = BaseSpeed;
    GlobalPosition = Vector3.Zero; // 从原点出发
    InitializeDespawnBounds();
    base._Ready();
  }

  public override void UpdateBullet(float scaledDelta) {
    base.UpdateBullet(scaledDelta);

    var target = _player.DecoyTarget ?? _player;
    // 移动
    GlobalPosition += Direction * _currentSpeed * scaledDelta;
    GlobalPosition = GlobalPosition with { Y = target.GlobalPosition.Y };

    // 逻辑检查
    if (!_hasSlowed) {
      // 比较平面距离 (忽略 Y 轴)
      Vector2 pPos2 = new Vector2(target.GlobalPosition.X, target.GlobalPosition.Z);
      Vector2 bPos2 = new Vector2(GlobalPosition.X, GlobalPosition.Z);

      // 当子弹距离原点的距离 >= 玩家距离原点的距离时触发
      if (bPos2.Length() + 1f >= pPos2.Length()) {
        _hasSlowed = true;
        _currentSpeed = BaseSpeed * 0.1f; // 变成 1/10 速度
      }
    } else {
      // 触发后开始加速恢复
      if (_currentSpeed < BaseSpeed) {
        _currentSpeed += Acceleration * scaledDelta;
        if (_currentSpeed > BaseSpeed) _currentSpeed = BaseSpeed;
      }
    }

    if (!_despawnBounds.HasPoint(new Vector2(GlobalPosition.X, GlobalPosition.Z))) {
      Destroy();
      return;
    }
  }

  /// <summary>
  /// 获取 MapGenerator 并计算销毁边界．
  /// </summary>
  private void InitializeDespawnBounds() {
    var mapGenerator = GetTree().Root.GetNode<MapGenerator>("GameRoot/MapGenerator");

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
  }

  public override RewindState CaptureState() {
    var bs = (BaseBulletState) base.CaptureState();
    return new PhaseStellarSpecialBulletState {
      GlobalPosition = bs.GlobalPosition,
      GlobalRotation = bs.GlobalRotation,
      WasGrazed = bs.WasGrazed,
      IsGrazing = bs.IsGrazing,
      Modulate = bs.Modulate,
      TimeAlive = bs.TimeAlive,
      HasSlowed = _hasSlowed,
      CurrentSpeed = _currentSpeed,
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not PhaseStellarSpecialBulletState s) return;
    _hasSlowed = s.HasSlowed;
    _currentSpeed = s.CurrentSpeed;
  }
}
