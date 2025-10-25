namespace Rewind;

public interface IRewindable {
  /// <summary>
  /// 获取此对象的唯一实例 ID．
  /// GodotObject.GetInstanceId() 是一个很好的选择．
  /// </summary>
  ulong InstanceId { get; }

  /// <summary>
  /// 捕获当前对象的状态并返回一个状态快照对象．
  /// </summary>
  RewindState CaptureState();

  /// <summary>
  /// 根据给定的状态快照恢复对象的状态．
  /// </summary>
  void RestoreState(RewindState state);

  /// <summary>
  /// 一个自定义的销毁方法，用于代替 QueueFree()．
  /// 它会通知 RewindManager 对象已死亡，并禁用自身．
  /// </summary>
  void Destroy();

  /// <summary>
  /// 使对象重新激活（用于回溯到它还存活的时间点）．
  /// </summary>
  void Resurrect();
}
