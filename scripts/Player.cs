using System.Collections.Generic;
using Bullet;
using Enemy;
using Godot;
using Rewind;

public class PlayerState : RewindState {
  public Vector2 GlobalPosition;
  public Vector2 Velocity;
  public Player.AnimationState CurrentAnimationState;
  public int CurrentAmmo;
  public bool IsReloading;
  public float TimeToReloaded;
  public float ShootTimer;
  // 以下字段仅用于「从当前阶段重来」，回溯系统会忽略它们
  public float Health;
  public float TimeBond;
}

public partial class Player : CharacterBody2D, IRewindable {
  public enum AnimationState {
    Idle,
    Walk,
  }

  [Signal]
  public delegate void DiedPermanentlyEventHandler();

  private AnimationState _currentAnimationState = AnimationState.Idle;
  private AnimationState _lastAnimationState = AnimationState.Idle;
  private AnimatedSprite3D _sprite;
  private Node2D _currentTarget = null;
  private Area2D _grazeArea; // 擦弹区域的引用
  private CollisionShape2D _grazeAreaShape;
  private AnimatedSprite3D _hitPointSprite;
  private bool _timeSlowPressed = false;
  private RandomNumberGenerator _rnd = new RandomNumberGenerator();
  private Node3D _visualizer;
  private MapGenerator _mapGenerator;
  private Area2D _interactionArea;
  private readonly List<IInteractable> _nearbyInteractables = new();
  private IInteractable _closestInteractable = null;
  private float _beginningHealth;
  private float _beginningTimeBond;
  private Camera3D _camera;

  private PlayerStats Stats => GameManager.Instance.PlayerStats;

  public float Health {
    get => GameManager.Instance.CurrentPlayerHealth;
    set {
      if (value <= 0) {
        GameManager.Instance.CurrentPlayerHealth = 0; // 确保不会变成负数
        Die();
      } else {
        GameManager.Instance.CurrentPlayerHealth = Mathf.Min(value, Stats.MaxHealth);
      }
    }
  }

  [Export]
  public PackedScene Bullet { get; set; }
  [Export]
  public PackedScene GrazeTimeShard { get; set; }
  [Export]
  public float AutoRewindOnHitDuration { get; set; } = 3.0f;  // 被击中时自动回溯的时长

  [ExportGroup("Camera")]
  [Export]
  private Vector3 _normalCameraPosition = new Vector3(0, 2.5f, 3.0f);
  private Vector3 _normalCameraRotationDegrees = new Vector3(-45f, 0, 0);
  [Export]
  private Vector3 _slowCameraPosition = new Vector3(0, 2f, 1.5f);
  private Vector3 _slowCameraRotationDegrees = new Vector3(-60f, 0, 0);
  [Export(PropertyHint.Range, "0.1, 20.0, 0.1")]
  private float _cameraSmoothingSpeed = 10.0f;

  public float ShootTimer { get; set; } = 0.0f;
  public int CurrentAmmo { get; private set; }
  public bool IsReloading { get; private set; } = false;
  public float TimeToReloaded { get; private set; } // 当前还有多长时间换完子弹
  public Vector2 SpawnPosition { get; set; }
  public bool IsPermanentlyDead = false;

  public ulong InstanceId => GetInstanceId();

  public override void _Ready() {
    _grazeArea = GetNode<Area2D>("GrazeArea");
    _grazeAreaShape = _grazeArea.GetNode<CollisionShape2D>("CollisionShape2D");
    _visualizer = GetNode<Node3D>("Visualizer");
    _sprite = _visualizer.GetNode<AnimatedSprite3D>("AnimatedSprite3D");
    _hitPointSprite = _visualizer.GetNode<AnimatedSprite3D>("HitPointSprite");
    _interactionArea = GetNode<Area2D>("InteractionArea");
    _interactionArea.AreaEntered += OnInteractionAreaEntered;
    _interactionArea.AreaExited += OnInteractionAreaExited;
    _camera = _visualizer.GetNode<Camera3D>("Camera3D");

    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.Print($"Player: MapGenerator not found at 'GameRoot/MapGenerator'. TimeShards may not spawn correctly.");
    }

    _beginningHealth = Health;
    _beginningTimeBond = GameManager.Instance.TimeBond;

    ResetState();

    _sprite.Play();
    _hitPointSprite.Play();

