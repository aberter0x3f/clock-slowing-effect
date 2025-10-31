using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class CosmicCrescendoEvent : GameEvent {
  private enum State {
    Decision,
    Processing
  }

  private State _currentState;
  private int _eventsProcessed;
  private string _lastEventDescription;

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    _currentState = State.Decision;
    _eventsProcessed = 0;
    _lastEventDescription = "";
  }

  public override string GetTitle() {
    return "Cosmic Crescendo";
  }

  public override string GetDescription() {
    if (_currentState == State.Decision) {
      return "You witness a chaotic cascade of cosmic events, a symphony of creation and destruction. You can try to ride the wave or step aside.";
    }
    return $"The crescendo continues... ({_eventsProcessed}/10)\n\n{_lastEventDescription}";
  }

  public override List<EventOption> GetOptions() {
    if (_currentState == State.Decision) {
      return new List<EventOption> {
        new("Ride the wave",
          "Trigger [color=orange]10[/color] random micro-events in sequence: gain a [color=orange]random[/color] Upgrade, lose a [color=orange]random[/color] Upgrade, gain a random Time Bond, or restore a random amount of health."),
        new("Step aside", "Do nothing.")
      };
    }

    // 在处理阶段，只提供一个继续选项
    return new List<EventOption> {
      new("Continue", "Witness the next event.")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    if (_currentState == State.Decision) {
      if (optionIndex == 1) { // Step aside
        IsFinished = true;
        return new FinishEvent();
      }
      // Ride the wave
      _currentState = State.Processing;
      _lastEventDescription = "The symphony begins!";
      // 直接处理第一个事件
      return ProcessNextMicroEvent();
    }

    if (_currentState == State.Processing) {
      return ProcessNextMicroEvent();
    }

    // 备用逻辑
    IsFinished = true;
    GD.PrintErr("Unexpected state reached in CosmicCrescendoEvent");
    return new FinishEvent();
  }

  private EventExecutionResult ProcessNextMicroEvent() {
    if (_eventsProcessed >= 10) {
      IsFinished = true;
      return new FinishEvent();
    }

    ++_eventsProcessed;
    var gm = GameManager.Instance;
    int eventType = Rng.RandiRange(0, 3);

    switch (eventType) {
      case 0: // Gain Upgrade
        // 弹出强化选择框本身就是一种通知，所以不需要额外信息
        _lastEventDescription = "";
        return new ShowUpgradeSelection { ChoiceCount = 1, Picks = 1 };

      case 1: // Lose Upgrade
        if (gm.GetCurrentAndPendingUpgrades().Count == 0) {
          _lastEventDescription = "A destructive force passes by, but you had nothing for it to take.";
          return new UpdateEvent();
        }
        _lastEventDescription = "";
        return new ShowUpgradeSelection { Mode = UI.UpgradeSelectionMenu.Mode.Lose, ChoiceCount = 1, Picks = 1 };

      case 2: // Gain Bond
        float bondAmount = Rng.Randf() * gm.PlayerStats.MaxHealth * 0.2f;
        gm.TimeBond += bondAmount;
        _lastEventDescription = $"A temporal echo creates a debt. You gain a [color=orange]{bondAmount:F1}s[/color] Time Bond.";
        return new UpdateEvent();

      case 3: // Gain Health
        var (bondPaid, healthGained) = gm.AddTime(Rng.Randf() * gm.PlayerStats.MaxHealth * 0.2f);
        _lastEventDescription = "A soothing timeline intersects with yours.";
        if (bondPaid > 0.01f) _lastEventDescription += $" You paid off [color=orange]{bondPaid:F1}s[/color] of your bond.";
        if (healthGained > 0.01f) _lastEventDescription += $" You restored [color=orange]{healthGained:F1}s[/color] of health.";
        if (bondPaid < 0.01f && healthGained < 0.01f) _lastEventDescription += " But you were already at full capacity.";
        return new UpdateEvent();
    }

    // 不应该执行到这里
    GD.PrintErr("Unexpected state reached in CosmicCrescendoEvent");
    return new FinishEvent();
  }
}
