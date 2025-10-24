using Godot;

public partial class GameRoot : Node {
  private Player _player;
  private Label _uiLabel;
  private MapGenerator _mapGenerator;
  private EnemySpawner _enemySpawner;

  public override void _Ready() {
    _player = GetNode<Player>("Player");
    _uiLabel = GetNode<Label>("CanvasLayer/Label");
    _mapGenerator = GetNode<MapGenerator>("MapGenerator");
    _enemySpawner = GetNode<EnemySpawner>("EnemySpawner");

    // 1. 生成地图并获取玩家出生点
    Vector2 playerSpawnPosition = _mapGenerator.GenerateMap();
    _player.GlobalPosition = playerSpawnPosition;

    // 2. 启动敌人生成器
    _enemySpawner.StartSpawning(_mapGenerator, _player);
  }

  public override void _Process(double delta) {
    if (_player != null && _uiLabel != null) {
      string ammoText;
      if (_player.IsReloading) {
        ammoText = $"Reloading: {_player.TimeToReloaded:F1}s";
      } else {
        ammoText = $"Ammo: {_player.CurrentAmmo} / {_player.MaxAmmo}";
      }
      _uiLabel.Text = $"Time HP: {_player.Health:F2}\nTime Scale: {TimeManager.Instance.TimeScale:F2}\n{ammoText}";
    }

    if (GetTree().GetNodesInGroup("enemies").Count > 0) {
      _player.Health -= (float) delta;
    }
  }
}
