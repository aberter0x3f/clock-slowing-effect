using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Event;

[GlobalClass]
public partial class LotteryMachineEvent : GameEvent {
  private enum State {
    Decision, // 玩家做选择的初始阶段
    AwaitingResult // 玩家已下注（失去强化），等待开奖
  }

  private State _state = State.Decision;
  // 用于比较并找出玩家失去了哪个强化
  private HashSet<Upgrade> _upgradesBeforeBet;

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    _state = State.Decision;
    _upgradesBeforeBet = null;
  }

  public override string GetTitle() {
    return "Lottery Machine";
  }

  public override string GetDescription() {
    if (_state == State.Decision) {
      return "A dusty lottery machine whirs to life. It promises great fortune for a small price.";
    }
    return "You've placed your bet. The machine clunks and whirs... What will it be?";
  }

  public override List<EventOption> GetOptions() {
    if (_state == State.Decision) {
      var options = new List<EventOption> {
        new("Play for Upgrades",
          "Lose [color=orange]1[/color] of your Upgrades. There is a [color=orange]30%[/color] chance to win and receive [color=orange]4[/color] new [color=orange]random[/color] Upgrades of the same level as the one you lost."),
        new("Play for Health",
          "Gain a [color=orange]20%[/color] max health Time Bond. There is a [color=orange]30%[/color] chance to win and restore [color=orange]80%[/color] of your max health."),
        new("Walk Away", "Don't test your luck.")
      };

      // 如果玩家没有任何强化，则禁用第一个选项
      if (GameManager.Instance.GetCurrentAndPendingUpgrades().Count == 0) {
        options[0].IsEnabled = false;
        options[0].Description += "\n[color=red]You have no upgrades to lose.[/color]";
      }
      return options;
    }

    if (_state == State.AwaitingResult) {
      return new List<EventOption> {
        new("See the result", "Find out if you won.")
      };
    }

    return new List<EventOption>();
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;

    if (_state == State.Decision) {
      switch (optionIndex) {
        case 0: // Play for Upgrades
          if (gm.GetCurrentAndPendingUpgrades().Count == 0) {
            return new FinishEvent();
          }

          _state = State.AwaitingResult;
          // 记录下注前的强化状态
          _upgradesBeforeBet = gm.GetCurrentAndPendingUpgrades();

          // 弹出菜单，让玩家选择失去哪个强化
          return new ShowUpgradeSelection {
            Mode = UI.UpgradeSelectionMenu.Mode.Lose,
            Picks = 1,
            MinLevel = 1,
            MaxLevel = 3,
            ChoiceCount = 3 // 显示最多 3 个选项
          };

        case 1: // Play for Health
          IsFinished = true;
          gm.TimeBond += gm.PlayerStats.MaxHealth * 0.2f;
          if (Rng.Randf() < 0.3f) {
            gm.AddTime(gm.PlayerStats.MaxHealth * 0.8f);
          }
          return new FinishEvent();

        case 2: // Walk Away
          IsFinished = true;
          return new FinishEvent();
      }
    }

    if (_state == State.AwaitingResult) {
      IsFinished = true; // 无论结果如何，事件都将结束

      var upgradesAfterBet = gm.GetCurrentAndPendingUpgrades();
      // 通过比较集合找出被移除的强化
      var lostUpgrades = _upgradesBeforeBet.Except(upgradesAfterBet).ToList();

      if (lostUpgrades.Count != 1) {
        GD.PrintErr("LotteryMachineEvent: Could not determine the lost upgrade. Finishing event.");
        return new FinishEvent();
      }

      int lostUpgradeLevel = lostUpgrades[0].Level;

      // 掷骰子决定胜负
      if (Rng.Randf() < 0.3f) {
        // 胜利：显示强化选择
        // Picks=4, ChoiceCount=1 会连续弹出 4 次单选界面，等同于随机给予 4 个强化
        return new ShowUpgradeSelection {
          Picks = 4,
          MinLevel = lostUpgradeLevel,
          MaxLevel = lostUpgradeLevel,
          ChoiceCount = 1
        };
      }
      // 失败：直接结束
      return new FinishEvent();
    }

    GD.PrintErr("Unexpected state reached in LotteryMachineEvent");
    return new FinishEvent();
  }
}
