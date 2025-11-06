using System.Linq;
using Curio;
using Godot;
using Rewind;
using UI;

namespace Room;

public partial class Combat : Node {
  private Player _player;
  private Label _uiLabel;
  private MapGenerator _mapGenerator;
  private EnemySpawner _enemySpawner;
  private RewindManager _rewindManager;
  private PauseMenu _pauseMenu;
  private Portal _spawnedPortal = null;

  private ulong _levelSeed;
  private RandomNumberGenerator _upgradeRng;

  [Export]
  public PackedScene PauseMenuScene { get; set; }

  [Export]
  public PackedScene PortalScene { get; set; }

  [Export]
  public PackedScene UpgradeSelectionMenuScene { get; set; }

  [Export(PropertyHint.File, "*.tscn")]
  public string InterLevelMenuScenePath { get; set; }

  public override void _Ready() {
    GameRootProvider.CurrentGameRoot = this;

    _player = GetNode<Player>("Player");
    _uiLabel = GetNode<Label>("CanvasLayer/Label");
    _mapGenerator = GetNode<MapGenerator>("MapGenerator");
    _enemySpawner = GetNode<EnemySpawner>("EnemySpawner");
    _rewindManager = GetNode<RewindManager>("RewindManager");

    if (PauseMenuScene != null) {
      _pauseMenu = PauseMenuScene.Instantiate<PauseMenu>();
      AddChild(_pauseMenu);
      _pauseMenu.RestartRequested += OnRestartRequested;
    }
    _player.DiedPermanently += OnPlayerDiedPermanently;
    _enemySpawner.WaveCompleted += OnWaveCompleted;

    _player.SpawnPosition = _mapGenerator.GenerateMap();
    _player.GlobalPosition = _player.SpawnPosition;

    // 初始化本关卡的随机种子和 RNG
    _levelSeed = ((ulong) GD.Randi() << 32) | (ulong) GD.Randi();
    _upgradeRng = new RandomNumberGenerator();
    _upgradeRng.Seed = _levelSeed;

    // 如果是从事件进入的子战斗，则使用返回场景路径
    if (GameManager.Instance.InSubCombat) {
      InterLevelMenuScenePath = GameManager.Instance.ReturnScenePath;
      GD.Print($"This is a sub-combat. Will return to '{InterLevelMenuScenePath}' upon completion.");
    }

    GameManager.Instance?.StartLevel();
    ConfigureEnemySpawner();
    _enemySpawner.StartSpawning(_mapGenerator, _player);
  }

  private void ConfigureEnemySpawner() {
    if (GameManager.Instance?.CurrentDifficulty == null) {
      GD.PrintErr("Combat: GameManager not initialized. Using default spawner values.");
      return;
    }
    var gm = GameManager.Instance;
    var difficultyMultiplier = gm.DifficultyMultiplier * gm.SubCombatDifficultyMultiplier;
    _enemySpawner.TotalDifficultyBudget = gm.CurrentDifficulty.InitialTotalDifficulty * difficultyMultiplier;
    _enemySpawner.MaxConcurrentDifficulty = gm.CurrentDifficulty.InitialMaxConcurrentDifficulty * difficultyMultiplier;

    // 处理 Tavern 事件的精英怪
    if (gm.InSubCombat && gm.SubCombatEnemies != null && gm.SubCombatEnemies.Count > 0) {
      _enemySpawner.EnemyDatabase = gm.SubCombatEnemies;
      _enemySpawner.TotalDifficultyBudget = Mathf.Max(_enemySpawner.TotalDifficultyBudget, gm.SubCombatEnemies.Sum(e => e.Difficulty));
      _enemySpawner.MaxConcurrentDifficulty = Mathf.Max(_enemySpawner.MaxConcurrentDifficulty, gm.SubCombatEnemies.Sum(e => e.Difficulty));
    }
    GD.Print($"Configuring spawner. Total Budget: {_enemySpawner.TotalDifficultyBudget}, Max Concurrent: {_enemySpawner.MaxConcurrentDifficulty}");
  }

  private void OnWaveCompleted() {
    GD.Print("Combat scene received WaveCompleted signal. Spawning portal.");
    if (PortalScene == null) {
      GD.PrintErr("PortalScene is not set in the Combat scene!");
      return;
    }
    if (!IsInstanceValid(_spawnedPortal)) {
      Vector2I centerCell = new Vector2I(_mapGenerator.MapWidth / 2, _mapGenerator.MapHeight / 2);
      if (!_mapGenerator.IsWalkable(centerCell)) {
        centerCell = _mapGenerator.WorldToMap(_player.SpawnPosition);
      }
      _spawnedPortal = PortalScene.Instantiate<Portal>();
      _spawnedPortal.GlobalPosition = _mapGenerator.MapToWorld(centerCell);
      // 连接传送门的信号
      _spawnedPortal.LevelCompleted += OnLevelCompleted;
      GameRootProvider.CurrentGameRoot.CallDeferred(Node.MethodName.AddChild, _spawnedPortal);
    } else {
      // 如果实例已存在（可能是在回溯后被 Destroy() 了），则复活它
      _spawnedPortal.Resurrect();
    }
  }

