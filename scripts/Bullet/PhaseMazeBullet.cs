using Godot;
using Rewind;

namespace Bullet;

public class PhaseMazeBulletState : BaseBulletState {
  public float CurrentY;
}

public partial class PhaseMazeBullet : BaseBullet {
  public enum MazeBulletType { Normal, LowSpeedPhase, HighSpeedPhase, Graze }

  [Export] public MazeBulletType Type { get; set; } = MazeBulletType.Normal;
  [Export] public float PhaseHeight { get; set; } = 0.5f;
  [Export] public float PhaseSpeed { get; set; } = 4.0f;

  public float VelocityZ { get; set; }
  private float _currentY = 0f;
  private Player _player;

  public override void _Ready() {
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    base._Ready();
  }

  public override void UpdateBullet(float scaledDelta) {
    GlobalPosition += Vector3.Back * VelocityZ * scaledDelta;

    // 2. 处理相位逻辑 (3D Y 轴)
    float targetY = 0f;
    bool isSlow = Input.IsActionPressed("time_slow");

    switch (Type) {
      case MazeBulletType.Normal:
        var target = _player.DecoyTarget ?? _player;
        _currentY = targetY = _player.GlobalPosition.Y;
        break;
      case MazeBulletType.LowSpeedPhase: if (isSlow) targetY = PhaseHeight; break;
      case MazeBulletType.HighSpeedPhase: if (!isSlow) targetY = PhaseHeight; break;
      case MazeBulletType.Graze: if (WasGrazed) targetY = PhaseHeight; break;
    }

    _currentY = Mathf.MoveToward(_currentY, targetY, PhaseSpeed * scaledDelta);
    GlobalPosition = GlobalPosition with { Y = _currentY };
  }

  public override RewindState CaptureState() {
    var bs = (BaseBulletState) base.CaptureState();
    return new PhaseMazeBulletState {
      GlobalPosition = bs.GlobalPosition,
      GlobalRotation = bs.GlobalRotation,
      WasGrazed = bs.WasGrazed,
      IsGrazing = bs.IsGrazing,
      Modulate = bs.Modulate,
      TimeAlive = bs.TimeAlive,
      CurrentY = _currentY
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not PhaseMazeBulletState s) return;
    _currentY = s.CurrentY;
  }
}
