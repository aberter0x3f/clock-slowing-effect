using Godot;
using Rewind;

namespace Bullet;

public class PhaseLiquidCrystalBulletState : BaseBulletState {
  public Vector2 CurrentOffset;
  public float BrownianTimer;
}

public partial class PhaseLiquidCrystalBullet : BaseBullet {
  [ExportGroup("Liquid Crystal Properties")]
  [Export]
  public float PlayerInfluenceConstant { get; set; } = 40000f; // 用于计算 k 值的常数
  [Export]
  public float BrownianMotionRadius { get; set; } = 15f; // 布朗运动的最大半径
  [Export]
  public float BrownianMotionSpeed { get; set; } = 10f; // 布朗运动的速度
  [Export]
  public float BrownianMotionChangeInterval { get; set; } = 0.33f; // 布朗运动改变方向的间隔

  public float TimeScaleSensitivity { get; set; } = 1f;
  public float BossInfluence { get; set; }
  public Vector2 BossVelocity { get; set; }

  private Player _player;
  private Vector2 _currentOffset;
  private Vector2 _brownianDirection;
  private float _brownianTimer;
  private float _sigma; // 独立随机自旋偏好
  private readonly RandomNumberGenerator _rnd = new();
  private Rect2 _bounds;

  public override void _Ready() {
    base._Ready();
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    _sigma = _rnd.Randfn(0, Mathf.DegToRad(5.0f));
    BrownianMotionChangeInterval += _rnd.Randfn(0, BrownianMotionChangeInterval / 4);
    PickNewBrownianDirection();
    UpdateRotation();
    UpdateVisualizer();
  }

  public void SetBounds(Rect2 bounds) {
    _bounds = bounds;
  }

  public override void _Process(double delta) {
    base._Process(delta);

    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;

    UpdateBrownianMotion(scaledDelta);
    UpdateBossInfluence(scaledDelta);
    UpdateRotation();
    ApplyWrapping();
    UpdateVisualizer();
  }

  private void UpdateBrownianMotion(float scaledDelta) {
    _brownianTimer -= scaledDelta;
    if (_brownianTimer <= 0) {
      PickNewBrownianDirection();
    }
    GlobalPosition -= _currentOffset;
    _currentOffset += _brownianDirection * BrownianMotionSpeed * scaledDelta;
    if (_currentOffset.LengthSquared() > BrownianMotionRadius * BrownianMotionRadius) {
      _currentOffset = _currentOffset.Normalized() * BrownianMotionRadius;
    }
    GlobalPosition += _currentOffset;
  }

  private void UpdateBossInfluence(float scaledDelta) {
    GlobalPosition += BossVelocity * BossInfluence * scaledDelta;
  }

  private void ApplyWrapping() {
    if (_bounds.Size.IsZeroApprox()) return;

    Vector2 newPos = GlobalPosition;
    bool wrapped = false;

    if (newPos.X > _bounds.End.X) {
      newPos.X -= _bounds.Size.X;
      wrapped = true;
    } else if (newPos.X < _bounds.Position.X) {
      newPos.X += _bounds.Size.X;
      wrapped = true;
    }

    if (newPos.Y > _bounds.End.Y) {
      newPos.Y -= _bounds.Size.Y;
      wrapped = true;
    } else if (newPos.Y < _bounds.Position.Y) {
      newPos.Y += _bounds.Size.Y;
      wrapped = true;
    }

    if (wrapped) {
      GlobalPosition = newPos;
    }
  }

  private void UpdateRotation() {
    var target = _player.DecoyTarget ?? _player;
    if (!IsInstanceValid(target)) return;

    // 默认旋转角度
    float alpha = BossVelocity.IsZeroApprox() ? Mathf.Pi / 2 : BossVelocity.Angle();
    // 指向目标的角度
    float beta = GlobalPosition.DirectionTo(target.GlobalPosition).Angle();

    // 计算 k 值
    float distSqToTarget = GlobalPosition.DistanceSquaredTo(target.GlobalPosition);
    float k = Mathf.Clamp(PlayerInfluenceConstant / (distSqToTarget + 1.0f), 0.0f, 1.0f);

    // 计算最终旋转角度
    // LerpAngle 确保在角度环上正确插值
    float targetAngle = Mathf.LerpAngle(alpha, beta, k);
    GlobalRotation = targetAngle + _sigma;
  }

  private void PickNewBrownianDirection() {
    _brownianTimer = BrownianMotionChangeInterval;
    _brownianDirection = Vector2.Right.Rotated(_rnd.Randf() * Mathf.Tau);
  }

  public override RewindState CaptureState() {
    var baseState = (BaseBulletState) base.CaptureState();
    return new PhaseLiquidCrystalBulletState {
      GlobalPosition = baseState.GlobalPosition,
      GlobalRotation = baseState.GlobalRotation,
      WasGrazed = baseState.WasGrazed,
      Modulate = baseState.Modulate,
      CurrentOffset = this._currentOffset,
      BrownianTimer = this._brownianTimer
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not PhaseLiquidCrystalBulletState lcbs) return;
    this._currentOffset = lcbs.CurrentOffset;
    this._brownianTimer = lcbs.BrownianTimer;
  }
}
