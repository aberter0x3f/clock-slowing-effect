using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class InsightsFromTheUniversalDancerEvent : GameEvent {
  public int _stage = 1;

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    _stage = 1;
  }

  public override string GetTitle() {
    return "Insights from the Universal Dancer";
  }

  public override string GetDescription() {
    if (IsFinished) {
      return "The dance concludes.";
    }
    return $"A cosmic being offers you a glimpse of profound knowledge, for a price. This is your attempt #{_stage}.";
  }

  public override List<EventOption> GetOptions() {
    if (IsFinished || _stage > 3) {
      return new List<EventOption> { new("Leave", "The opportunity has passed.") };
    }

    float cost = 10 * _stage;
    float chance = _stage == 1 ? 30f : (_stage == 2 ? 60f : 100f);

    return new List<EventOption> {
      new("Draw",
        $"Pay a [color=orange]{cost}s[/color] Time Bond for a [color=orange]{chance}%[/color] chance to receive a Level [color=orange]{_stage}[/color] Upgrade. Success ends the event."),
      new("Do not draw", "Walk away from the offer.")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;

    if (optionIndex == 1 || _stage > 3) { // Do not draw or finished
      return new FinishEvent();
    }

    // Draw
    float cost = 10 * _stage;
    float chance = _stage == 1 ? 0.3f : (_stage == 2 ? 0.6f : 1.0f);
    gm.TimeBond += cost;

    if (Rng.Randf() < chance) {
      IsFinished = true;
      return new ShowUpgradeSelection { MinLevel = _stage, MaxLevel = _stage };
    }

    // Failure
    ++_stage;
    if (_stage > 3) {
      return new FinishEvent();
    }

    return new UpdateEvent();
  }
}
