using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class NildisCardEvent : GameEvent {
  [ExportGroup("_Internal States")]
  [Export]
  public int Flips { get; set; } = 0;

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    Flips = 0;
  }

  public override string GetTitle() {
    return "Nildis Card";
  }

  public override string GetDescription() {
    if (IsFinished) return "The game is over.";
    return $"A deck of cards hovers in the air. A disembodied voice dares you to draw. You have flipped {Flips} card(s).";
  }

  public override List<EventOption> GetOptions() {
    if (IsFinished || Flips >= 5) {
      return new List<EventOption> { new("Leave", "The deck vanishes.") };
    }

    float combatChance = (Flips + 1) * 0.2f;

    return new List<EventOption> {
      new("Flip the card",
        $"There is a [color=orange]{combatChance:P0}[/color] chance to trigger a combat encounter and end the event. Otherwise, you receive a Level [color=orange]1-2[/color] Upgrade."),
      new("Give up", "End the game and walk away.")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    if (optionIndex == 1) {
      return new FinishEvent();
    }

    var gm = GameManager.Instance;
    ++Flips;
    float combatChance = Flips * 0.2f;

    if (Rng.Randf() < combatChance) {
      // Combat
      IsFinished = true; // The event itself is over, combat is the result
      return new StartCombat();
    }

    // Get Upgrade
    return new ShowUpgradeSelection { MinLevel = 1, MaxLevel = 2 };
  }
}
