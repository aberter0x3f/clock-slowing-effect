using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class WeAreCowboysEvent : GameEvent {
  public enum State {
    Decision,
    CombatWon
  }

  [ExportGroup("_Internal States")]
  public State CurrentState { get; set; } = State.Decision;

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    CurrentState = State.Decision;
  }

  public override string GetTitle() {
    return "We Are Cowboys";
  }

  public override string GetDescription() {
    if (CurrentState == State.Decision) {
      return "Shadowy figures in wide-brimmed hats block your path. The air is thick with tension, like a classic western standoff about to unfold.";
    }
    return "The dust settles, and you stand victorious. Your resolve is hardened.";
  }

  public override List<EventOption> GetOptions() {
    if (CurrentState == State.Decision) {
      return new List<EventOption> {
        new("Duel!",
          "Draw your weapon and settle this with a fight. Victory will grant you an additional [color=orange]50%[/color] of your max health."),
        new("Surrender",
          "Pay for peace. You will immediately gain a [color=orange]25s[/color] Time Bond to ensure safe passage.")
      };
    }
    return new List<EventOption> {
      new("Claim Reward", "Your maximum health has been increased.")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;

    if (CurrentState == State.Decision) {
      if (optionIndex == 0) { // Duel
        CurrentState = State.CombatWon;
        return new StartCombat { };
      }
      if (optionIndex == 1) { // Surrender
        gm.TimeBond += 25f;
        return new FinishEvent();
      }
    } else if (CurrentState == State.CombatWon) {
      gm.AddTime(gm.PlayerStats.MaxHealth * 0.5f);
      return new FinishEvent();
    }

    return new FinishEvent();
  }
}
