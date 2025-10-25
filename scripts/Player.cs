using Bullet;
using Enemy;
using Godot;
using Rewind;

public class PlayerState : RewindState {
  public Vector2 GlobalPosition;
  public Vector2 Velocity;
  public Player.AnimationState CurrentState;
  public int CurrentAmmo;
  public bool IsReloading;
  public float TimeToReloaded;
  public float ShootTimer;
}

public partial class Player : CharacterBody2D, IRewindable {
  public enum AnimationState {
    Idle,
    Walk,
  }

  [Signal]
  public delegate void DiedPermanentlyEventHandler();

  private AnimationState _currentState = AnimationState.Idle;
  private AnimationState _lastState = AnimationState.Idle;
  private float _health;
  private AnimatedSprite3D _sprite;
  private Node2D _currentTarget = null;
  private Area2D _grazeArea; // 擦弹区域的引用
  private AnimatedSprite3D _hitPointSprite;
  private bool _timeSlowPressed = false;
  private RandomNumberGenerator _rnd = new RandomNumberGenerator();
  private Node3D _visualizer;
  private bool _isPermanentlyDead = false;
  private MapGenerator _mapGenerator;

  [ExportGroup("Movement")]
  [Export]
  public float Speed { get; set; } = 400.0f;
  [Export]
  public float SlowSpeedScale { get; set; } = 0.45f;

  [ExportGroup("Health")]
  [Export]
  public float MaxHealth { get; set; } = 60.0f;
  public float Health {
    get => _health;
    set {
      if (value <= 0) {
        _health = 0; // 确保不会变成负数
        Die();
      } else {
        _health = float.Min(value, MaxHealth);
      }
    }
  }

  [ExportGroup("Shoot")]
  [Export]
  public PackedScene Bullet { get; set; }
  [Export]
  public float ShootCooldown { get; set; } = 0.03f;
  public float ShootTimer { get; set; } = 0.0f;
  [Export]
  public float BulletSpreadNormal { get; set; } = float.Pi / 24.0f;
  [Export]
  public float BulletSpreadSlow { get; set; } = float.Pi / 60.0f;
  [Export]
  public float BulletDamage { get; set; } = 0.5f;   // 单发子弹伤害
  [Export]
  public int MaxAmmo { get; set; } = 20;
  public int CurrentAmmo { get; set; }
  [Export]
  public float ReloadTime { get; set; } = 3.0f;
  public bool IsReloading { get; private set; } = false;
  public float TimeToReloaded { get; set; } // 当前还有多长时间换完子弹

  [ExportGroup("Graze")]
  [Export]
  public float GrazeTimeBonus { get; set; } = 0.3f;
  [Export]
  public PackedScene GrazeTimeShard { get; set; }

  [ExportGroup("Rewind")]
  [Export]
  public float AutoRewindOnHitDuration { get; set; } = 3.0f;  // 被击中时自动回溯的时长

  public ulong InstanceId => GetInstanceId();

  public override void _Ready() {
    _grazeArea = GetNode<Area2D>("GrazeArea");
    _visualizer = GetNode<Node3D>("Visualizer");
    _sprite = _visualizer.GetNode<AnimatedSprite3D>("AnimatedSprite3D");
    _hitPointSprite = _visualizer.GetNode<AnimatedSprite3D>("HitPointSprite");

    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr($"Player: MapGenerator not found at 'GameRoot/MapGenerator'. TimeShards may not spawn correctly.");
    }

    CurrentAmmo = MaxAmmo; // 初始化弹药
    _health = MaxHealth;

    _sprite.Play();
    _hitPointSprite.Play();
    UpdateVisualizer();

