using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class RobotSalesTerminalEvent : GameEvent {
  private int _attempt = 1;

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    _attempt = 1;
  }

  public override string GetTitle() {
    return "Robot Sales Terminal";
  }

  public override string GetDescription() {
    if (IsFinished) return "The terminal powers down.";
    return $"A cheerful robot offers you a 'free' sample. This is your attempt #{_attempt}.";
  }

  public override List<EventOption> GetOptions() {
    if (IsFinished || _attempt > 4) {
      return new List<EventOption> { new("Leave", "The offer has expired.") };
    }

    float failChance = _attempt * 0.2f;
    float successChance = 1.0f - failChance;

    return new List<EventOption> {
      new("Accept Sample",
        $"There is a [color=orange]{successChance:P0}[/color] chance to get a free Level [color=orange]1-3[/color] Upgrade. On failure ([color=orange]{failChance:P0}[/color] chance), you gain a [color=orange]60s[/color] Time Bond and the event ends."),
      new("Decline", "Walk away.")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    if (optionIndex == 1) {
      return new FinishEvent();
    }

    var gm = GameManager.Instance;
    float failChance = _attempt * 0.2f;

    if (Rng.Randf() < failChance) {
      // Failure
      gm.TimeBond += 60f;
      return new FinishEvent();
    }

    // Success
    ++_attempt;
    return new ShowUpgradeSelection();
  }
}
