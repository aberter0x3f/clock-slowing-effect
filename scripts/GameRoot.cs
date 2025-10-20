using Enemy;
using Godot;

public partial class GameRoot : Node2D {
  private Player _player;
  private Label _uiLabel;

  public override void _Ready() {
    _player = GetNode<Player>("Player");
    _uiLabel = GetNode<Label>("CanvasLayer/Label");
  }

  public override void _Process(double delta) {
    double scaledDelta = delta * TimeManager.Instance.TimeScale;

    if (_player != null && _uiLabel != null) {
      string ammoText;
      if (_player.IsReloading) {
        ammoText = $"Reloading: {_player.ReloadTimer:F1}s";
      } else {
        ammoText = $"Ammo: {_player.CurrentAmmo} / {_player.MaxAmmo}";
      }

      _uiLabel.Text = $"Time HP: {_player.Health:F2}\nTime Scale: {TimeManager.Instance.TimeScale:F2}\n{ammoText}";
    }

    var enemies = GetTree().GetNodesInGroup("enemies");

    if (enemies.Count > 0) {
      _player.Health -= delta;
    }

    foreach (BaseEnemy enemy in enemies) {
      enemy.Health -= (float) scaledDelta;
    }
  }
}
