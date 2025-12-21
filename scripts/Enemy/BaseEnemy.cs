using Godot;
using Rewind;

namespace Enemy;

public class BaseEnemyState : RewindState {
  public Vector3 GlobalPosition;
  public Vector3 Velocity;
  public float Health;
  public float HitTimerLeft;
  public bool IsInHitState;
}

public abstract partial class BaseEnemy : RewindableCharacterBody3D {
  private float _health;
  protected Player _player;
  protected Timer _hitTimer;
  protected Label3D _healthLabel;
  protected EnemyPolyhedron _enemyPolyhedron;
  protected MapGenerator _mapGenerator;

  protected Node3D PlayerNode => _player.DecoyTarget ?? _player;

  [Export]
  public virtual float MaxHealth { get; protected set; } = 20.0f;

  [ExportGroup("Death Drops")]
  [Export]
  public PackedScene TimeShardScene { get; set; } // 引用 TimeShard.tscn 场景
  [Export(PropertyHint.Range, "0, 50, 1")]
  public int TimeShardCount { get; set; } = 0; // 死亡时掉落的碎片数量

  [ExportGroup("Time")]
  [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
  public float TimeScaleSensitivity { get; set; } = 1.0f; // 时间缩放敏感度．0=完全忽略, 1=完全受影响．

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

    SoundManager.Instance.Play(SoundEffect.EnemyDeath);

    SpawnTimeShards(TimeShardCount);
    Destroy();
    EmitSignal(SignalName.Died, Difficulty); // 发射信号
  }

  public override void _Ready() {
    base._Ready();
    _health = MaxHealth;
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    _hitTimer = GetNode<Timer>("HitTimer");
    _enemyPolyhedron = GetNode<EnemyPolyhedron>("EnemyPolyhedron");
    _healthLabel = GetNode<Label3D>("HealthLabel");

    // 获取并缓存地图生成器的引用，以提高性能和鲁棒性
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr($"BaseEnemy ({Name}): MapGenerator not found at 'GameRoot/MapGenerator'. TimeShards may not spawn correctly.");
    }

    UpdateHealthLabel();

    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    UpdateEnemy(0, effectiveTimeScale);
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing) return;
    if (RewindManager.Instance.IsRewinding) return;

    Health -= (float) delta * TimeManager.Instance.TimeScale;

    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;

    UpdateEnemy(scaledDelta, effectiveTimeScale);
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

  public virtual void UpdateEnemy(float scaledDelta, float effectiveTimeScale) { }

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
      shard.StartPosition = GlobalPosition;
      shard.MapGeneratorRef = _mapGenerator;

      // 使用 CallDeferred 将节点添加到场景树，以避免在物理帧内修改物理世界
      GameRootProvider.CurrentGameRoot.CallDeferred(Node.MethodName.AddChild, shard);
    }
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
  }
}
