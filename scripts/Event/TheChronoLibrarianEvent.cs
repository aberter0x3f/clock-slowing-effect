using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class TheChronoLibrarianEvent : GameEvent {
  private enum State {
    Decision,
    GainingLevel2,
    GainingLevel3
  }
  private State _currentState = State.Decision;

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    _currentState = State.Decision;
  }

  public override string GetTitle() {
    return "The Chrono-Librarian";
  }

  public override string GetDescription() {
    switch (_currentState) {
      case State.Decision:
        return "An elderly librarian, buried in books of \"possibility\", offers to help you exchange knowledge.";
      case State.GainingLevel2:
        return "A fair trade. Now, choose your new knowledge.";
      case State.GainingLevel3:
        return "A masterful refinement. Select your reward.";
    }
    return "";
  }

  public override List<EventOption> GetOptions() {
    if (_currentState == State.Decision) {
      return new List<EventOption> {
        new("Trade Up",
          "Lose [color=orange]1[/color] of your Level [color=orange]1[/color] Upgrades to gain [color=orange]1[/color] Level [color=orange]2[/color] Upgrade."),
        new("Refine Knowledge",
          "Lose [color=orange]1[/color] of your Level [color=orange]2[/color] Upgrades to gain [color=orange]1[/color] Level [color=orange]3[/color] Upgrade."),
        new("Keep Current Knowledge", "Leave the library.")
      };
    }
    return new List<EventOption> { new("Claim Reward", "Receive your prize.") };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;

    if (_currentState == State.Decision) {
      if (optionIndex == 0) { // Trade Up
        _currentState = State.GainingLevel2;
        return new ShowUpgradeSelection { Mode = UI.UpgradeSelectionMenu.Mode.Lose, MinLevel = 1, MaxLevel = 1 };
      }
      if (optionIndex == 1) { // Refine
        _currentState = State.GainingLevel3;
        return new ShowUpgradeSelection { Mode = UI.UpgradeSelectionMenu.Mode.Lose, MinLevel = 2, MaxLevel = 2 };
      }
      if (optionIndex == 2) { // Leave
        return new FinishEvent();
      }
    } else {
      IsFinished = true;
      if (_currentState == State.GainingLevel2) {
        return new ShowUpgradeSelection { MinLevel = 2, MaxLevel = 2 };
      }
      if (_currentState == State.GainingLevel3) {
        return new ShowUpgradeSelection { MinLevel = 3, MaxLevel = 3 };
      }
    }

    GD.PrintErr("Unexpected state reached in TheChronoLibrarianEvent");
    return new FinishEvent();
  }
}
