using Enemy;
using Godot;

namespace Bullet;

public abstract partial class BaseBullet : Area2D {
  public static readonly Color GRAZE_COLOR = new Color(1.0f, 0.5f, 0.5f);

  [Export]
  public bool IsPlayerBullet { get; set; } = false;

  // 是否被擦弹
  public bool WasGrazed { get; set; } = false;

  private Color _originalColor; // 用于存储子弹的原始颜色

  [Export]
  public float Damage { get; set; } = 0f;

  public override void _Ready() {
    _originalColor = Modulate;
  }

  public void OnGrazeEnter() {
    Modulate = GRAZE_COLOR;
  }

  public void OnGrazeExit() {
    Modulate = _originalColor;
  }

  private void OnBodyEntered(Node2D body) {
    if (!IsPlayerBullet || !body.IsInGroup("enemies")) return;
    var enemy = body as BaseEnemy;
    enemy.TakeDamage(Damage);
    QueueFree();
  }
}