    RewindManager.Instance.Register(this);
  }

  public override void _Process(double delta) {
    if (_isPermanentlyDead) return;

    if (RewindManager.Instance.IsPreviewing) {
      // 在预览时，我们只更新 3D 可视化对象的位置，不做任何逻辑
      UpdateVisualizer();
      return;
    }
    if (RewindManager.Instance.IsRewinding) return;

    _currentState = AnimationState.Idle;

    if (IsReloading) {
      TimeToReloaded -= (float) delta * TimeManager.Instance.TimeScale; // 换弹时间不受时间缩放影响
      if (TimeToReloaded <= 0.0f) {
        FinishReload();
      }
    }

    if (Input.IsActionJustPressed("weapon_reload")) {
      if (CurrentAmmo < MaxAmmo && !IsReloading) {
        StartReload();
      }
    }

    FindAndAimTarget();
    HandleMovement();

    _timeSlowPressed = Input.IsActionPressed("time_slow");

    if (Input.IsActionPressed("shoot")) {
      if (!IsReloading && CurrentAmmo <= 0) {
        StartReload(); // 没子弹时自动换弹
      }
      if (!IsReloading && ShootTimer <= 0.0f) {
        Shoot();
      }
    }

    ShootTimer -= (float) delta;
    UpdateState();

    UpdateVisualizer();
  }

  private void UpdateVisualizer() {
    _visualizer.GlobalPosition = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );

    _hitPointSprite.Visible = _timeSlowPressed;
  }

  private void StartReload() {
    if (IsReloading) return;
    IsReloading = true;
    TimeToReloaded = ReloadTime;
  }

  private void FinishReload() {
    IsReloading = false;
    CurrentAmmo = MaxAmmo;
    TimeToReloaded = 0.0f;
  }

  private void OnGrazeAreaBodyEntered(Node2D body) {
    if (body is BaseBullet bullet) {
      if (bullet.IsPlayerBullet) return;
      bullet.OnGrazeEnter();
      if (bullet.WasGrazed) return;
      bullet.WasGrazed = true;
      var shard = GrazeTimeShard.Instantiate<TimeShard>();
      shard.SpawnCenter = bullet.GlobalPosition;
      shard.MapGeneratorRef = _mapGenerator;
      GetTree().Root.CallDeferred(Node.MethodName.AddChild, shard);
    }
  }

  private void OnGrazeAreaBodyExited(Node2D body) {
    if (body is BaseBullet bullet) {
      if (bullet.IsPlayerBullet) return;
      bullet.OnGrazeExit();
    }
  }

  private void OnHitAreaBodyEntered(Node2D body) {
    // 玩家在回溯预览期间是无敌的
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    if (body.IsInGroup("enemies")) {
      Die();
      return;
    }
    if (body is BaseBullet bullet) {
      if (bullet.IsPlayerBullet) return;
      Die();
      return;
    }
  }

  private void FindAndAimTarget() {
    _currentTarget = null;
    float closestDistance = float.MaxValue;
    var enemies = GetTree().GetNodesInGroup("enemies");
    foreach (BaseEnemy enemy in enemies) {
      if (enemy.IsDestroyed) {
        continue;
      }
      float distance = GlobalPosition.DistanceSquaredTo(enemy.GlobalPosition);
      if (distance < closestDistance) {
        closestDistance = distance;
        _currentTarget = enemy;
      }
    }
  }

  private void Shoot() {
    if (CurrentAmmo <= 0) return;

    ShootTimer = ShootCooldown;
    --CurrentAmmo;

    // 实例化 SimpleBullet
    var bullet = Bullet.Instantiate<SimpleBullet>();
    bullet.IsPlayerBullet = true; // 明确设置这是玩家子弹
    bullet.Damage = BulletDamage;
    bullet.GlobalPosition = GlobalPosition;

    var randomRotationSigma = _timeSlowPressed ? BulletSpreadSlow : BulletSpreadNormal;
    var randomRotation = _rnd.Randfn(0, randomRotationSigma);

    Vector2 direction;
    if (_currentTarget != null) {
      direction = (_currentTarget.GlobalPosition - GlobalPosition).Normalized().Rotated(randomRotation);
    } else {
      direction = Vector2.Right.Rotated(randomRotation);
    }

    // 设置初始速度和旋转
    bullet.Velocity = direction * bullet.InitialSpeed;
    bullet.GlobalRotation = direction.Angle();

    GetTree().Root.AddChild(bullet);
  }

  private void HandleMovement() {
    Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
    Velocity = direction * (_timeSlowPressed ? Speed * SlowSpeedScale : Speed);
    if (!direction.IsZeroApprox()) {
      _currentState = AnimationState.Walk;
    }
    MoveAndSlide();
  }

  public void ResetState(Vector2 spawnPosition) {
    GlobalPosition = spawnPosition;
    Velocity = Vector2.Zero;
    _health = MaxHealth; // 直接设置字段以避免触发 Die()
    CurrentAmmo = MaxAmmo;
    IsReloading = false;
    TimeToReloaded = 0.0f;
    ShootTimer = 0.0f;
    _isPermanentlyDead = false; // 非常重要：重置永久死亡状态
    _currentState = AnimationState.Idle;
    UpdateState();
    UpdateVisualizer();
  }

  public void Die() {
    if (_isPermanentlyDead) return;

    // 尝试触发自动回溯
    bool didRewind = RewindManager.Instance.TriggerAutoRewind(AutoRewindOnHitDuration);

    // 如果回溯失败（例如时间不够），则触发死亡菜单
    if (!didRewind) {
      GD.Print("Game Over! Not enough rewind time to survive.");
      _isPermanentlyDead = true;
      EmitSignal(SignalName.DiedPermanently);
    }
    // 如果回溯成功，玩家将「复活」到几秒前的状态，游戏继续
  }

  private void UpdateState() {
    if (_currentState == _lastState) {
      return;
    }
    switch (_currentState) {
      case AnimationState.Idle:
        _sprite.Play("idle");
        break;
      case AnimationState.Walk:
        _sprite.Play("walk");
        break;
    }
    _lastState = _currentState;
  }

  public RewindState CaptureState() {
    return new PlayerState {
      GlobalPosition = this.GlobalPosition,
      Velocity = this.Velocity,
      CurrentState = this._currentState,
      CurrentAmmo = this.CurrentAmmo,
      IsReloading = this.IsReloading,
      TimeToReloaded = this.TimeToReloaded,
      ShootTimer = this.ShootTimer
    };
  }

  public void RestoreState(RewindState state) {
    if (state is not PlayerState ps) return;

    this.GlobalPosition = ps.GlobalPosition;
    this.Velocity = ps.Velocity;
    this._currentState = ps.CurrentState;
    this.CurrentAmmo = ps.CurrentAmmo;
    this.IsReloading = ps.IsReloading;
    this.TimeToReloaded = ps.TimeToReloaded;
    this.ShootTimer = ps.ShootTimer;

    // 恢复状态后可能需要更新一些依赖状态的视觉效果
    UpdateState();
  }

  // Player 不会被 Destroy 或 Resurrect，所以提供空实现
  public void Destroy() { }
  public void Resurrect() { }

  public override void _ExitTree() {
    if (RewindManager.Instance != null) {
      RewindManager.Instance.Unregister(this);
    }
    base._ExitTree();
  }
}
