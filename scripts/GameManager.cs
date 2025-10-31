using System.Collections.Generic;
using System.Linq;
using Godot;

public partial class GameManager : Node {
  public static GameManager Instance { get; private set; }

  [Export]
  public Godot.Collections.Array<Event.GameEvent> AllEvents { get; set; }
  [Export]
  public UpgradeDatabase UpgradeDb { get; set; }

  public DifficultySetting CurrentDifficulty { get; private set; }
  public HexMap GameMap { get; private set; }
  public Vector2I SelectedMapPosition { get; set; }
  public Vector2I? PlayerMapPosition { get; private set; }
  public int LevelsCleared { get; private set; }
  public float DifficultyMultiplier { get; private set; } = 1.0f;

  // 玩家状态和强化系统属性
  [Export]
  public PlayerBaseStats PlayerBaseStats { get; private set; }
  public PlayerStats PlayerStats { get; set; }
  public float CurrentPlayerHealth { get; set; }
  public HashSet<Upgrade> AcquiredUpgrades { get; } = new();
  public HashSet<Upgrade> PendingUpgradesAdditions { get; } = new();
  public HashSet<Upgrade> PendingUpgradesRemovals { get; } = new();

  // 时间债券属性
  public float TimeBond { get; set; }

  // 当前关卡表现
  public bool UsedSlowThisLevel { get; set; }
  public bool UsedSkillThisLevel { get; set; }
  public bool HadMissThisLevel { get; set; }

  // 事件和子战斗系统
  private List<Event.GameEvent> _currentEventSequence = new();
  public string ReturnScenePath { get; private set; }
  public bool InSubCombat { get; private set; } = false;
  public Event.GameEvent ActiveEvent { get; set; }

  // 用于自定义子战斗
  public Godot.Collections.Array<EnemyData> SubCombatEnemies { get; set; }
  public float SubCombatDifficultyMultiplier { get; set; } = 1.0f;

  public override void _Ready() {
    Instance = this;
    ResetPlayerStats();
  }

  /// <summary>
  /// 当玩家从难度菜单开始新游戏时调用．
  /// </summary>
  public void InitializeNewRun(DifficultySetting difficulty) {
    CurrentDifficulty = difficulty;
    GameMap = new HexMap();
    LevelsCleared = 0;
    DifficultyMultiplier = 1.0f;
    PlayerMapPosition = null; // 初始时玩家不在任何节点上
    ActiveEvent = null;
    ShuffleEventSequence();

    ResetPlayerStats();

    GD.Print($"New run started with difficulty '{difficulty.Name}'.");
  }

  /// <summary>
  /// 打乱事件池，为新一轮游戏做准备．
  /// </summary>
  private void ShuffleEventSequence() {
    var list = new Godot.Collections.Array<Event.GameEvent>(AllEvents);
    list.Shuffle();
    _currentEventSequence = list.ToList();
  }

  /// <summary>
  /// 重置玩家状态．
  /// </summary>
  public void ResetPlayerStats() {
    AcquiredUpgrades.Clear();
    PendingUpgradesAdditions.Clear();
    PendingUpgradesRemovals.Clear();
    CurrentPlayerHealth = PlayerBaseStats.MaxHealth;
    PlayerStats = new PlayerStats(PlayerBaseStats);
    PlayerStats.RecalculateStats(AcquiredUpgrades, CurrentPlayerHealth);
    TimeBond = 0f;
  }

  /// <summary>
  /// 在重开一个关卡时调用．
  /// </summary>
  public void RestartLevel() {
    PendingUpgradesAdditions.Clear();
    PendingUpgradesRemovals.Clear();
    StartLevel();
  }

  /// <summary>
  /// 在进入一个新关卡时调用．
  /// </summary>
  public void StartLevel() {
    UsedSlowThisLevel = false;
    UsedSkillThisLevel = false;
    HadMissThisLevel = false;
  }

  /// <summary>
  /// 当玩家成功通过一个关卡时调用．
  /// </summary>
  public void CompleteLevel(HexMap.ClearScore score) {
    if (GameMap == null) return;

    // 如果是在子战斗中，则不更新地图和难度，只发出信号
    if (InSubCombat) {
      EndSubCombat();
      return;
    }

    var completedNode = GameMap.GetNode(SelectedMapPosition);
    if (completedNode != null) {
      completedNode.Score = score;
      ++LevelsCleared;
      DifficultyMultiplier *= CurrentDifficulty.PerLevelDifficultyMultiplier;
      GD.Print($"Level at {SelectedMapPosition} completed. Levels cleared: {LevelsCleared}. New difficulty multiplier: {DifficultyMultiplier:F2}.");
    }
    // 更新玩家当前所在的节点
    PlayerMapPosition = SelectedMapPosition;
    ActiveEvent = null;
    CommitPendingUpgrades();
  }

