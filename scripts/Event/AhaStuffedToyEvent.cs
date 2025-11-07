using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class AhaStuffedToyEvent : GameEvent {
  public override string GetTitle() {
    return "Aha Stuffed Toy";
  }

  public override string GetDescription() {
    return "You find a peculiar stuffed toy of Aha, the Aeon of Elation. It has a small switch on its back.";
  }

  public override List<EventOption> GetOptions() {
    return new List<EventOption> {
      new("Twist the switch",
        "There's a [color=orange]42%[/color] chance to get a Level [color=orange]1[/color] Upgrade, a [color=orange]36%[/color] chance for nothing to happen, and a [color=orange]22%[/color] chance to lose a Level [color=orange]1[/color] Upgrade."),
      new("Tear it apart",
        "There's a [color=orange]50%[/color] chance to restore health equal to [color=orange]50%[/color] of your max health, and a [color=orange]50%[/color] chance to gain a Time Bond equal to [color=orange]50%[/color] of your current health.")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;
    IsFinished = true;

    if (optionIndex == 0) { // Twist the switch
      float roll = Rng.Randf();
      if (roll < 0.42f) {
        return new ShowUpgradeSelection();
      }
      if (roll < 0.42f + 0.36f) {
        return new FinishEvent();
      }
      return new ShowUpgradeSelection { Mode = UI.UpgradeSelectionMenu.Mode.Lose };
    }

    if (optionIndex == 1) { // Tear it apart
      if (Rng.Randf() < 0.5f) {
        gm.AddTime(gm.PlayerStats.MaxHealth * 0.5f);
      } else {
        gm.TimeBond += gm.CurrentPlayerHealth * 0.5f;
      }
      return new FinishEvent();
    }

    GD.PrintErr("Unexpected state reached in AhaStuffedToyEvent");
    return new FinishEvent();
  }
}
