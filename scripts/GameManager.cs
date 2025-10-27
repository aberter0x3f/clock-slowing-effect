using Godot;

public partial class GameManager : Node {
  public static GameManager Instance { get; private set; }

  public DifficultySetting CurrentDifficulty { get; private set; }
  public HexMap GameMap { get; private set; }
  public Vector2I CurrentMapPosition { get; set; }
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
    CurrentMapPosition = GameMap.StartPosition;
    GD.Print($"New run started with difficulty '{difficulty.Name}'.");
  }

  /// <summary>
  /// 当玩家成功通过一个关卡时调用．
  /// </summary>
  public void CompleteLevel() {
    if (GameMap == null) return;

    var completedNode = GameMap.GetNode(CurrentMapPosition);
    if (completedNode != null && !completedNode.IsCleared) {
      completedNode.IsCleared = true;
      LevelsCleared++;
      DifficultyMultiplier *= CurrentDifficulty.PerLevelDifficultyMultiplier;
      GameMap.UpdateAccessibleNodes(CurrentMapPosition);
      GD.Print($"Level at {CurrentMapPosition} completed. Levels cleared: {LevelsCleared}. New difficulty multiplier: {DifficultyMultiplier:F2}.");
    }
  }
}
