using System.Collections.Generic;
using System.Linq;
using Godot;

namespace Rewind;

[GlobalClass]
public partial class RewindManager : Node {
  public static RewindManager Instance { get; private set; }

  [Export(PropertyHint.Range, "1, 120, 1")]
  public int RecordFps { get; set; } = 45;

  [Export(PropertyHint.Range, "1, 120, 1")]
  public float MaxRecordTime { get; set; } = 15.0f;

  public bool IsRewinding { get; private set; } = false;
  public bool IsPreviewing { get; private set; } = false;
  public double PreviewRewindTime => IsPreviewing ? _history.Last.Value.Timestamp - _rewindTargetTimestamp : 0;
  public double AutoRewindRemainingTime => IsPreviewing && IsAutoRewinding ? _rewindTargetTimestamp - _autoRewindCommitTimestamp : 0;

  public int TrackedObjectCount => _objectPool.Count;

  // 用于自动回溯的状态
  public bool IsAutoRewinding { get; private set; } = false;
  private double _autoRewindCommitTimestamp;

  /// <summary>
  /// 获取当前可回溯的总时长（秒）．
  /// </summary>
  public float AvailableRewindTime {
    get {
      if (_history.Count < 2) {
        return 0.0f;
      }
      // 使用 LinkedList 的 First 和 Last 属性来高效地访问两端
      return (float) (_history.Last.Value.Timestamp - _history.First.Value.Timestamp);
    }
  }

  // 内部类，用于存储一帧的快照数据
  private class RewindFrame {
    public double Timestamp;
    public Dictionary<ulong, RewindState> States = new();
  }

  private readonly LinkedList<RewindFrame> _history = new();
  // 只追踪当前存活的对象，以提高录制效率
  private readonly Dictionary<ulong, IRewindable> _aliveObjects = new();
  // 对象池，包含所有在历史记录中存在过的对象（无论死活）
  private readonly Dictionary<ulong, Node> _objectPool = new();

  // 高效的引用计数器，用于对象池清理
  private readonly Dictionary<ulong, int> _idReferenceCounts = new();

  private double _recordTimer;
  private double _rewindTargetTimestamp;
  private LinkedListNode<RewindFrame> _currentPreviewNode;

  public override void _Ready() {
    Instance = this;
    _recordTimer = 1.0f / RecordFps;
  }

  public override void _Process(double delta) {
    HandleInput();

    if (IsPreviewing) {
      // 在预览模式下，根据 TimeScale 调整回溯速度
      _rewindTargetTimestamp -= delta * TimeManager.Instance.TimeScale;
      ApplyPreview();

      if (IsAutoRewinding && _rewindTargetTimestamp <= _autoRewindCommitTimestamp) {
        CommitRewind();
      }
    } else {
      // 正常记录
      _recordTimer -= delta;
      if (_recordTimer <= 0) {
        RecordFrame();
        _recordTimer = 1.0f / RecordFps;
      }
    }
  }

  /// <summary>
  /// 清空所有历史记录和内部状态，用于关卡重置或 Boss 阶段切换．
  /// </summary>
  public void ResetHistory() {
    // 遍历对象池的副本，因为 Unregister 会修改原始集合
    foreach (var (id, obj) in _objectPool.ToList()) {
      // 只清理那些已经死亡的对象
      if (_aliveObjects.ContainsKey(id)) {
        continue;
      }

      if (obj is IRewindable rewindable) {
        Unregister(rewindable);
      }
      obj.QueueFree();
    }

    _history.Clear();
    _idReferenceCounts.Clear();

    // 注意：这里不清除 _aliveObjects 和 _objectPool 中的持久化对象（如 Player）．
    // 动态对象被 QueueFree() 时，它们的 _ExitTree 会调用 Unregister() 来清理．

    IsRewinding = false;
    IsPreviewing = false;
    IsAutoRewinding = false;
    _recordTimer = 1.0f / RecordFps;
  }