    RewindManager.Instance.Register(this);
  }

  public override void _Process(double delta) {
    if (IsPermanentlyDead) return;

    _timeSlowPressed = Input.IsActionPressed("time_slow");

    UpdateCameraTransform((float) delta);

    if (RewindManager.Instance.IsPreviewing) {
      // 在预览时，我们只更新 3D 可视化对象的位置，不做任何逻辑
      UpdateVisualizer();
      return;
    }
    if (RewindManager.Instance.IsRewinding) return;

    // 每帧更新动态属性
    Stats.ApplyDynamicBonuses(Health);
    (_grazeAreaShape.Shape as CircleShape2D).Radius = Stats.GrazeRadius;

    _currentAnimationState = AnimationState.Idle;

    UpdateInteractionTarget();
    HandleInteractionInput();

    if (IsReloading) {
      TimeToReloaded -= (float) delta * TimeManager.Instance.TimeScale; // 换弹时间不受时间缩放影响
      if (TimeToReloaded <= 0.0f) {
        FinishReload();
      }
    }

    if (Input.IsActionJustPressed("weapon_reload")) {
      if (CurrentAmmo < Stats.MaxAmmo && !IsReloading) {
        StartReload();
      }
    }

    FindAndAimTarget();
    HandleMovement();

    if (Input.IsActionPressed("shoot")) {
      if (!IsReloading && CurrentAmmo <= 0) {
        StartReload(); // 没子弹时自动换弹
      }
      if (!IsReloading && ShootTimer <= 0.0f) {
        Shoot();
      }
    }

    ShootTimer -= (float) delta;
    UpdateAnimationState();

    UpdateVisualizer();
  }


  private void UpdateCameraTransform(float delta) {
    if (_camera == null) return;

    Vector3 targetPosition = _timeSlowPressed ? _slowCameraPosition : _normalCameraPosition;
    Vector3 targetRotationDegrees = _timeSlowPressed ? _slowCameraRotationDegrees : _normalCameraRotationDegrees;
    _camera.Position = _camera.Position.Lerp(targetPosition, _cameraSmoothingSpeed * delta);
    _camera.RotationDegrees = _camera.RotationDegrees.Lerp(targetRotationDegrees, _cameraSmoothingSpeed * delta);
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
    TimeToReloaded = Stats.ReloadTime;
  }

  private void FinishReload() {
    IsReloading = false;
    CurrentAmmo = Stats.MaxAmmoInt;
    TimeToReloaded = 0.0f;
  }

  private void OnGrazeAreaBodyEntered(Node2D body) {
    if (body is BaseBullet bullet) {
      if (bullet.IsPlayerBullet) return;
      bullet.OnGrazeEnter();
      if (bullet.WasGrazed) return;
      bullet.WasGrazed = true;
      var shard = GrazeTimeShard.Instantiate<TimeShard>();
      shard.TimeBonus = Stats.GrazeTimeBonus;
      shard.SpawnCenter = bullet.GlobalPosition;
      shard.MapGeneratorRef = _mapGenerator;
      GameRootProvider.CurrentGameRoot.CallDeferred(Node.MethodName.AddChild, shard);
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

  private void HandleInteractionInput() {
    if (Input.IsActionJustPressed("interact") && _closestInteractable != null) {
      _closestInteractable.Interact();
    }
  }

  // 更新交互目标
  private void UpdateInteractionTarget() {
    IInteractable newClosest = null;
    float minDistanceSq = float.MaxValue;

    // 遍历所有在范围内的可交互对象
    foreach (var interactable in _nearbyInteractables) {
      if (interactable is not Node2D node) continue;

      float distanceSq = this.GlobalPosition.DistanceSquaredTo(node.GlobalPosition);
      if (distanceSq < minDistanceSq) {
        minDistanceSq = distanceSq;
        newClosest = interactable;
      }
    }

    // 如果最近的目标发生了变化
    if (newClosest != _closestInteractable) {
      // 取消旧目标的高亮
      _closestInteractable?.SetHighlight(false);
      // 高亮新目标
      newClosest?.SetHighlight(true);
      // 更新当前目标
      _closestInteractable = newClosest;
    }
  }

  // 处理进入交互范围的信号
  private void OnInteractionAreaEntered(Area2D area) {
    if (area is IInteractable interactable) {
      if (!_nearbyInteractables.Contains(interactable)) {
        _nearbyInteractables.Add(interactable);
      }
    }
  }

  // 处理离开交互范围的信号
  private void OnInteractionAreaExited(Area2D area) {
    if (area is IInteractable interactable) {
      // 如果离开的是当前高亮的目标，取消高亮
      if (interactable == _closestInteractable) {
        _closestInteractable.SetHighlight(false);
        _closestInteractable = null;
      }
      _nearbyInteractables.Remove(interactable);
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
      if (enemy.IsDestroyed) {
        continue;
      }
      var collider = enemy.GetNodeOrNull<CollisionShape2D>("CollisionShape2D");
      if (collider == null || collider.Disabled) {
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

    ShootTimer = Stats.ShootCooldown;
    --CurrentAmmo;

    // 实例化 SimpleBullet
    var bullet = Bullet.Instantiate<SimpleBullet>();
    bullet.IsPlayerBullet = true; // 明确设置这是玩家子弹
    bullet.Damage = Stats.BulletDamage;
    bullet.GlobalPosition = GlobalPosition;

    var randomRotationSigma = _timeSlowPressed ? Stats.BulletSpreadSlow : Stats.BulletSpreadNormal;
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

    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  private void HandleMovement() {
    Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
    Velocity = direction * Stats.MovementSpeed * (_timeSlowPressed ? Stats.SlowMovementSpeedScale : 1f);
    if (!direction.IsZeroApprox()) {
      _currentAnimationState = AnimationState.Walk;
    }
    MoveAndSlide();
  }

  public void ResetState() {
    GlobalPosition = SpawnPosition;
    Velocity = Vector2.Zero;
    Health = _beginningHealth;
    GameManager.Instance.TimeBond = _beginningTimeBond;
    Stats.ApplyDynamicBonuses(Health);
    (_grazeAreaShape.Shape as CircleShape2D).Radius = Stats.GrazeRadius;
    IsReloading = false;
    TimeToReloaded = 0.0f;
    ShootTimer = 0.0f;
    CurrentAmmo = Stats.MaxAmmoInt;
    IsPermanentlyDead = false;
    _currentAnimationState = AnimationState.Idle;
    UpdateAnimationState();
    UpdateVisualizer();
  }

  public void Die() {
    if (IsPermanentlyDead) return;

    // 尝试触发自动回溯
    bool didRewind = RewindManager.Instance.TriggerAutoRewind(AutoRewindOnHitDuration);

    // 如果回溯失败（例如时间不够），则触发死亡菜单
    if (!didRewind) {
      GD.Print("Game Over! Not enough rewind time to survive.");
      IsPermanentlyDead = true;
      EmitSignal(SignalName.DiedPermanently);
    }
    // 如果回溯成功，玩家将「复活」到几秒前的状态，游戏继续
  }

  private void UpdateAnimationState() {
    if (_currentAnimationState == _lastAnimationState) {
      return;
    }
    switch (_currentAnimationState) {
      case AnimationState.Idle:
        _sprite.Play("idle");
        break;
      case AnimationState.Walk:
        _sprite.Play("walk");
        break;
    }
  }

  public RewindState CaptureState() {
    return new PlayerState {
      GlobalPosition = this.GlobalPosition,
      Velocity = this.Velocity,
      CurrentAnimationState = this._currentAnimationState,
      CurrentAmmo = this.CurrentAmmo,
      IsReloading = this.IsReloading,
      TimeToReloaded = this.TimeToReloaded,
      ShootTimer = this.ShootTimer,
      // Health 和 TimeBond 仅用于阶段重启，回溯系统不会使用它们
      Health = this.Health,
      TimeBond = GameManager.Instance.TimeBond
    };
  }

  public void RestoreState(RewindState state) {
    if (state is not PlayerState ps) return;

    this.GlobalPosition = ps.GlobalPosition;
    this.Velocity = ps.Velocity;
    this._currentAnimationState = ps.CurrentAnimationState;
    this.CurrentAmmo = ps.CurrentAmmo;
    this.IsReloading = ps.IsReloading;
    this.TimeToReloaded = ps.TimeToReloaded;
    this.ShootTimer = ps.ShootTimer;

    // 注意：回溯系统不应该恢复 Health 和 TimeBond，
    // 但「从当前阶段重来」功能会手动恢复它们．

    // 恢复状态后可能需要更新一些依赖状态的视觉效果
    UpdateAnimationState();
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
