using System;
using System.IO;
using System.Text.Json;
using Godot;

public partial class SaveManager : Node {
  public static SaveManager Instance { get; private set; }

  public override void _Ready() {
    Instance = this;
  }

  public void SaveGame(string filePath) {
    try {
      var saveData = CreateSaveData();
      string jsonString = JsonSerializer.Serialize(saveData);
      File.WriteAllText(filePath, jsonString);
      GD.Print($"Game saved to {filePath}");
    } catch (Exception e) {
      GD.PrintErr($"Failed to save game: {e.Message}");
    }
  }

  public void LoadGame(string filePath) {
    try {
      if (!File.Exists(filePath)) {
        GD.PrintErr("Save file not found.");
        return;
      }
      string jsonString = File.ReadAllText(filePath);
      var saveData = JsonSerializer.Deserialize<SaveData>(jsonString);
      ApplySaveData(saveData);
      GD.Print($"Game loaded from {filePath}");
    } catch (Exception e) {
      GD.PrintErr($"Failed to load game: {e.Message}");
    }
  }

  private SaveData CreateSaveData() {
    var gm = GameManager.Instance;
    var data = new SaveData();

    // Global
    data.DifficultyPath = gm.CurrentDifficulty?.ResourcePath;
    data.LevelsCleared = gm.LevelsCleared;
    data.DifficultyMultiplier = gm.DifficultyMultiplier;
    data.EnemyRank = gm.EnemyRank;
    data.HyperGauge = gm.HyperGauge;

    // Events
    foreach (var evt in gm.CurrentEventSequence) {
      data.RemainingEventPaths.Add(evt.ResourcePath);
    }

    // Map
    if (gm.GameMap != null) {
      data.CurrentPlane = gm.GameMap.Plane;
      if (gm.PlayerMapPosition.HasValue) {
        data.HasPlayerPos = true;
        data.PlayerPosQ = gm.PlayerMapPosition.Value.X;
        data.PlayerPosR = gm.PlayerMapPosition.Value.Y;
      } else {
        data.HasPlayerPos = false;
      }

      foreach (var kvp in gm.GameMap.Nodes) {
        data.MapNodes.Add(new MapNodeData {
          Q = kvp.Key.X,
          R = kvp.Key.Y,
          Type = (int) kvp.Value.Type,
          Score = (int) kvp.Value.Score
        });
      }
    }

    // Player Build
    data.WeaponPath = gm.SelectedWeaponDefinition?.ResourcePath;
    foreach (var upgrade in gm.AcquiredUpgrades) {
      data.UpgradePaths.Add(upgrade.ResourcePath);
    }
    foreach (var curio in gm.AcquiredCurios) {
      data.CurioPaths.Add(curio.ResourcePath);
    }

    // Context & Logic for SubCombat
    if (gm.InSubCombat) {
      // 关键逻辑：如果在子战斗中，存档路径指向上一层场景（事件房）
      // 并且重置战斗状态
      data.SceneFilePath = gm.ReturnScenePath;
      data.IsBossEncounter = false;
      data.BossPhaseIndex = 0;
      data.CurrentHealth = gm.CurrentPlayerHealth;
      data.TimeBond = gm.TimeBond;
    } else {
      data.SceneFilePath = GetTree().CurrentScene.SceneFilePath;

      var boss = GetTree().Root.GetNodeOrNull<Enemy.Boss.Boss>("GameRoot/Boss");
      if (IsInstanceValid(boss)) {
        data.IsBossEncounter = true;
        data.BossPhaseIndex = boss.CurrentPhaseIndex;
        // Boss 战使用阶段开始时的快照
        var playerSnapshot = boss.PlayerPhaseStartState;
        data.CurrentHealth = playerSnapshot?.Health ?? gm.CurrentPlayerHealth;
        data.TimeBond = playerSnapshot?.TimeBond ?? gm.TimeBond;
      } else {
        data.IsBossEncounter = false;
        data.BossPhaseIndex = 0;
        data.CurrentHealth = gm.CurrentPlayerHealth;
        data.TimeBond = gm.TimeBond;
      }
    }

    return data;
  }

  private void ApplySaveData(SaveData data) {
    var gm = GameManager.Instance;

    // Restore Global
    if (!string.IsNullOrEmpty(data.DifficultyPath)) {
      gm.InitializeNewRun(GD.Load<DifficultySetting>(data.DifficultyPath));
    } else {
      gm.ResetPlayerStats();
    }

    // 覆盖 InitializeNewRun 重置的值
    gm.LevelsCleared = data.LevelsCleared;
    gm.DifficultyMultiplier = data.DifficultyMultiplier;
    gm.EnemyRank = data.EnemyRank;

    // Restore Events & Map
    gm.RestoreEventDeck(data.RemainingEventPaths);
    gm.RestoreMapFromSave(data);

    // Restore Player Build
    foreach (var path in data.UpgradePaths) {
      gm.AcquiredUpgrades.Add(GD.Load<Upgrade>(path));
    }

    foreach (var path in data.CurioPaths) {
      gm.AcquiredCurios.Add(GD.Load<Curio.BaseCurio>(path));
    }

    if (!string.IsNullOrEmpty(data.WeaponPath)) {
      gm.SelectedWeaponDefinition = GD.Load<Weapon.WeaponDefinition>(data.WeaponPath);
    }

    gm.HyperGauge = data.HyperGauge;
    gm.TimeBond = data.TimeBond;
    gm.CurrentPlayerHealth = data.CurrentHealth;
    gm.PlayerStats.RecalculateStats(gm.GetCurrentAndPendingUpgrades(), gm.CurrentPlayerHealth);

    // 设置加载标记
    gm.IsLoadingFromSave = true;
    gm.PendingBossPhaseIndex = data.IsBossEncounter ? data.BossPhaseIndex : -1;
    gm.PendingPhaseStartHealth = data.CurrentHealth;
    gm.PendingPhaseStartBond = data.TimeBond; // 只有 Boss 战快照才需要这个，普通房间和 TimeBond 一样

    // Change Scene
    SceneTransitionManager.Instance.TransitionToScene(data.SceneFilePath, Vector3.Zero);
  }
}
