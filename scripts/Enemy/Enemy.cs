using Godot;

namespace Enemy;

public abstract partial class BaseEnemy : CharacterBody2D {
  protected Node2D _player;
  protected Timer _hitTimer;
  protected Color _originalColor;
  protected float _health = 100.0f;

  [Export]
  public Color HitColor { get; set; } = new Color(1.0f, 0.5f, 0.5f);

  public float Health {
    get => _health;
    set {
      if (value <= 0) {
        _health = 0;
        QueueFree();
      } else {
        _health = value;
      }
    }
  }

  public override void _Ready() {
    _player = GetTree().Root.GetNode<Node2D>("GameRoot/Player");
    _hitTimer = GetNode<Timer>("HitTimer");
    _originalColor = Modulate;
  }

  public void TakeDamage(float damage) {
    Health -= damage;
    Modulate = HitColor;
    _hitTimer.Start();
  }

  private void OnHitTimerTimeout() {
    Modulate = _originalColor;
  }
}
