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
  private MapGenerator _mapGenerator; // 添加对地图生成器的引用

  [Export]
  public float MaxHealth { get; set; } = 20.0f;

  [ExportGroup("Death Drops")]
  [Export]
  public PackedScene TimeShardScene { get; set; } // 引用 TimeShard.tscn 场景
  [Export(PropertyHint.Range, "0, 50, 1")]
  public int TimeShardCount { get; set; } = 0; // 死亡时掉落的碎片数量

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

    SpawnTimeShards();

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

    // 获取并缓存地图生成器的引用，以提高性能和鲁棒性
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr($"BaseEnemy ({Name}): MapGenerator not found at 'GameRoot/MapGenerator'. TimeShards may not spawn correctly.");
    }

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

  protected virtual void UpdateVisualizer() {
    _visualizer.GlobalPosition = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );
  }

  /// <summary>
  /// 在敌人死亡的位置生成一堆时间碎片．
  /// </summary>
  private void SpawnTimeShards() {
    if (TimeShardScene == null) {
      GD.PrintErr($"Enemy '{Name}' has no TimeShardScene assigned. Cannot spawn shards.");
      return;
    }

    for (int i = 0; i < TimeShardCount; i++) {
      var shard = TimeShardScene.Instantiate<TimeShard>();

      // 在添加到场景前，设置好初始化所需的属性。
      shard.SpawnCenter = GlobalPosition;
      shard.MapGeneratorRef = _mapGenerator;

      // 使用 CallDeferred 将节点添加到场景树，以避免在物理帧内修改物理世界。
      GetTree().Root.CallDeferred(Node.MethodName.AddChild, shard);
    }
  }
}
