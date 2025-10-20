using Enemy;
using Godot;

public partial class Bullet : Area2D {
  [Export]
  public float Speed { get; set; } = 400.0f;

  public Vector2 Direction { get; set; } = Vector2.Zero;

  [Export]
  public bool IsPlayerBullet { get; set; } = false;

  // 是否被擦弹
  public bool WasGrazed { get; set; } = false;

  private Color _originalColor; // 用于存储子弹的原始颜色

  [Export]
  public Color GrazeColor { get; set; } = new Color(1.0f, 0.5f, 0.5f);

  [Export]
  public float Damage { get; set; } = 0f;

  public override void _Ready() {
    _originalColor = Modulate;
  }

  public override void _Process(double delta) {
    Position += Direction * Speed * (float) delta * TimeManager.Instance.TimeScale;
  }

  public void OnGrazeEnter() {
    Modulate = GrazeColor;
  }

  public void OnGrazeExit() {
    Modulate = _originalColor;
  }

  private void OnBodyEntered(Node2D body) {
    if (IsPlayerBullet && body.IsInGroup("enemies") && body is BaseEnemy enemy) {
      GD.Print("Hit an enemy!");
      enemy.TakeDamage(Damage);
      GD.Print($"Enemy health: {enemy.Health}");
      QueueFree();
    }
  }
}
