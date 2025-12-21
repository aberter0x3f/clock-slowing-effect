using Godot;
using Rewind;

namespace Bullet;

public class PhaseLiquidCrystalBulletState : BaseBulletState {
  public Vector3 CurrentOffset;
  public float BrownianTimer;
  public Vector3 BrownianDirection;
}

public partial class PhaseLiquidCrystalBullet : BaseBullet {
  [ExportGroup("Liquid Crystal Properties")]
  [Export] public float PlayerInfluenceConstant { get; set; } = 4.0f;
  [Export] public float BrownianMotionRadius { get; set; } = 0.15f;
  [Export] public float BrownianMotionSpeed { get; set; } = 0.1f;
  [Export] public float BrownianMotionChangeInterval { get; set; } = 0.33f;
  [Export] public float BossInfluence { get; set; } = 0.5f;

  public Vector3 BossVelocity { get; set; }

  private Player _player;
  private Vector3 _currentOffset;
  private Vector3 _brownianDirection;
  private float _brownianTimer;
  private float _sigma;
  private Rect2 _bounds;

  public override void _Ready() {
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    _sigma = (float) GD.Randfn(0, Mathf.DegToRad(5.0f));
    BrownianMotionChangeInterval += (float) GD.Randfn(0, BrownianMotionChangeInterval / 4);
    PickNewBrownianDirection();
    base._Ready();
  }

  public void SetBounds(Rect2 bounds) { _bounds = bounds; }

  public override void UpdateBullet(float scaledDelta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    // 1. 布朗运动 (在 XZ 平面)
    _brownianTimer -= scaledDelta;
    if (_brownianTimer <= 0) PickNewBrownianDirection();

    GlobalPosition -= _currentOffset;
    _currentOffset += _brownianDirection * BrownianMotionSpeed * scaledDelta;
    if (_currentOffset.LengthSquared() > BrownianMotionRadius * BrownianMotionRadius) {
      _currentOffset = _currentOffset.Normalized() * BrownianMotionRadius;
    }
    GlobalPosition += _currentOffset;

    // 2. Boss 影响
    GlobalPosition += BossVelocity * BossInfluence * scaledDelta;

    // 3. 屏幕环绕 (XZ 平面)
    ApplyWrapping();

    // 4. 更新旋转 (朝向加权目标)
    UpdateRotation();
  }

  private void ApplyWrapping() {
    Vector3 pos = GlobalPosition;
    if (pos.X > _bounds.End.X) pos.X -= _bounds.Size.X;
    else if (pos.X < _bounds.Position.X) pos.X += _bounds.Size.X;

    if (pos.Z > _bounds.End.Y) pos.Z -= _bounds.Size.Y;
    else if (pos.Z < _bounds.Position.Y) pos.Z += _bounds.Size.Y;

    GlobalPosition = pos;
  }

  private void UpdateRotation() {
    var target = _player.DecoyTarget ?? _player;
    if (!IsInstanceValid(target)) return;

    // alpha: Boss 速度方向 (XZ 平面)
    float alpha = BossVelocity.LengthSquared() < 0.001f ? -Mathf.Pi / 2 : Mathf.Atan2(BossVelocity.Z, BossVelocity.X);
    // beta: 指向玩家的方向
    Vector3 toPlayer = target.GlobalPosition - GlobalPosition;
    float beta = Mathf.Atan2(toPlayer.Z, toPlayer.X);

    float distSq = toPlayer.LengthSquared();
    float k = Mathf.Clamp(PlayerInfluenceConstant / (distSq + 0.01f), 0.0f, 1.0f);

    float targetAngle = Mathf.LerpAngle(alpha, beta, k) + _sigma;
    Rotation = new Vector3(0, -targetAngle, 0); // 3D Y 轴旋转
  }

  private void PickNewBrownianDirection() {
    _brownianTimer = BrownianMotionChangeInterval;
    float angle = GD.Randf() * Mathf.Tau;
    _brownianDirection = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle));
  }

  public override RewindState CaptureState() {
    var bs = (BaseBulletState) base.CaptureState();
    return new PhaseLiquidCrystalBulletState {
      GlobalPosition = bs.GlobalPosition,
      GlobalRotation = bs.GlobalRotation,
      WasGrazed = bs.WasGrazed,
      IsGrazing = bs.IsGrazing,
      Modulate = bs.Modulate,
      TimeAlive = bs.TimeAlive,
      CurrentOffset = _currentOffset,
      BrownianTimer = _brownianTimer,
      BrownianDirection = _brownianDirection
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not PhaseLiquidCrystalBulletState s) return;
    _currentOffset = s.CurrentOffset; _brownianTimer = s.BrownianTimer; _brownianDirection = s.BrownianDirection;
  }
}
