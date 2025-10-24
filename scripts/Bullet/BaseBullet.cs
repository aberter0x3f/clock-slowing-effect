using Enemy;
using Godot;

namespace Bullet;

public abstract partial class BaseBullet : Area2D {
  public static readonly Color GRAZE_COLOR = new Color(1.0f, 0.5f, 0.5f);

  [Export]
  public bool IsPlayerBullet { get; set; } = false;


  [Export]
  public float Damage { get; set; } = 0f;

  // 是否被擦弹
  public bool WasGrazed { get; set; } = false;

  private Color _originalColor; // 用于存储子弹的原始颜色
  protected Node3D _visualizer;
  protected SpriteBase3D _sprite;

  public override void _Ready() {
    _visualizer = GetNodeOrNull<Node3D>("Visualizer");
    _sprite = _visualizer.GetNode<SpriteBase3D>("Sprite");
    _originalColor = _sprite.Modulate;
    UpdateVisualizer();
  }

  public void OnGrazeEnter() {
    _sprite.Modulate = GRAZE_COLOR;
  }

  public void OnGrazeExit() {
    _sprite.Modulate = _originalColor;
  }

  private void OnBodyEntered(Node2D body) {
    if (!IsPlayerBullet || !body.IsInGroup("enemies")) return;
    var enemy = body as BaseEnemy;
    enemy.TakeDamage(Damage);
    QueueFree();
  }

  public override void _Process(double delta) {
    UpdateVisualizer();
  }

  protected virtual void UpdateVisualizer() {
    if (_visualizer != null) {
      _visualizer.GlobalPosition = new Vector3(
        GlobalPosition.X * GameConstants.WorldScaleFactor,
        GameConstants.GamePlaneY,
        GlobalPosition.Y * GameConstants.WorldScaleFactor
      );
      _visualizer.Rotation = new Vector3(0, 0, -GlobalRotation);
    }
  }
}
