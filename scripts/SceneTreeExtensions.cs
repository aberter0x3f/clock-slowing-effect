using System.Threading.Tasks;
using Godot;

/// <summary>
/// Provides convenient extension methods for SceneTree, particularly for handling
/// time-scaled asynchronous operations.
/// </summary>
public static class SceneTreeExtensions {
  /// <summary>
  /// Creates a one-shot timer that respects TimeManager.Instance.TimeScale and waits for it to complete.
  /// The timer node is automatically added to the scene tree's root and freed upon completion.
  /// This is an async replacement for `await ToSignal(GetTree().CreateTimer(duration), "timeout")`
  /// for scenarios where time scale must be respected.
  /// </summary>
  /// <param name="tree">The SceneTree to create the timer in.</param>
  /// <param name="duration">The duration in seconds for the timer to wait.</param>
  /// <returns>A task that completes when the timer times out.</returns>
  public static async Task CreateTimeScaleTimer(this SceneTree tree, float duration) {
    var timer = new TimeScaleTimer {
      WaitTime = duration,
      OneShot = true
    };
    tree.Root.AddChild(timer);
    timer.Start();
    await timer.ToSignal(timer, TimeScaleTimer.SignalName.Timeout);
    timer.QueueFree();
  }
}
