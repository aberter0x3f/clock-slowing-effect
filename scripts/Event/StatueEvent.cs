using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class StatueEvent : GameEvent {
  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
  }

  public override string GetTitle() {
    return "Statue";
  }

  public override string GetDescription() {
    return "You find a beautifully carved statue. It seems to radiate a powerful, yet costly, aura.";
  }

  public override List<EventOption> GetOptions() {
    var gm = GameManager.Instance;
    float cost1 = gm.CurrentPlayerHealth * 0.3f;
    float cost2 = gm.CurrentPlayerHealth * 0.7f;
    return new List<EventOption> {
      new("Discard the statue",
        $"Obtain a Level [color=orange]2[/color] Upgrade, but gain a [color=orange]{cost1:F1}s[/color] Time Bond (30% of current health)."),
      new("Believe in the statue",
        $"Obtain a Level [color=orange]3[/color] Upgrade, but gain a [color=orange]{cost2:F1}s[/color] Time Bond (70% of current health).")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;
    IsFinished = true;

    if (optionIndex == 0) {
      gm.TimeBond += gm.CurrentPlayerHealth * 0.3f;
      return new ShowUpgradeSelection { MinLevel = 2, MaxLevel = 2 };
    }
    if (optionIndex == 1) {
      gm.TimeBond += gm.CurrentPlayerHealth * 0.7f;
      return new ShowUpgradeSelection { MinLevel = 3, MaxLevel = 3 };
    }

    GD.PrintErr("Unexpected state reached in StatueEvent");
    return new FinishEvent();
  }
}
