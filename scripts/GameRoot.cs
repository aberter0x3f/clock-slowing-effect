using System.Linq;
using Godot;
using Rewind;

public partial class GameRoot : Node {
  private Player _player;
  private Label _uiLabel;
  private MapGenerator _mapGenerator;
  private EnemySpawner _enemySpawner;
  private RewindManager _rewindManager;
  private PauseMenu _pauseMenu;
  private Vector2 _playerSpawnPosition;

  [Export]
  public PackedScene PauseMenuScene { get; set; }

  public override void _Ready() {
    _player = GetNode<Player>("Player");
    _uiLabel = GetNode<Label>("CanvasLayer/Label");
    _mapGenerator = GetNode<MapGenerator>("MapGenerator");
    _enemySpawner = GetNode<EnemySpawner>("EnemySpawner");
    _rewindManager = GetNode<RewindManager>("RewindManager");

    // 实例化暂停菜单并连接玩家死亡信号
    if (PauseMenuScene != null) {
      _pauseMenu = PauseMenuScene.Instantiate<PauseMenu>();
      AddChild(_pauseMenu);
      _pauseMenu.RestartRequested += OnRestartRequested;
    }
    _player.DiedPermanently += OnPlayerDiedPermanently;

    // 1. 生成地图并获取玩家出生点
    _playerSpawnPosition = _mapGenerator.GenerateMap();
    _player.GlobalPosition = _playerSpawnPosition;

    // 2. 启动敌人生成器
    _enemySpawner.StartSpawning(_mapGenerator, _player);
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
    if (_enemySpawner != null && !_enemySpawner.IsFinished) {
      _player.Health -= (float) delta;
    }

    UpdateUILabelText();
  }

  /// <summary>
  /// 执行关卡重置的核心方法．
  /// </summary>
  private void OnRestartRequested() {
    GD.Print("Restarting level...");

    // 1. 清理所有动态生成的节点
    // 为了安全地在迭代时删除节点，我们先将集合复制到列表中．
    foreach (var node in GetTree().GetNodesInGroup("enemies").ToList()) {
      node.QueueFree();
    }
    foreach (var node in GetTree().GetNodesInGroup("bullets").ToList()) {
      node.QueueFree();
    }
    foreach (var node in GetTree().GetNodesInGroup("pickups").ToList()) {
      node.QueueFree();
    }

    // 2. 重置玩家状态
    _player.ResetState(_playerSpawnPosition);

    // 3. 重置回溯管理器
    _rewindManager.ResetHistory();

    // 4. 重置敌人生成器
    _enemySpawner.ResetSpawner();

    // 5. 重置全局时间
    TimeManager.Instance.SetCurrentGameTime(0.0);
  }

  private void UpdateUILabelText() {
    if (_player == null || _uiLabel == null) return;
    string ammoText;
    if (_player.IsReloading) {
      ammoText = $"Reloading: {_player.TimeToReloaded:F1}s";
    } else {
      ammoText = $"Ammo: {_player.CurrentAmmo} / {_player.MaxAmmo}";
    }
    var bulletObjectCount = GetTree().GetNodesInGroup("bullets").Count;
    var rewindTimeLeft = _rewindManager.AvailableRewindTime;

    _uiLabel.Text = $"Time HP: {_player.Health:F2}\n" +
                    $"Time Scale: {TimeManager.Instance.TimeScale:F2}\n" +
                    $"Rewind Left: {rewindTimeLeft:F1}s\n" +
                    $"{ammoText}\n" +
                    $"Bullet object count: {bulletObjectCount}";
  }
}
