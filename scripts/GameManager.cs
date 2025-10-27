using System.Collections.Generic;
using Godot;

public partial class GameManager : Node {
  public static GameManager Instance { get; private set; }

  public DifficultySetting CurrentDifficulty { get; private set; }
  public HexMap GameMap { get; private set; }
  public Vector2I SelectedMapPosition { get; set; }
  public Vector2I? PlayerMapPosition { get; private set; }
  public int LevelsCleared { get; private set; }
  public float DifficultyMultiplier { get; private set; } = 1.0f;

  public override void _Ready() {
    Instance = this;
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
    GD.Print($"New run started with difficulty '{difficulty.Name}'.");
  }

  /// <summary>
  /// 当玩家成功通过一个关卡时调用．
  /// </summary>
  public void CompleteLevel() {
    if (GameMap == null) return;

    var completedNode = GameMap.GetNode(SelectedMapPosition);
    if (completedNode != null && !completedNode.IsCleared) {
      completedNode.IsCleared = true;
      LevelsCleared++;
      DifficultyMultiplier *= CurrentDifficulty.PerLevelDifficultyMultiplier;
      GD.Print($"Level at {SelectedMapPosition} completed. Levels cleared: {LevelsCleared}. New difficulty multiplier: {DifficultyMultiplier:F2}.");
    }
    // 更新玩家当前所在的节点
    PlayerMapPosition = SelectedMapPosition;
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
}
