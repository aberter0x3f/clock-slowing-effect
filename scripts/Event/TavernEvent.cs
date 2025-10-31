using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class TavernEvent : GameEvent {
  [Export]
  public Godot.Collections.Array<EnemyData> EliteEnemies { get; set; }

  public enum State {
    Decision,
    CombatWonA,
    CombatWonB,
    CombatWonBoth
  }

  [ExportGroup("_Internal States")]
  [Export]
  public State _currentState { get; set; } = State.Decision;

  [Export]
  public EnemyData _eliteA { get; set; }

  [Export]
  public EnemyData _eliteB { get; set; }

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    _currentState = State.Decision;

    var availableElites = new List<EnemyData>(EliteEnemies);
    _eliteA = availableElites[rng.RandiRange(0, availableElites.Count - 1)];
    _eliteB = availableElites[rng.RandiRange(0, availableElites.Count - 1)];
    while (_eliteA == _eliteB) {
      _eliteB = availableElites[rng.RandiRange(0, availableElites.Count - 1)];
    }
  }

  public override string GetTitle() {
    return "Tavern";
  }

  public override string GetDescription() {
    switch (_currentState) {
      case State.Decision:
        return "You enter a rowdy tavern filled with formidable-looking patrons. Two of them seem particularly interested in a brawl.";
      case State.CombatWonA:
        return $"You defeated {_eliteA.Scene.Instantiate<Node>().Name}! Collect your reward.";
      case State.CombatWonB:
        return $"You defeated {_eliteB.Scene.Instantiate<Node>().Name}! Collect your reward.";
      case State.CombatWonBoth:
        return "An incredible victory! The entire tavern is in awe. Claim your well-earned prize.";
    }
    return "";
  }

  public override List<EventOption> GetOptions() {
    if (_currentState == State.Decision) {
      var options = new List<EventOption>();
      if (_eliteA != null) {
        options.Add(new($"Fight {_eliteA.Scene.Instantiate<Node>().Name}",
          "Fight this elite. Reward: [color=orange]1[/color] Level [color=orange]2[/color] Upgrade."));
      }
      if (_eliteB != null) {
        options.Add(new($"Fight {_eliteB.Scene.Instantiate<Node>().Name}",
          "Fight this elite. Reward: [color=orange]1[/color] Level [color=orange]2[/color] Upgrade."));
      }
      if (_eliteA != null && _eliteB != null) {
        options.Add(new("Fight them both",
          "Fight both elites at once in a [color=orange]1.5x[/color] difficulty battle. Reward: [color=orange]2[/color] Level [color=orange]2-3[/color] Upgrade."));
      }
      return options;
    }
    return new List<EventOption> { new("Claim Reward", "Receive your prize.") };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;

    if (_currentState == State.Decision) {
      if (optionIndex == 0 && _eliteA != null) {
        _currentState = State.CombatWonA;
        return new StartCombat { Enemies = new Godot.Collections.Array<EnemyData> { _eliteA } };
      }
      if (optionIndex == 1 && _eliteB != null) {
        _currentState = State.CombatWonB;
        return new StartCombat { Enemies = new Godot.Collections.Array<EnemyData> { _eliteB } };
      }
      if (optionIndex == 2 && _eliteA != null && _eliteB != null) {
        _currentState = State.CombatWonBoth;
        return new StartCombat { Enemies = new Godot.Collections.Array<EnemyData> { _eliteA, _eliteB }, DifficultyMultiplier = 1.5f };
      }
    } else {
      IsFinished = true;
      if (_currentState == State.CombatWonA || _currentState == State.CombatWonB) {
        return new ShowUpgradeSelection { MinLevel = 2, MaxLevel = 2 };
      }
      if (_currentState == State.CombatWonBoth) {
        return new ShowUpgradeSelection { Picks = 2, MinLevel = 2, MaxLevel = 3 };
      }
    }

    GD.PrintErr("Unexpected state reached in TavernEvent");
    return new FinishEvent();
  }
}
