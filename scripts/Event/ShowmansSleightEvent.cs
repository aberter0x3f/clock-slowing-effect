using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class ShowmansSleightEvent : GameEvent {
  private enum State {
    Decision,
    LosingLevel1,
    LosingLevel2
  }
  private State _currentState = State.Decision;

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    _currentState = State.Decision;
  }

  public override string GetTitle() {
    return "Showman's Sleight";
  }

  public override string GetDescription() {
    switch (_currentState) {
      case State.Decision:
        return "A charismatic showman offers you a trade, a classic sleight of hand. 'What you see is what you get... mostly.'";
      case State.LosingLevel1:
        return "Now, for the second part of the trick... you must discard two of your lesser treasures.";
      case State.LosingLevel2:
        return "And for the grand finale... one of your prized possessions must vanish.";
    }
    return "";
  }

  public override List<EventOption> GetOptions() {
    if (_currentState == State.Decision) {
      return new List<EventOption> {
        new("A grand trade",
          "Gain [color=orange]2[/color] Level [color=orange]2[/color] Upgrades, then lose [color=orange]1[/color] [color=orange]random[/color] Level [color=orange]2[/color] Upgrade."),
        new("A flurry of tricks",
          "Gain [color=orange]4[/color] Level [color=orange]1[/color] Upgrades, then lose [color=orange]2[/color] [color=orange]random[/color] Level [color=orange]1[/color] Upgrades.")
      };
    }

    if (_currentState == State.LosingLevel1) {
      return new List<EventOption> {
        new("Pick Upgrades to lose", "Discard [color=orange]2[/color] [color=orange]random[/color] Level [color=orange]1[/color] Upgrades.")
      };
    }

    if (_currentState == State.LosingLevel2) {
      return new List<EventOption> {
        new("Pick Upgrade to lose", "Discard [color=orange]1[/color] [color=orange]random[/color] Level [color=orange]2[/color] Upgrade.")
      };
    }

    return new List<EventOption>();
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;

    if (_currentState == State.Decision) {
      if (optionIndex == 0) {
        _currentState = State.LosingLevel2;
        return new ShowUpgradeSelection { Picks = 2, MinLevel = 2, MaxLevel = 2 };
      }
      if (optionIndex == 1) {
        _currentState = State.LosingLevel1;
        return new ShowUpgradeSelection { Picks = 4, MinLevel = 1, MaxLevel = 1 };
      }
    } else if (_currentState == State.LosingLevel1) {
      IsFinished = true;
      return new ShowUpgradeSelection { Mode = UI.UpgradeSelectionMenu.Mode.Lose, Picks = 2, MinLevel = 1, MaxLevel = 1, ChoiceCount = 1 };
    } else if (_currentState == State.LosingLevel2) {
      IsFinished = true;
      return new ShowUpgradeSelection { Mode = UI.UpgradeSelectionMenu.Mode.Lose, Picks = 1, MinLevel = 2, MaxLevel = 2, ChoiceCount = 1 };
    }

    GD.PrintErr("Unexpected state reached in ShowmansSleightEvent");
    return new FinishEvent();
  }
}
