using System.Linq;
using Godot;
using Rewind;

namespace Room;

public partial class Combat : Node {
  private Player _player;
  private Label _uiLabel;
  private MapGenerator _mapGenerator;
  private EnemySpawner _enemySpawner;
  private RewindManager _rewindManager;
  private PauseMenu _pauseMenu;
  private Vector2 _playerSpawnPosition;
  private Portal _spawnedPortal = null;

  [Export]
  public PackedScene PauseMenuScene { get; set; }

  [Export]
  public PackedScene PortalScene { get; set; }

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

    // 连接敌人生成器完成波次的信号
    _enemySpawner.WaveCompleted += OnWaveCompleted;

    _playerSpawnPosition = _mapGenerator.GenerateMap();
    _player.GlobalPosition = _playerSpawnPosition;

    // 从 GameManager 配置 EnemySpawner
    ConfigureEnemySpawner();

    _enemySpawner.StartSpawning(_mapGenerator, _player);
  }

  private void ConfigureEnemySpawner() {
    if (GameManager.Instance?.CurrentDifficulty == null) {
      GD.PrintErr("Combat: GameManager not initialized. Using default spawner values.");
      return;
    }

    var gm = GameManager.Instance;
    _enemySpawner.TotalDifficultyBudget = gm.CurrentDifficulty.InitialTotalDifficulty * gm.DifficultyMultiplier;
    _enemySpawner.MaxConcurrentDifficulty = gm.CurrentDifficulty.InitialMaxConcurrentDifficulty * gm.DifficultyMultiplier;

    GD.Print($"Configuring spawner. Total Budget: {_enemySpawner.TotalDifficultyBudget}, Max Concurrent: {_enemySpawner.MaxConcurrentDifficulty}");
  }

  private void OnWaveCompleted() {
    GD.Print("Combat scene received WaveCompleted signal. Spawning portal.");
    if (PortalScene == null) {
      GD.PrintErr("PortalScene is not set in the Combat scene!");
      return;
    }

    // 检查传送门实例是否有效
    if (!IsInstanceValid(_spawnedPortal)) {
      // 如果实例不存在，则创建一个新的
      Vector2I centerCell = new Vector2I(_mapGenerator.MapWidth / 2, _mapGenerator.MapHeight / 2);
      if (!_mapGenerator.IsWalkable(centerCell)) {
        centerCell = _mapGenerator.WorldToMap(_playerSpawnPosition);
      }

      _spawnedPortal = PortalScene.Instantiate<Portal>();
      _spawnedPortal.GlobalPosition = _mapGenerator.MapToWorld(centerCell);
      GameRootProvider.CurrentGameRoot.CallDeferred(Node.MethodName.AddChild, _spawnedPortal);
    } else {
      // 如果实例已存在（可能是在回溯后被 Destroy() 了），则复活它
      _spawnedPortal.Resurrect();
    }
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
    if (_enemySpawner != null && !_enemySpawner.IsWaveCompleted) {
      _player.Health -= (float) delta;
    }
    UpdateUILabelText();
  }

  private void OnRestartRequested() {
    GD.Print("Restarting level...");

    foreach (var node in GetTree().GetNodesInGroup("enemies").ToList()) {
      node.QueueFree();
    }
    foreach (var node in GetTree().GetNodesInGroup("bullets").ToList()) {
      node.QueueFree();
    }
    foreach (var node in GetTree().GetNodesInGroup("pickups").ToList()) {
      node.QueueFree();
    }

    _player.ResetState(_playerSpawnPosition);
    _rewindManager.ResetHistory();
    _enemySpawner.ResetSpawner();
    TimeManager.Instance.SetCurrentGameTime(0.0);
  }

  private void UpdateUILabelText() {
    if (_player == null || _uiLabel == null) return;
    string ammoText = _player.IsReloading ? $"Reloading: {_player.TimeToReloaded:F1}s" : $"Ammo: {_player.CurrentAmmo} / {_player.MaxAmmo}";
    var bulletObjectCount = GetTree().GetNodesInGroup("bullets").Count;
    var rewindTimeLeft = _rewindManager.AvailableRewindTime;

    _uiLabel.Text = $"Time HP: {_player.Health:F2}\n" +
                    $"Time Scale: {TimeManager.Instance.TimeScale:F2}\n" +
                    $"Rewind Left: {rewindTimeLeft:F1}s\n" +
                    $"{ammoText}\n" +
                    $"Bullet object count: {bulletObjectCount}";
  }
}
