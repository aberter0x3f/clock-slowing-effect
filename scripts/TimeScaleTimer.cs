using Godot;

/// <summary>
/// A custom Timer node that respects a global TimeManager.Instance.TimeScale.
/// It mimics the behavior of the standard Timer node but its countdown
/// is affected by the time scale.
/// </summary>
[GlobalClass] // Makes it available as a node type in the Godot editor
public partial class TimeScaleTimer : Node {
  [Signal]
  public delegate void TimeoutEventHandler();

  [Export]
  public float WaitTime { get; set; } = 1.0f;

  [Export]
  public bool OneShot { get; set; } = true;

  [Export]
  public bool Autostart { get; set; } = false;

  private float _timeLeft;
  private bool _isStopped = true;

  public float TimeLeft => _timeLeft;
  public bool IsStopped() => _isStopped;

  public override void _Ready() {
    if (Autostart) {
      Start();
    }
  }

  public override void _Process(double delta) {
    if (_isStopped) {
      return;
    }

    // Apply the global time scale to the delta time
    _timeLeft -= (float) delta * TimeManager.Instance.TimeScale;

    if (_timeLeft <= 0) {
      EmitSignal(SignalName.Timeout);
      if (OneShot) {
        Stop();
      } else {
        // Reset for the next interval, carrying over the remainder
        _timeLeft += WaitTime;
      }
    }
  }

  /// <summary>
  /// Starts the timer.
  /// </summary>
  /// <param name="customTime">If provided, sets the WaitTime before starting.</param>
  public void Start(float customTime = -1.0f) {
    if (customTime > 0) {
      WaitTime = customTime;
    }
    _timeLeft = WaitTime;
    _isStopped = false;
  }

  /// <summary>
  /// Stops the timer.
  /// </summary>
  public void Stop() {
    _isStopped = true;
  }
}