  /// <summary>
  /// 开始一场由事件触发的特殊战斗．
  /// </summary>
  public void StartSubCombat(string returnScenePath) {
    InSubCombat = true;
    ReturnScenePath = returnScenePath;
  }

  /// <summary>
  /// 结束子战斗状态．
  /// </summary>
  public void EndSubCombat() {
    InSubCombat = false;
    ReturnScenePath = null;
    // 清理特殊战斗参数
    SubCombatEnemies = null;
    SubCombatDifficultyMultiplier = 1.0f;
  }

  /// <summary>
  /// 将一个新强化添加到待定列表．它将在下一关开始时生效．
  /// </summary>
  public void AddUpgrade(Upgrade upgrade) {
    if (upgrade == null) return;
    // 如果此强化正等待被移除，则取消移除操作
    if (PendingUpgradesRemovals.Contains(upgrade)) {
      PendingUpgradesRemovals.Remove(upgrade);
      return;
    }
    // 确保强化在已获得和待定添加列表中都是唯一的
    if (AcquiredUpgrades.Contains(upgrade) || PendingUpgradesAdditions.Contains(upgrade)) return;
    PendingUpgradesAdditions.Add(upgrade);
  }

  /// <summary>
  /// 将一个强化添加到待移除列表．
  /// </summary>
  public void RemoveUpgrade(Upgrade upgrade) {
    if (upgrade == null) return;
    // 如果它是一个在本关卡中刚添加的待定强化，直接从待定添加列表中移除即可
    if (PendingUpgradesAdditions.Contains(upgrade)) {
      PendingUpgradesAdditions.Remove(upgrade);
      return;
    }
    // 如果它是一个已获得的强化，且尚未被标记为待移除，则添加到待移除列表
    if (AcquiredUpgrades.Contains(upgrade) && !PendingUpgradesRemovals.Contains(upgrade)) {
      PendingUpgradesRemovals.Add(upgrade);
    }
  }

  /// <summary>
  /// 将所有待定强化正式应用，并重新计算玩家属性．
  /// </summary>
  public void CommitPendingUpgrades() {
    if (PendingUpgradesAdditions.Count == 0 && PendingUpgradesRemovals.Count == 0) return;

    AcquiredUpgrades.UnionWith(PendingUpgradesAdditions);
    AcquiredUpgrades.ExceptWith(PendingUpgradesRemovals);
    PendingUpgradesAdditions.Clear();
    PendingUpgradesRemovals.Clear();

    // 立即重新计算属性，特别是血量上限
    PlayerStats.RecalculateStats(AcquiredUpgrades, CurrentPlayerHealth);
    // 确保玩家血量不超过新的上限
    CurrentPlayerHealth = Mathf.Min(CurrentPlayerHealth, PlayerStats.MaxHealth);
  }

  /// <summary>
  /// 获取玩家当前生效的所有强化（已获得 + 待定）．
  /// </summary>
  public HashSet<Upgrade> GetCurrentAndPendingUpgrades() {
    var currentUpgrades = new HashSet<Upgrade>(AcquiredUpgrades);
    currentUpgrades.UnionWith(PendingUpgradesAdditions);
    currentUpgrades.ExceptWith(PendingUpgradesRemovals);
    return currentUpgrades;
  }

