using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class RockPaperScissorsEvent : GameEvent {
  public enum State {
    Decision,
    CombatWon
  }

  [ExportGroup("_Internal States")]
  [Export]
  public State CurrentState { get; set; } = State.Decision;

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    CurrentState = State.Decision;
  }

  public override string GetTitle() {
    return "Rock, Paper, Scissors";
  }

  public override string GetDescription() {
    if (CurrentState == State.Decision) {
      return "A strange machine challenges you to a game. It seems the only way to play is with bullets.";
    }
    return "You've outsmarted the machine. It dispenses your prize.";
  }

  public override List<EventOption> GetOptions() {
    if (CurrentState == State.Decision) {
      return new List<EventOption> {
        new("Play",
          "Engage in combat. Winning will reward you with [color=orange]2[/color] Level [color=orange]1-2[/color] Upgrades."),
        new("Forfeit",
          "Decline the challenge and accept a [color=orange]30s[/color] Time Bond.")
      };
    }
    return new List<EventOption> {
      new("Claim Reward", "You receive two new upgrades.")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;

    if (CurrentState == State.Decision) {
      if (optionIndex == 0) { // Play
        CurrentState = State.CombatWon;
        return new StartCombat();
      }
      if (optionIndex == 1) { // Forfeit
        gm.TimeBond += 30f;
        return new FinishEvent();
      }
    } else if (CurrentState == State.CombatWon) {
      IsFinished = true;
      return new ShowUpgradeSelection { Picks = 2, MinLevel = 1, MaxLevel = 2 };
    }

    GD.PrintErr("Unexpected state reached in RockPaperScissorsEvent");
    return new FinishEvent();
  }
}
