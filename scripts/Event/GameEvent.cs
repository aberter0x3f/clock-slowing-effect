using System.Collections.Generic;
using Godot;

namespace Event;

public abstract record EventExecutionResult;
public record UpdateEvent : EventExecutionResult;
public record FinishEvent : EventExecutionResult;
public record StartCombat : EventExecutionResult {
  public Godot.Collections.Array<EnemyData> Enemies { get; init; } = null;
  public float DifficultyMultiplier { get; init; } = 1.0f;
};
public record ShowUpgradeSelection : EventExecutionResult {
  public UI.UpgradeSelectionMenu.Mode Mode { get; init; } = UI.UpgradeSelectionMenu.Mode.Gain;
  public int Picks { get; init; } = 1;
  public int MinLevel { get; init; } = 1;
  public int MaxLevel { get; init; } = 3;
  public int ChoiceCount { get; init; } = 3;
}

[GlobalClass]
public abstract partial class GameEvent : Resource {
  [ExportGroup("_Internal States")]
  [Export]
  public RandomNumberGenerator Rng { get; set; }

  [Export]
  public bool IsFinished { get; set; } = false;

  /// <summary>
  /// 初始化事件，传入一个专用的 RNG 以保证结果可重现．
  /// </summary>
  public virtual void Initialize(RandomNumberGenerator rng) {
    Rng = rng;
    IsFinished = false;
  }

  /// <summary>
  /// 获取事件的标题．
  /// </summary>
  public abstract string GetTitle();

  /// <summary>
  /// 获取事件的当前描述．
  /// </summary>
  public abstract string GetDescription();

  /// <summary>
  /// 获取当前可用的选项列表．
  /// </summary>
  public abstract List<EventOption> GetOptions();

  /// <summary>
  /// 当玩家选择一个选项时调用．
  /// </summary>
  /// <param name="optionIndex">被选中的选项索引．</param>
  /// <returns>一个结果，告诉调用者接下来该做什么．</returns>
  public abstract EventExecutionResult ExecuteOption(int optionIndex);
}