  /// <summary>
  /// 根据规则获取一批可供选择的强化．
  /// </summary>
  public List<Upgrade> GetUpgradeToGain(int minLevel, int maxLevel, int count, RandomNumberGenerator rnd) {
    var availableUpgrades = new List<Upgrade>();
    var currentlyOwned = GetCurrentAndPendingUpgrades();

    // 从高到低遍历等级，填充候选列表
    for (int level = minLevel; level <= maxLevel; ++level) {
      var candidates = UpgradeDb.AllUpgrades
        .Where(u => u.Level == level && !currentlyOwned.Contains(u))
        .ToList();
      availableUpgrades.AddRange(candidates);
    }

    // 如果候选列表数量不足，尝试扩充更低等级的强化
    if (availableUpgrades.Count < count) {
      for (int level = minLevel - 1; level >= 1; --level) {
        var candidates = UpgradeDb.AllUpgrades
          .Where(u => u.Level == level && !currentlyOwned.Contains(u))
          .ToList();
        if (availableUpgrades.Count + candidates.Count <= count) {
          availableUpgrades.AddRange(candidates);
        } else {
          // 随机挑选更低等级的强化直到数量足够
          while (availableUpgrades.Count < count) {
            if (candidates.Count == 0) break;
            int randomIndex = rnd.RandiRange(0, candidates.Count - 1);
            availableUpgrades.Add(candidates[randomIndex]);
            candidates.RemoveAt(randomIndex);
          }
        }
      }
    }

    if (availableUpgrades.Count <= count) {
      return availableUpgrades;
    }

    // 随机挑选指定数量的强化
    var choices = new List<Upgrade>();
    for (int i = 0; i < count; ++i) {
      if (availableUpgrades.Count == 0) break;
      int randomIndex = rnd.RandiRange(0, availableUpgrades.Count - 1);
      choices.Add(availableUpgrades[randomIndex]);
      availableUpgrades.RemoveAt(randomIndex);
    }
    return choices;
  }

  /// <summary>
  /// 从玩家已有的强化中随机选择一些供丢弃．
  /// </summary>
  public List<Upgrade> GetUpgradesToLose(int minLevel, int maxLevel, int count, RandomNumberGenerator rnd) {
    var ownedUpgrades = GetCurrentAndPendingUpgrades();
    var candidates = ownedUpgrades
      .Where(u => u.Level >= minLevel && u.Level <= maxLevel)
      .ToList();

    if (candidates.Count <= count) {
      return candidates;
    }

    // 随机挑选指定数量
    var choices = new List<Upgrade>();
    for (int i = 0; i < count; ++i) {
      if (candidates.Count == 0) break;
      int randomIndex = rnd.RandiRange(0, candidates.Count - 1);
      choices.Add(candidates[randomIndex]);
      candidates.RemoveAt(randomIndex);
    }
    return choices;
  }

  /// <summary>
  /// 根据玩家当前位置获取所有可访问的节点．
  /// </summary>
  public List<Vector2I> GetAccessibleNodes() {
    var accessibleNodes = new List<Vector2I>();
    if (GameMap == null) return accessibleNodes;

    // 如果玩家还未踏上任何节点（游戏刚开始），则只有起始节点可访问
    if (PlayerMapPosition == null) {
      accessibleNodes.Add(GameMap.StartPosition);
      return accessibleNodes;
    }

    // 否则，可访问的节点是玩家当前所在节点的右侧邻居
    var basePosition = PlayerMapPosition.Value;
    Vector2I[] rightNeighbors = {
      basePosition + HexMap.Dirs[0],
      basePosition + HexMap.Dirs[1],
      basePosition + HexMap.Dirs[5],
    };

    foreach (var neighborPos in rightNeighbors) {
      if (GameMap.Nodes.ContainsKey(neighborPos)) {
        accessibleNodes.Add(neighborPos);
      }
    }
    return accessibleNodes;
  }

  /// <summary>
  /// 从事件序列中获取下一个事件．
  /// </summary>
  public Event.GameEvent GetNextEvent() {
    if (_currentEventSequence.Count == 0) {
      ShuffleEventSequence();
    }
    var nextEvent = _currentEventSequence.Last();
    _currentEventSequence.RemoveAt(_currentEventSequence.Count - 1);
    return nextEvent;
  }

  /// <summary>
  /// 增加时间，优先偿还时间债券．
  /// </summary>
  /// <returns>一个元组，包含分别用于偿还债券和增加生命的时间量．</returns>
  public (float appliedToBond, float appliedToHealth) AddTime(float amount) {
    float appliedToBond = Mathf.Min(amount, TimeBond);
    TimeBond -= appliedToBond;

    float appliedToHealth = amount - appliedToBond;
    if (appliedToHealth > 0) {
      appliedToHealth = Mathf.Min(appliedToHealth, PlayerStats.MaxHealth - CurrentPlayerHealth);
      CurrentPlayerHealth += appliedToHealth;
    }

    return (appliedToBond, appliedToHealth);
  }
}
