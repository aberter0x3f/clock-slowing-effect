using Godot;
using Rewind;

namespace Enemy;

// 所有敌人状态快照的基类，包含通用属性
public class BaseEnemyState : RewindState {
  public Vector2 GlobalPosition;
  public Vector2 Velocity;
  public float Health;
  public float HitTimerLeft;
  public bool IsInHitState;
}

public abstract partial class BaseEnemy : RewindableCharacterBody2D {
  private float _health;
  protected Player _player;
  protected Timer _hitTimer;
  protected Label3D _healthLabel;
  protected Node3D _visualizer;
  protected EnemyPolyhedron _enemyPolyhedron;
  protected MapGenerator _mapGenerator;

  protected Node2D PlayerNode => _player.DecoyTarget ?? _player;

  [Export]
  public virtual float MaxHealth { get; protected set; } = 20.0f;

  [ExportGroup("Death Drops")]
  [Export]
  public PackedScene TimeShardScene { get; set; } // 引用 TimeShard.tscn 场景
  [Export(PropertyHint.Range, "0, 50, 1")]
  public int TimeShardCount { get; set; } = 0; // 死亡时掉落的碎片数量

  [ExportGroup("Sound Effects")]
  [Export]
  public AudioStream AttackSound { get; set; }
  [Export]
  public AudioStream DeathSound { get; set; }

  [Signal]
  public delegate void DiedEventHandler(float difficulty);

  public virtual float Health {
    get => _health;
    protected set {
      if (value <= 0) {
        _health = 0;
        var rm = RewindManager.Instance;
        if (!rm.IsPreviewing && !rm.IsRewinding) {
          Die();
        }
      } else {
        _health = float.Min(MaxHealth, value);
      }
      UpdateHealthLabel();
    }
  }

  public float Difficulty;

  protected virtual void Die() {
    if (IsDestroyed) return; // 使用基类的 IsDestroyed 属性检查

    if (DeathSound != null) {
      SoundManager.Instance.PlaySoundEffect(DeathSound, cooldown: 0.2f, volumeDb: -5f);
    }

    SpawnTimeShards(TimeShardCount);
    Destroy();
    EmitSignal(SignalName.Died, Difficulty); // 发射信号
  }

  public override void _Ready() {
    base._Ready();
    _health = MaxHealth;
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    _hitTimer = GetNode<Timer>("HitTimer");
    _visualizer = GetNode<Node3D>("Visualizer");
    _enemyPolyhedron = _visualizer.GetNode<EnemyPolyhedron>("EnemyPolyhedron");
    _healthLabel = _visualizer.GetNode<Label3D>("HealthLabel");

    // 获取并缓存地图生成器的引用，以提高性能和鲁棒性
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr($"BaseEnemy ({Name}): MapGenerator not found at 'GameRoot/MapGenerator'. TimeShards may not spawn correctly.");
    }

    UpdateHealthLabel();
    UpdateVisualizer();
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing) {
      UpdateVisualizer();
      return;
    }
    if (RewindManager.Instance.IsRewinding) return;

    Health -= (float) delta * TimeManager.Instance.TimeScale;
    UpdateVisualizer();
  }

  public virtual void TakeDamage(float damage) {
    Health -= damage;
    _enemyPolyhedron.SetHitState(true);
    _hitTimer.Start();
  }

  private void OnHitTimerTimeout() {
    _enemyPolyhedron.SetHitState(false);
  }

  protected void UpdateHealthLabel() {
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
  protected void SpawnTimeShards(int count) {
    if (TimeShardScene == null) {
      GD.PrintErr($"Enemy '{Name}' has no TimeShardScene assigned. Cannot spawn shards.");
      return;
    }

    for (int i = 0; i < count; ++i) {
      var shard = TimeShardScene.Instantiate<TimeShard>();

      // 在添加到场景前，设置好初始化所需的属性
      shard.SpawnCenter = GlobalPosition;
      shard.MapGeneratorRef = _mapGenerator;

      // 使用 CallDeferred 将节点添加到场景树，以避免在物理帧内修改物理世界
      GameRootProvider.CurrentGameRoot.CallDeferred(Node.MethodName.AddChild, shard);
    }
  }

  protected void PlayAttackSound() {
    SoundManager.Instance.PlaySoundEffect(AttackSound, cooldown: 0.2f, volumeDb: -5f);
  }

  public override RewindState CaptureState() {
    return new BaseEnemyState {
      GlobalPosition = this.GlobalPosition,
      Velocity = this.Velocity,
      Health = this.Health,
      HitTimerLeft = (float) _hitTimer.TimeLeft,
      IsInHitState = _hitTimer.TimeLeft > 0
    };
  }

  public override void RestoreState(RewindState state) {
    if (state is not BaseEnemyState bes) return;
    this.GlobalPosition = bes.GlobalPosition;
    this.Velocity = bes.Velocity;
    this.Health = bes.Health;
    _enemyPolyhedron.SetHitState(bes.IsInHitState);
    if (bes.HitTimerLeft > 0) {
      _hitTimer.Start(bes.HitTimerLeft);
    } else {
      _hitTimer.Stop();
    }
    UpdateHealthLabel();
  }
}
