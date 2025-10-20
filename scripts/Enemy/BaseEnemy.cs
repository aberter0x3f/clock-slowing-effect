using Godot;

namespace Enemy;

public abstract partial class BaseEnemy : CharacterBody2D {
  protected Player _player;
  protected Timer _hitTimer;
  protected Color _originalColor;
  protected float _health = 100.0f;
  protected Label _healthLabel;

  [Export]
  public Color HitColor { get; set; } = new Color(1.0f, 0.5f, 0.5f);

  [Export]
  public float KillBonus { get; set; } = 0.0f;

  public float Health {
    get => _health;
    set {
      if (value <= 0) {
        _health = 0;
        Die();
      } else {
        _health = value;
      }
      UpdateHealthLabel();
    }
  }

  public virtual void Die() {
    _player.Health += KillBonus;
    QueueFree();
  }

  public override void _Ready() {
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    _hitTimer = GetNode<Timer>("HitTimer");
    _originalColor = Modulate;
    _healthLabel = GetNode<Label>("HealthLabel");

    UpdateHealthLabel();
  }

  public void TakeDamage(float damage) {
    Health -= damage;
    Modulate = HitColor;
    _hitTimer.Start();
  }

  private void OnHitTimerTimeout() {
    Modulate = _originalColor;
  }

  private void UpdateHealthLabel() {
    if (_healthLabel != null) {
      _healthLabel.Text = Mathf.Ceil(Health).ToString();
    }
  }
}
