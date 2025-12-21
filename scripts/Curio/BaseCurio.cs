using Godot;

namespace Curio;

// 定义奇物的类型，便于在代码中识别和处理
public enum CurioType {
  TAxisEnhancement,
  XAxisEnhancement,
  ZAxisEnhancement,
  DecoyTarget,
  ItemCollector,
  GoldenBody,
  TotemOfUndying,
}

/// <summary>
/// 代表一个奇物（道具）的基类资源．
/// </summary>
public abstract partial class BaseCurio : Resource {
  public abstract CurioType Type { get; }
  public abstract string Name { get; }
  public abstract string Description { get; }
  public abstract bool HasActiveEffect { get; }
  public abstract bool HasPassiveEffect { get; }
  public abstract float Cooldown { get; }

  // 运行时状态
  public float CurrentCooldown { get; set; } = 0f;

  /// <summary>
  /// 当玩家获得此奇物时调用．
  /// </summary>
  public virtual void OnAcquired(Player player) { }

  /// <summary>
  /// 当玩家失去此奇物时调用．
  /// </summary>
  public virtual void OnLost(Player player) { }

  /// <summary>
  /// 每帧调用（如果存在被动效果），用于处理持续性逻辑．
  /// </summary>
  public virtual void OnUpdate(Player player, float scaledDelta) { }

  /// <summary>
  /// 当玩家按下使用键时调用．
  /// </summary>
  public virtual void OnUsePressed(Player player) { }

  /// <summary>
  /// 当玩家按住使用键时每帧调用．
  /// </summary>
  public virtual void OnUseHeld(Player player, float scaledDelta) { }

  /// <summary>
  /// 当玩家松开使用键时调用．
  /// </summary>
  public virtual void OnUseReleased(Player player) { }

  /// <summary>
  /// 当玩家在按住使用键时切换奇物，强制中断使用效果时调用．
  /// </summary>
  public virtual void OnUseCancelled(Player player) { }
}