  private void HandleInput() {
    // 游戏暂停时，不处理任何输入．
    if (GetTree().Paused) {
      return;
    }

    if (Input.IsActionPressed("time_slow")) {
      TimeManager.Instance.TimeScale = 0.5f;
      if (GameManager.Instance != null) GameManager.Instance.UsedSlowThisLevel = true;
    } else {
      TimeManager.Instance.TimeScale = 1.0f;
    }

    // 自动回溯期间，禁用玩家手动控制
    if (!IsAutoRewinding) {
      if (Input.IsActionJustPressed("time_rewind")) {
        if (GameManager.Instance != null) GameManager.Instance.UsedSkillThisLevel = true;
        StartRewindPreview();
      } else if (Input.IsActionJustReleased("time_rewind")) {
        CommitRewind();
      }
    }
  }

  /// <summary>
  /// 触发一次自动回溯．
  /// </summary>
  /// <param name="duration">要回溯的时间长度（秒）．</param>
  /// <returns>如果成功触发则返回 true，否则返回 false．</returns>
  public bool TriggerAutoRewind(float duration) {
    // 如果正在回溯或历史记录不足，则无法触发
    if (IsPreviewing || IsRewinding || AvailableRewindTime < duration) {
      return false;
    }

    GD.Print($"Triggering auto-rewind for {duration} seconds.");
    if (GameManager.Instance != null) GameManager.Instance.HadMissThisLevel = true;

    IsAutoRewinding = true;
    StartRewindPreview(); // 启动预览模式

    // 计算回溯预览结束并提交状态的时间点
    _autoRewindCommitTimestamp = _rewindTargetTimestamp - duration;
    // 确保不会回溯到比历史记录还早的时间
    _autoRewindCommitTimestamp = double.Max(_autoRewindCommitTimestamp, _history.First.Value.Timestamp);

    return true;
  }

  public void Register(IRewindable obj) {
    if (obj is not Node node) return;
    ulong id = obj.InstanceId;
    // 一个被注册的对象总是被认为是存活的
    if (!_aliveObjects.ContainsKey(id)) {
      _aliveObjects.Add(id, obj);
    }
    // 仅当对象是新的时候才添加到主对象池
    if (!_objectPool.ContainsKey(id)) {
      _objectPool.Add(id, node);
    }
  }

  public void Unregister(IRewindable obj) {
    // 这是对象真正被 QueueFree 时调用的，通常在清理时
    ulong id = obj.InstanceId;
    _aliveObjects.Remove(id);
    _objectPool.Remove(id);
    _idReferenceCounts.Remove(id);
  }

  public void NotifyDestroyed(IRewindable obj) {
    // 这是对象调用 Destroy() 时调用的，将其从存活列表中移除
    _aliveObjects.Remove(obj.InstanceId);
  }

  private void RecordFrame() {
    if (IsRewinding) return;

    var frame = new RewindFrame {
      Timestamp = TimeManager.Instance.CurrentGameTime
    };

    // 只遍历并捕获所有当前存活对象的状态
    foreach (var (id, obj) in _aliveObjects) {
      frame.States.Add(id, obj.CaptureState());
    }

    _history.AddLast(frame);

    // 为新帧中的所有对象增加引用计数
    foreach (var id in frame.States.Keys) {
      _idReferenceCounts[id] = _idReferenceCounts.GetValueOrDefault(id, 0) + 1;
    }

    TrimHistory();
  }

  private void TrimHistory() {
    double cutoffTimestamp = TimeManager.Instance.CurrentGameTime - MaxRecordTime;
    while (_history.Count > 0 && _history.First.Value.Timestamp < cutoffTimestamp) {
      var frameToRemove = _history.First.Value;
      _history.RemoveFirst();
      // 将单帧包装在列表中以复用清理逻辑
      CleanupObjectPool(new List<RewindFrame> { frameToRemove });
    }
  }

