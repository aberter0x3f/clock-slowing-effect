using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Event;

[GlobalClass]
public partial class TheCrematorsEvent : GameEvent {
  private enum State {
    Decision,
    GainingRebirthUpgrades,
    LosingForSacrifice
  }

  private State _currentState = State.Decision;
  private readonly Queue<int> _rebirthGainQueue = new();

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    _currentState = State.Decision;
    _rebirthGainQueue.Clear();
  }

  public override string GetTitle() {
    return "The Cremators";
  }

  public override string GetDescription() {
    switch (_currentState) {
      case State.Decision:
        return "Figures in dark robes offer you a \"purification\" ritual. They promise rebirth from the ashes of your current power.";
      case State.GainingRebirthUpgrades:
        return $"The ashes swirl, reforming into new power. You have {_rebirthGainQueue.Count} gift(s) remaining.";
      case State.LosingForSacrifice:
        return "A power is gained, but the pact demands a sacrifice in return. You must give up one of your strongest abilities.";
      default:
        return "";
    }
  }

  public override List<EventOption> GetOptions() {
    var gm = GameManager.Instance;
    var currentUpgrades = gm.GetCurrentAndPendingUpgrades();

    switch (_currentState) {
      case State.Decision:
        var options = new List<EventOption> {
          new("Total Rebirth",
            "Lose [color=orange]ALL[/color] of your upgrades. For each upgrade lost, you have a [color=orange]1/3[/color] chance to gain a new upgrade of the next highest level (max Level 3), and a [color=orange]2/3[/color] chance to gain one of the [b]same level[/b]."),
          new("Calculated Sacrifice",
            "Gain [color=orange]1[/color] Level [color=orange]1-3[/color] Upgrade, then lose [color=orange]1[/color] of your highest-level upgrades."),
          new("Refuse", "Decline their offer.")
        };
        if (currentUpgrades.Count == 0) {
          options[0].IsEnabled = false;
          options[0].Description += "\n[color=red]You have no upgrades to lose.[/color]";
          options[1].IsEnabled = false;
          options[1].Description += "\n[color=red]You have no upgrades to lose.[/color]";
        }
        return options;

      case State.GainingRebirthUpgrades:
        return new List<EventOption> {
          new("Claim next gift", "Reach into the ashes.")
        };

      case State.LosingForSacrifice:
        return new List<EventOption> {
          new("Fulfill the pact", "Offer up an upgrade.")
        };
    }
    return new List<EventOption>();
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;

    switch (_currentState) {
      case State.Decision:
        switch (optionIndex) {
          case 0: // Total Rebirth
            var upgradesToLose = gm.GetCurrentAndPendingUpgrades().ToList();
            if (upgradesToLose.Count == 0) return new FinishEvent();

            foreach (var upgrade in upgradesToLose) {
              gm.RemoveUpgrade(upgrade);

              int currentLevel = upgrade.Level;
              int newLevel;
              if (Rng.Randf() < 1.0f / 3.0f) {
                newLevel = Mathf.Min(currentLevel + 1, 3);
              } else {
                newLevel = currentLevel;
              }
              _rebirthGainQueue.Enqueue(newLevel);
            }

            _currentState = State.GainingRebirthUpgrades;
            // 更新 UI 以显示「Claim」按钮，玩家点击后开始领取第一个强化
            return new UpdateEvent();

          case 1: // Calculated Sacrifice
            if (gm.GetCurrentAndPendingUpgrades().Count == 0) return new FinishEvent();
            _currentState = State.LosingForSacrifice;
            // 先获得强化
            return new ShowUpgradeSelection { MinLevel = 1, MaxLevel = 3 };

          case 2: // Refuse
            return new FinishEvent();
        }
        break;

      case State.GainingRebirthUpgrades:
        if (_rebirthGainQueue.Count > 0) {
          int level = _rebirthGainQueue.Dequeue();
          // 如果这是最后一个，则在选择后结束事件
          if (_rebirthGainQueue.Count == 0) {
            IsFinished = true;
          }
          return new ShowUpgradeSelection { MinLevel = level, MaxLevel = level };
        }
        // 作为备用逻辑，如果队列为空则结束
        return new FinishEvent();

      case State.LosingForSacrifice:
        var upgrades = gm.GetCurrentAndPendingUpgrades();
        if (upgrades.Count == 0) {
          // 如果在获得强化后，玩家仍然没有任何强化（不太可能），则直接结束
          return new FinishEvent();
        }
        int highestLevel = upgrades.Max(u => u.Level);
        IsFinished = true;
        // 失去一个最高等级的强化
        return new ShowUpgradeSelection { Mode = UI.UpgradeSelectionMenu.Mode.Lose, MinLevel = highestLevel, MaxLevel = highestLevel };
    }

    GD.PrintErr("Unexpected state reached in TheCrematorsEvent");
    return new FinishEvent();
  }
}
