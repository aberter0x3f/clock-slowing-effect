using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class StephenLloydsIdeaEvent : GameEvent {
  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
  }

  public override string GetTitle() {
    return "Stephen Lloyd's Idea";
  }

  public override string GetDescription() {
    return "A brilliant, fleeting idea flashes through your mind. You can try to grasp it or let it go.";
  }

  public override List<EventOption> GetOptions() {
    return new List<EventOption> {
      new("Grasp it", "Obtain [color=orange]1[/color] Level [color=orange]1-2[/color] Upgrade."),
      new("Let it go", "Do nothing.")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    IsFinished = true;
    if (optionIndex == 0) {
      return new ShowUpgradeSelection { MinLevel = 1, MaxLevel = 2 };
    }
    return new FinishEvent();
  }
}