  private void CleanupObjectPool(List<RewindFrame> removedFrames) {
    var potentialPurgeIds = new HashSet<ulong>();

    // 遍历被移除的帧，减少对应 ID 的引用计数
    foreach (var frame in removedFrames) {
      foreach (var id in frame.States.Keys) {
        if (_idReferenceCounts.ContainsKey(id)) {
          --_idReferenceCounts[id];
          if (_idReferenceCounts[id] <= 0) {
            // 此 ID 的引用计数已降为 0，意味着它不再存在于任何历史快照中．
            // 将其从计数器中移除，并加入待清理候选列表．
            _idReferenceCounts.Remove(id);
            potentialPurgeIds.Add(id);
          }
        } else {
          potentialPurgeIds.Add(id);
        }
      }
    }

    // 遍历候选列表，检查哪些对象可以被真正销毁
    foreach (var id in potentialPurgeIds) {
      // 清理条件：对象的引用计数为 0，并且它当前不是存活状态．
      if (!_aliveObjects.ContainsKey(id)) {
        if (_objectPool.TryGetValue(id, out var nodeToPurge) && IsInstanceValid(nodeToPurge)) {
          // 在 QueueFree 之前，必须从所有追踪系统中完全注销
          if (nodeToPurge is IRewindable rewindable) {
            Unregister(rewindable);
          }
          nodeToPurge.QueueFree();
        }
      }
    }
  }

  private void StartRewindPreview() {
    if (_history.Count == 0) return;
    IsPreviewing = true;
    _rewindTargetTimestamp = _history.Last.Value.Timestamp;
    _currentPreviewNode = _history.Last;
  }

  private void ApplyPreview() {
    if (_currentPreviewNode == null) {
      CommitRewind();
      return;
    }

    // 找到最接近目标时间戳的帧
    while (_currentPreviewNode.Previous != null && _currentPreviewNode.Value.Timestamp > _rewindTargetTimestamp) {
      _currentPreviewNode = _currentPreviewNode.Previous;
    }
    while (_currentPreviewNode.Next != null && _currentPreviewNode.Next.Value.Timestamp < _rewindTargetTimestamp) {
      _currentPreviewNode = _currentPreviewNode.Next;
    }

    var frame = _currentPreviewNode.Value;
    RestoreFromFrame(frame);
  }

  private void CommitRewind() {
    if (!IsPreviewing) return;

    IsRewinding = true; // 标记正在进行一次真正的状态恢复

    // 恢复到最终选择的帧
    if (_currentPreviewNode != null) {
      var finalFrame = _currentPreviewNode.Value;
      RestoreFromFrame(finalFrame);
      TimeManager.Instance.SetCurrentGameTime(finalFrame.Timestamp);

      // 移除此节点之后的所有历史记录
      var framesToRemove = new List<RewindFrame>();
      var nodeToRemove = _currentPreviewNode.Next;
      while (nodeToRemove != null) {
        framesToRemove.Add(nodeToRemove.Value);
        var nextNode = nodeToRemove.Next;
        _history.Remove(nodeToRemove);
        nodeToRemove = nextNode;
      }
      if (framesToRemove.Count > 0) {
        CleanupObjectPool(framesToRemove);
      }
    }

    IsPreviewing = false;
    IsRewinding = false; // 恢复完成
    IsAutoRewinding = false; // 确保重置自动回溯状态
  }

  private void RestoreFromFrame(RewindFrame frame) {
    var frameObjectIds = new HashSet<ulong>(frame.States.Keys);

    // 遍历所有可能存在的对象（无论死活）
    foreach (var (id, node) in _objectPool) {
      var obj = (IRewindable) node;
      bool isCurrentlyAlive = _aliveObjects.ContainsKey(id);
      bool shouldBeAlive = frameObjectIds.Contains(id);

      if (shouldBeAlive && !isCurrentlyAlive) {
        // 复活并恢复状态
        obj.Resurrect();
        obj.RestoreState(frame.States[id]);
      } else if (!shouldBeAlive && isCurrentlyAlive) {
        // 销毁
        obj.Destroy();
      } else if (shouldBeAlive && isCurrentlyAlive) {
        // 仅恢复状态
        obj.RestoreState(frame.States[id]);
      }
      // 如果 !shouldBeAlive && !isCurrentlyAlive，则什么都不做．它已死，且应该保持死亡．
    }
  }
}
