using System.Collections.Generic;

public class SaveData {
  // Global State
  public string DifficultyPath { get; set; }
  public int LevelsCleared { get; set; }
  public float DifficultyMultiplier { get; set; }
  public float EnemyRank { get; set; }
  public float TimeBond { get; set; }
  public float HyperGauge { get; set; }

  // Event State
  // 存储当前事件池中剩余事件的资源路径
  public List<string> RemainingEventPaths { get; set; } = new();

  // Map State
  public int CurrentPlane { get; set; }
  public int PlayerPosQ { get; set; }
  public int PlayerPosR { get; set; }
  public bool HasPlayerPos { get; set; }
  public List<MapNodeData> MapNodes { get; set; } = new();

  // Player Build
  public float CurrentHealth { get; set; }
  public string WeaponPath { get; set; }
  public List<string> UpgradePaths { get; set; } = new();
  public List<string> CurioPaths { get; set; } = new();

  // Context
  public string SceneFilePath { get; set; }
  public bool IsBossEncounter { get; set; }
  public int BossPhaseIndex { get; set; }
}

public class MapNodeData {
  public int Q { get; set; }
  public int R { get; set; }
  public int Type { get; set; }
  public int Score { get; set; }
}