  /// <summary>
  /// 当玩家与传送门交互后，由此方法处理后续逻辑．
  /// </summary>
  private void OnLevelCompleted(HexMap.ClearScore score) {

    // 根据分数决定奖励
    int picks = 0;
    int minLevel = 1;
    int maxLevel = 1;
    switch (score) {
      case HexMap.ClearScore.StandardClear:
        picks = 1;
        maxLevel = 1;
        break;
      case HexMap.ClearScore.NoMiss:
        picks = 1;
        maxLevel = 2;
        break;
      case HexMap.ClearScore.NoMissNoSkill:
        picks = 2;
        maxLevel = 2;
        break;
      case HexMap.ClearScore.Perfect:
        picks = 2;
        minLevel = 2;
        maxLevel = 2;
        break;
    }

    if (picks > 0 && UpgradeSelectionMenuScene != null) {
      var upgradeMenu = UpgradeSelectionMenuScene.Instantiate<UpgradeSelectionMenu>();
      AddChild(upgradeMenu);
      upgradeMenu.UpgradeSelectionFinished += () => OnUpgradeSelectionFinished(score);
      // 传入本关专用的 RNG，以确保重玩时强化选项不变
      upgradeMenu.StartUpgradeSelection(UpgradeSelectionMenu.Mode.Gain, picks, minLevel, maxLevel, 3, _upgradeRng);
    } else {
      // 没有奖励，直接切换场景
      OnUpgradeSelectionFinished(score);
    }
  }

  /// <summary>
  /// 当强化选择结束后，切换到关间菜单．
  /// </summary>
  private void OnUpgradeSelectionFinished(HexMap.ClearScore score) {
    GameManager.Instance.CompleteLevel(score);
    GetTree().ChangeSceneToFile(InterLevelMenuScenePath);
  }

  private void OnPlayerDiedPermanently() {
    if (_pauseMenu != null) {
      _pauseMenu.ShowMenu(true);
    }
  }

  public override void _Input(InputEvent @event) {
    if (_pauseMenu != null && !_pauseMenu.Visible && @event.IsActionPressed("ui_cancel")) {
      GetViewport().SetInputAsHandled();
      _pauseMenu.ShowMenu(false);
    }
  }

  public override void _Process(double delta) {
    // 在没结束之前，持续扣除玩家生命值
    var tAxisCurio = GameManager.Instance.GetCurrentAndPendingCurios().FirstOrDefault(c => c.Type == CurioType.TAxisEnhancement);
    bool isRewindingWithCurio = tAxisCurio != null && (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding);

    if (_enemySpawner != null && !_enemySpawner.IsWaveCompleted && !_player.IsPermanentlyDead && !isRewindingWithCurio) {
      _player.Health -= (float) delta;
    }
    UpdateUILabelText();
  }

  private void OnRestartRequested() {
    GD.Print("Restarting level...");

    if (IsInstanceValid(_spawnedPortal)) {
      _spawnedPortal.QueueFree();
      _spawnedPortal = null;
    }

    _player.ResetState();
    _rewindManager.ResetHistory();
    _enemySpawner.ResetSpawner();

    foreach (var node in GetTree().GetNodesInGroup("enemies").ToList()) {
      node.QueueFree();
    }
    foreach (var node in GetTree().GetNodesInGroup("bullets").ToList()) {
      node.QueueFree();
    }
    foreach (var node in GetTree().GetNodesInGroup("pickups").ToList()) {
      node.QueueFree();
    }

    TimeManager.Instance.SetCurrentGameTime(0.0);

    // 使用之前保存的种子重新初始化 RNG，以保证强化选项不变
    _upgradeRng = new RandomNumberGenerator();
    _upgradeRng.Seed = _levelSeed;

    GameManager.Instance?.RestartLevel();
  }

  private void UpdateUILabelText() {
    if (_player == null || _uiLabel == null) return;
    string ammoText = _player.IsReloading ? $"Reloading: {_player.TimeToReloaded:F1}s" : $"Ammo: {_player.CurrentAmmo} / {GameManager.Instance.PlayerStats.MaxAmmoInt}";
    var rewindTimeLeft = _rewindManager.AvailableRewindTime;
    var timeBondText = $"Time Bond: {GameManager.Instance.TimeBond:F1}s";
    var bulletObjectCount = GetTree().GetNodesInGroup("bullets").Count;

    string curioText = "Curio: None";
    var activeCurio = GameManager.Instance.GetCurrentActiveCurio();
    if (activeCurio != null) {
      string cdText = activeCurio.CurrentCooldown > 0 ? $" (CD: {activeCurio.CurrentCooldown:F1}s)" : " (Ready)";
      curioText = $"Curio: {activeCurio.Name}{cdText}";
    }

    _uiLabel.Text = $"Time HP: {_player.Health:F2}\n" +
                    $"{timeBondText}\n" +
                    $"Rewind Left: {rewindTimeLeft:F1}s\n" +
                    $"{ammoText}\n" +
                    $"{curioText}\n" +
                    $"Bullet object count: {bulletObjectCount}";
  }
}
