using Godot;
using Rewind;

namespace Bullet;

public class SimpleBullet3DState : BaseBullet3DState {
  public Vector3 Velocity;
}

public partial class SimpleBullet3D : BaseBullet3D {
  [ExportGroup("3D Movement")]
  [Export]
  public Vector3 Velocity { get; set; }
  [Export]
  public Vector3 Acceleration { get; set; } = Vector3.Zero;
  [Export]
  public float SameDirectionAcceleration { get; set; } = 0.0f;
  [Export]
  public float MaxSpeedXY { get; set; } = -1.0f; // 负数表示无限制

  protected override void UpdatePosition(float scaledDelta) {
    // Update Velocity & Position
    Velocity += Acceleration * scaledDelta;
    if (!Velocity.IsZeroApprox()) {
      Velocity += Velocity.Normalized() * SameDirectionAcceleration * scaledDelta;
    }
    if (MaxSpeedXY > 0) {
      var velocityXY = new Vector2(Velocity.X, Velocity.Y);
      var length = velocityXY.Length();
      if (length > MaxSpeedXY) {
        Velocity *= (MaxSpeedXY / length);
      }
    }
    RawPosition += Velocity * scaledDelta;
  }

  public override RewindState CaptureState() {
    var baseState = (BaseBullet3DState) base.CaptureState();
    return new SimpleBullet3DState {
      GlobalPosition = baseState.GlobalPosition,
      GlobalRotation = baseState.GlobalRotation,
      WasGrazed = baseState.WasGrazed,
      Modulate = baseState.Modulate,
      RawPosition = baseState.RawPosition,
      TimeAlive = baseState.TimeAlive,
      LandingIndicatorVisible = baseState.LandingIndicatorVisible,
      LandingIndicatorScale = baseState.LandingIndicatorScale,
      Velocity = this.Velocity,
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not SimpleBullet3DState b3s) return;
    this.Velocity = b3s.Velocity;
  }
}
