using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class ATMMachineEvent : GameEvent {
  public override string GetTitle() {
    return "ATM Machine";
  }

  public override string GetDescription() {
    return "A strange, temporal ATM hums before you. It seems to deal in time itself.";
  }

  public override List<EventOption> GetOptions() {
    var gm = GameManager.Instance;
    return new List<EventOption> {
      new("Withdraw",
        $"Pay off your entire Time Bond, restoring an equivalent amount of health (up to your max). Current bond: [color=orange]{gm.TimeBond:F1}s[/color]."),
      new("Take a loan",
        "Instantly restore your health to its maximum value. You will then gain a Time Bond equal to [color=orange]50%[/color] of the health you restored."),
      new("Do nothing", "Leave the machine alone.")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;
    IsFinished = true;

    switch (optionIndex) {
      case 0: // Withdraw
        gm.AddTime(gm.TimeBond); // AddTime handles paying off bond first
        return new FinishEvent();
      case 1: // Loan
        float healthBefore = gm.CurrentPlayerHealth;
        gm.CurrentPlayerHealth = gm.PlayerStats.MaxHealth;
        float healthGained = gm.CurrentPlayerHealth - healthBefore;
        if (healthGained > 0) {
          gm.TimeBond += healthGained * 0.5f;
        }
        return new FinishEvent();
      case 2: // Do nothing
        return new FinishEvent();
    }

    GD.PrintErr("Unexpected state reached in ATMMachineEvent");
    return new FinishEvent();
  }
}
