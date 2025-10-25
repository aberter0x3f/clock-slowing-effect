using Godot;

public partial class TimeManager : Node {
  [Export]
  public float TimeScale { get; set; } = 1.0f;

  /// <summary>
  /// The total elapsed game time, affected by TimeScale.
  /// This is the authoritative game clock.
  /// </summary>
  public double CurrentGameTime { get; private set; } = 0.0;

  public static TimeManager Instance { get; private set; }

  public override void _Ready() {
    Instance = this;
  }

  public override void _Process(double delta) {
    // Update the game clock every frame.
    CurrentGameTime += delta * TimeScale;
  }

  /// <summary>
  /// Allows the RewindManager to set the game clock back in time.
  /// </summary>
  public void SetCurrentGameTime(double time) {
    CurrentGameTime = time;
  }
}
