using System.Collections.Generic;
using Godot;

namespace Event;

[GlobalClass]
public partial class CheatCodeEvent : GameEvent {
  public enum State {
    Decision, // 玩家做选择的初始阶段
    CheckForConsequences, // 给予奖励后，检查是否触发战斗的阶段
  }

  [ExportGroup("_Internal States")]
  [Export]
  public State CurrentState { get; set; }
  [Export]
  public float CombatChance { get; set; }
  [Export]
  public float DifficultyMultiplier { get; set; }

  public override void Initialize(RandomNumberGenerator rng) {
    base.Initialize(rng);
    CurrentState = State.Decision;
    CombatChance = 0f;
    DifficultyMultiplier = 1.0f;
  }

  public override string GetTitle() {
    return "Cheat Code";
  }

  public override string GetDescription() {
    if (CurrentState == State.Decision) {
      return "You've found a glitch in the fabric of time. It offers tempting rewards, but exploiting it might have consequences.";
    }

    return "The deal is done. But did you attract any unwanted attention?";
  }

  public override List<EventOption> GetOptions() {
    if (CurrentState == State.Decision) {
      return new List<EventOption> {
        new("IDDQD",
          "Gain health equal to [color=orange]20%[/color] of your max health. There is a [color=orange]20%[/color] chance of triggering a combat encounter."),
        new("IDKFA",
          "Gain [color=orange]2[/color] Level [color=orange]1-2[/color] Upgrades. There is a [color=orange]50%[/color] chance of triggering a combat encounter."),
        new("I want it all",
          "Gain both of the above rewards. There is a [color=orange]75%[/color] chance of triggering a [b]harder[/b] ([color=orange]1.2x[/color] difficulty) combat encounter."),
        new("No clipping", "Leave the glitch alone.")
      };
    }

    // 在检查后果阶段，只提供一个继续选项
    if (CurrentState == State.CheckForConsequences) {
      return new List<EventOption> {
        new("[Continue]", "Face the consequences.")
      };
    }

    return new List<EventOption> {
      new("[Exit]", "Close.")
    };
  }

  public override EventExecutionResult ExecuteOption(int optionIndex) {
    var gm = GameManager.Instance;

    if (CurrentState == State.Decision) {
      // 改变状态，为检查后果做准备
      CurrentState = State.CheckForConsequences;

      switch (optionIndex) {
        case 0: // IDDQD - Health
          gm.AddTime(gm.PlayerStats.MaxHealth * 0.2f);
          CombatChance = 0.2f;
          // 因为没有中间 UI (如升级选择)，直接更新事件菜单以显示 [Continue] 按钮
          return new UpdateEvent();

        case 1: // IDKFA - Upgrades
          CombatChance = 0.5f;
          // 显示升级选择菜单．结束后，EventDevice 会重新显示事件菜单，
          // 届时事件将处于 CheckForConsequences 状态
          return new ShowUpgradeSelection { Picks = 2, MinLevel = 1, MaxLevel = 2 };

        case 2: // I want it all - Both
          gm.AddTime(gm.PlayerStats.MaxHealth * 0.2f);
          CombatChance = 0.75f;
          DifficultyMultiplier = 1.2f;
          return new ShowUpgradeSelection { Picks = 2, MinLevel = 1, MaxLevel = 2 };

        case 3: // No clipping - Leave
          return new FinishEvent();

        default:
          GD.PrintErr("Unexpected state reached in CheatCodeEvent");
          return new FinishEvent();
      }
    }

    // 如果当前状态是 CheckForConsequences，意味着玩家点击了 [Continue]
    // 或者从升级选择菜单返回
    if (CurrentState == State.CheckForConsequences) {
      IsFinished = true; // 无论结果如何，事件都将结束

      if (Rng.Randf() < CombatChance) {
        return new StartCombat();
      }
      return new FinishEvent();
    }


    GD.PrintErr("Unexpected state reached in CheatCodeEvent");
    return new FinishEvent();
  }
}
