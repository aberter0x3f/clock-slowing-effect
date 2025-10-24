using Godot;

namespace Enemy;

public abstract partial class BaseEnemy : CharacterBody2D {
  public static readonly Color HIT_COLOR = new Color(1.0f, 0.5f, 0.5f);

  protected Player _player;
  protected Timer _hitTimer;
  protected Color _originalColor;
  protected float _health;
  protected Label3D _healthLabel;
  protected Node3D _visualizer;
  protected SpriteBase3D _sprite;
  private bool _isDead = false;

  [Export]
  public float MaxHealth { get; set; } = 20.0f;
  [Export]
  public float KillBonus { get; set; } = 0.0f;

  [Signal]
  public delegate void DiedEventHandler(float difficulty);

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

  public float Difficulty;

  public virtual void Die() {
    if (_isDead) return;
    _isDead = true;
    if (_player != null) {
      _player.Health += KillBonus;
    }
    EmitSignal(SignalName.Died, Difficulty); // 发射信号
    QueueFree();
  }

  public override void _Ready() {
    _health = MaxHealth;
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    _hitTimer = GetNode<Timer>("HitTimer");
    _visualizer = GetNode<Node3D>("Visualizer");
    _sprite = _visualizer.GetNode<SpriteBase3D>("Sprite");
    _healthLabel = _visualizer.GetNode<Label3D>("HealthLabel");
    _originalColor = _sprite.Modulate;

    UpdateHealthLabel();
    UpdateVisualizer();
  }

  public override void _Process(double delta) {
    Health -= (float) delta * TimeManager.Instance.TimeScale;
    UpdateVisualizer();
  }

  public void TakeDamage(float damage) {
    Health -= damage;
    _sprite.Modulate = HIT_COLOR;
    _hitTimer.Start();
  }

  private void OnHitTimerTimeout() {
    _sprite.Modulate = _originalColor;
  }

  private void UpdateHealthLabel() {
    if (_healthLabel != null) {
      _healthLabel.Text = Mathf.Ceil(Health).ToString();
    }
  }

  protected void UpdateVisualizer() {
    _visualizer.GlobalPosition = new Vector3(GlobalPosition.X * 0.01f, 0.3f, GlobalPosition.Y * 0.01f);
  }
}
