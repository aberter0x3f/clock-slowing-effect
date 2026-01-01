using Godot;
using Rewind;

namespace Bullet;

public class PhaseInvisibleBulletState : BaseBulletState {
  public bool IsRevealed;
  public float CurrentSpeed;
}

public partial class PhaseInvisibleBullet : BaseBullet {
  private Player _player;

  [Export] public Vector3 InitialPosition { get; set; }
  [Export] public Vector3 InitialVelocity { get; set; }
  [Export] public float GravityY { get; set; } = 6.0f;
  [Export] public float FadeDuration { get; set; } = 1f;
  [Export] public float TriggerDistance { get; set; } = 0.8f;
  [Export] public float Acceleration { get; set; } = 2.0f;

  private bool _isRevealed = false;
  private float _currentSpeed;
  private float _targetSpeed;
  private Vector3 _moveDirection;
  protected Rect2 _despawnBounds;
  protected bool _boundsInitialized = false;

  public override void _Ready() {
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    _targetSpeed = InitialVelocity.Length();
    _moveDirection = InitialVelocity.Normalized();
    _currentSpeed = _targetSpeed;
    InitializeDespawnBounds();
    GlobalPosition = InitialPosition;
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

    UpdateMovement(scaledDelta);
    if (!_isRevealed) {
      float alpha = Mathf.Max(0.0f, 1.0f - (TimeAlive / FadeDuration));
      _sprite.Modulate = _sprite.Modulate with { A = alpha };
      CheckTrigger();
    } else {
      _currentSpeed = Mathf.MoveToward(_currentSpeed, _targetSpeed, Acceleration * scaledDelta);
    }

    if (_boundsInitialized && !_despawnBounds.HasPoint(new Vector2(GlobalPosition.X, GlobalPosition.Z))) {
      Destroy();
      return;
    }
  }

  private void UpdateMovement(float scaledDelta) {
    float t = TimeAlive;
    // 垂直方向：模拟抛物线
    float y = Mathf.Max(0, InitialPosition.Y - (0.5f * GravityY * t * t));

    if (!_isRevealed) {
      GlobalPosition += InitialVelocity * scaledDelta;
    } else {
      GlobalPosition += _moveDirection * _currentSpeed * scaledDelta;
    }

    GlobalPosition = GlobalPosition with { Y = y };
  }

  private void CheckTrigger() {
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    var target = _player.DecoyTarget ?? _player;
    if (GlobalPosition.DistanceTo(target.GlobalPosition) <= TriggerDistance) {
      Reveal();
    }
  }

  private void Reveal() {
    _isRevealed = true;
    _currentSpeed = _targetSpeed * 0.1f;
  }

  public override RewindState CaptureState() {
    var bs = (BaseBulletState) base.CaptureState();
    return new PhaseInvisibleBulletState {
      GlobalPosition = bs.GlobalPosition,
      GlobalRotation = bs.GlobalRotation,
      WasGrazed = bs.WasGrazed,
      IsGrazing = bs.IsGrazing,
      Modulate = bs.Modulate,
      TimeAlive = bs.TimeAlive,
      IsRevealed = _isRevealed,
      CurrentSpeed = _currentSpeed
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not PhaseInvisibleBulletState s) return;
    _isRevealed = s.IsRevealed;
    _currentSpeed = s.CurrentSpeed;
  }
}
