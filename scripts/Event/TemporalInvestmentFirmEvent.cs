using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class TemporalInvestmentFirmEvent : GameEvent {
  private int _stage = 1;

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    _stage = 1;
  }

  public override string GetTitle() {
    return "Temporal Investment Firm";
  }

  public override string GetDescription() {
    if (IsFinished) return "The firm has closed your account.";
    return $"A slick banker offers a high-risk, high-reward investment plan. This is investment round #{_stage}.";
  }

  public override List<EventOption> GetOptions() {
    if (IsFinished || _stage > 3) {
      return new List<EventOption> { new("Leave", "Your portfolio is closed.") };
    }

    float cost = _stage == 1 ? 10f : (GameManager.Instance.PlayerStats.MaxHealth * (_stage == 2 ? 0.5f : 1.0f));
    float failChance = _stage == 1 ? 0f : (_stage == 2 ? 0.5f : 0.75f);
    string costString = _stage == 1 ? $"{cost:F0}s" : $"{(_stage == 2 ? 50 : 100)}% max health";

    return new List<EventOption> {
      new("Invest",
        $"Invest a [color=orange]{costString}[/color] Time Bond. On success, you gain health equal to [color=orange]double[/color] your investment. There is a [color=orange]{failChance:P0}[/color] chance of failure, which ends the event."),
      new("Cash out", "Walk away with your current gains.")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    if (optionIndex == 1) {
      return new FinishEvent();
    }

    var gm = GameManager.Instance;
    float cost = _stage == 1 ? 10f : (gm.PlayerStats.MaxHealth * (_stage == 2 ? 0.5f : 1.0f));
    float failChance = _stage == 1 ? 0f : (_stage == 2 ? 0.5f : 0.75f);

    gm.TimeBond += cost;

    if (Rng.Randf() < failChance) {
      // Failure
      return new FinishEvent();
    }

    // Success
    gm.AddTime(cost * 2);
    ++_stage;

    if (_stage > 3) {
      return new FinishEvent();
    }

    return new UpdateEvent();
  }
}
