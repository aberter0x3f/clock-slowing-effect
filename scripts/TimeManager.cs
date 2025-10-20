using Godot;

public partial class TimeManager : Node {
  [Export]
  public float TimeScale { get; set; } = 1.0f;

  public static TimeManager Instance { get; private set; }

  public override void _Ready() {
    Instance = this;
  }
}
