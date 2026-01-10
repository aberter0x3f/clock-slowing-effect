using System.Collections.Generic;
using Bullet;
using Curio;
using Enemy;
using Godot;
using Rewind;

public class PlayerState : RewindState {
  public Vector3 GlobalPosition;
  public Vector3 Velocity;
  public bool FlipSprite;
  public int CurrentAmmo;
  public bool IsReloading;
  public float TimeToReloaded;
  public float ShootTimer;
  public bool IsInvincible;
  public Dictionary<CurioType, float> CurioCooldowns = new();
  public bool DecoyActive;
  public Vector3 DecoyPosition;
  public float DecoyRemoveTimer;
  // Hyper 机制状态
  public float HyperGauge;
  public bool IsHyperActive;
  public float HyperTimer;
  public float HyperInvincibilityTimer;
  // 以下字段仅用于「从当前阶段重来」，回溯系统会忽略它们
  public float Health;
  public float TimeBond;
}

public partial class Player : CharacterBody3D, IRewindable {
  [Signal]
  public delegate void DiedPermanentlyEventHandler();

  private AnimatedSprite3D _sprite;
  private bool _flipSprite = false;
  private Node3D _currentTarget = null;
  private Area3D _grazeArea; // 擦弹区域的引用
  private CollisionShape3D _grazeAreaShape;
  private AnimatedSprite3D _hitPointSprite;
  private CollisionShape3D _hitPointShape;
  private Node3D _landingIndicator;
  private bool _timeSlowPressed = false;
  private RandomNumberGenerator _rnd = new RandomNumberGenerator();
  private MapGenerator _mapGenerator;
  private Area3D _interactionArea;
  private readonly List<IInteractable> _nearbyInteractables = new();
  private IInteractable _closestInteractable = null;
  private float _beginningHealth;
  private float _beginningTimeBond;
  private float _beginningHyperGauge;
  private Camera3D _camera;

  // 奇物系统状态
  private bool _curioUsePressed = false;
  public bool IsGoldenBody { get; set; } = false;
  public Node3D DecoyTarget { get; private set; }
  private float _decoyRemoveTimer = 0;

  // Hyper 机制状态
  public bool IsHyperActive { get; private set; } = false;
  private float _hyperTimer = 0f;
  private float _hyperInvincibilityTimer = 0f;

  public PlayerStats Stats => GameManager.Instance.PlayerStats;

  public float Health {
    get => GameManager.Instance.CurrentPlayerHealth;
    set {
      if (value <= 0) {
        GameManager.Instance.CurrentPlayerHealth = 0; // 确保不会变成负数
        IsPermanentlyDead = true;
        EmitSignal(SignalName.DiedPermanently);
      } else {
        GameManager.Instance.CurrentPlayerHealth = Mathf.Min(value, Stats.MaxHealth);
      }
    }
  }

  [Export]
  public float SpriteBasePositionX { get; set; }

  [Export]
  public PackedScene BulletScene { get; set; }
  [Export]
  public PackedScene DecoyTargetScene { get; set; }
  [Export]
  public PackedScene GrazeTimeShard { get; set; }
  [Export]
  public float AutoRewindOnHitDuration { get; set; } = 3.0f;  // 被击中时自动回溯的时长

  [Export]
  public float Gravity { get; set; } = 12.8f;

  [ExportGroup("Camera")]
  [Export]
  private Vector3 _normalCameraPosition = new Vector3(0, 2.5f, 3.0f);
  private Vector3 _normalCameraRotationDegrees = new Vector3(-45f, 0, 0);
  [Export]
  private Vector3 _slowCameraPosition = new Vector3(0, 2f, 1.5f);
  private Vector3 _slowCameraRotationDegrees = new Vector3(-60f, 0, 0);
  [Export(PropertyHint.Range, "0.1, 20.0, 0.1")]
  private float _cameraSmoothingSpeed = 10.0f;

  [ExportGroup("Death Effects")]
  [Export]
  public PackedScene DeathRingEffectScene { get; set; }

  public float ShootTimer { get; set; } = 0.0f;
  public int CurrentAmmo { get; private set; }
  public bool IsReloading { get; private set; } = false;
  public float TimeToReloaded { get; private set; } // 当前还有多长时间换完子弹
  public Vector3 SpawnPosition { get; set; }
  public bool IsPermanentlyDead = false;

  public ulong InstanceId => GetInstanceId();

  public override void _Ready() {
    _grazeArea = GetNode<Area3D>("GrazeArea");
    _grazeAreaShape = _grazeArea.GetNode<CollisionShape3D>("CollisionShape3D");
    _sprite = GetNode<AnimatedSprite3D>("AnimatedSprite3D");
    _hitPointSprite = GetNode<AnimatedSprite3D>("HitPointSprite");
    _hitPointShape = GetNode<CollisionShape3D>("HitArea/CollisionShape3D");
    _landingIndicator = GetNode<Node3D>("LandingIndicator");
    _interactionArea = GetNode<Area3D>("InteractionArea");
    _interactionArea.AreaEntered += OnInteractionAreaEntered;
    _interactionArea.AreaExited += OnInteractionAreaExited;
    _camera = GetNode<Camera3D>("Camera3D");

    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.Print($"Player: MapGenerator not found at 'GameRoot/MapGenerator'. TimeShards may not spawn correctly.");
    }

    var gm = GameManager.Instance;

    _beginningHealth = Health;
    _beginningTimeBond = gm.TimeBond;
    _beginningHyperGauge = gm.HyperGauge;

    _sprite.Play();
    _hitPointSprite.Play();

    UpdateVisualizer();

    ResetState();

    RewindManager.Instance.Register(this);
  }

  public override void _Process(double delta) {
    if (IsPermanentlyDead) return;

    _timeSlowPressed = Input.IsActionPressed("time_slow");

    UpdateCameraTransform((float) delta);

    if (RewindManager.Instance.IsPreviewing) {
      HandleCurioInput(false);
      UpdateVisualizer();
      return;
    }
    if (RewindManager.Instance.IsRewinding) return;

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    // 每帧更新动态属性
    Stats.RecalculateStats(GameManager.Instance.GetCurrentAndPendingUpgrades(), Health);
    (_grazeAreaShape.Shape as SphereShape3D).Radius = Stats.GrazeRadius;

    UpdateInteractionTarget();
    HandleInteractionInput();
    HandleCurioInput(true);
    HandleHyperInput();
    UpdateCurios(scaledDelta);
    UpdateHyperState(scaledDelta);


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
    HandleMovement(scaledDelta);

    if (Input.IsActionPressed("shoot")) {
      if (!IsReloading && CurrentAmmo <= 0) {
        StartReload(); // 没子弹时自动换弹
      }
      if (!IsReloading && ShootTimer <= 0.0f) {
        Shoot();
      }
    }

    ShootTimer -= (float) delta;

    if (IsInstanceValid(DecoyTarget)) {
      _decoyRemoveTimer -= scaledDelta;
      if (_decoyRemoveTimer <= 0) {
        RemoveDecoyTarget();
      }
    }

    UpdateVisualizer();
  }

  private void UpdateCurios(float scaledDelta) {
    var gm = GameManager.Instance;
    if (gm == null) return;

    foreach (var curio in gm.GetCurrentAndPendingCurios()) {
      // 更新冷却
      if (curio.CurrentCooldown > 0) {
        curio.CurrentCooldown -= scaledDelta;
        if (curio.CurrentCooldown <= 0 && curio == gm.GetCurrentActiveCurio()) {
          SoundManager.Instance.Play(SoundEffect.PlayerSkillAvailable);
        }
      }
      // 更新被动效果
      if (curio.HasPassiveEffect) {
        curio.OnUpdate(this, scaledDelta);
      }
    }

    // 更新按住效果
    if (_curioUsePressed) {
      gm.GetCurrentActiveCurio()?.OnUseHeld(this, scaledDelta);
    }
  }

  private void HandleCurioInput(bool allowUse) {
    var gm = GameManager.Instance;
    if (gm == null) return;

    var currentCurio = gm.GetCurrentActiveCurio();

    if (Input.IsActionJustPressed("curio_switch")) {
      if (_curioUsePressed) {
        currentCurio?.OnUseCancelled(this);
        _curioUsePressed = false;
      }
      if (gm.SwitchToNextActiveCurio()) {
        SoundManager.Instance.Play(SoundEffect.CurioSwitch);
      }
    }

    if (allowUse && Input.IsActionJustPressed("curio_use")) {
      _curioUsePressed = true;
      currentCurio?.OnUsePressed(this);
    }

    if (Input.IsActionJustReleased("curio_use") && _curioUsePressed) {
      if (allowUse || (currentCurio != null && currentCurio.Type == CurioType.TAxisEnhancement)) {
        currentCurio?.OnUseReleased(this);
        _curioUsePressed = false;
      }
    }
  }

  private void HandleHyperInput() {
    const float HYPER_JUMP_SPEED = 3.2f;

    if (Input.IsActionJustPressed("hyper")) {
      var gm = GameManager.Instance;
      if (IsHyperActive) {
        // Hyper 期间再次按下：跳跃
        if (GlobalPosition.Y <= 0) {
          Velocity += new Vector3(0, HYPER_JUMP_SPEED, 0);
        } else {
          SoundManager.Instance.Play(SoundEffect.CurioWrong);
        }
      } else {
        // 尝试激活 Hyper
        if (gm.HyperGauge >= 1.0f) {
          IsHyperActive = true;
          _hyperTimer = Stats.HyperDuration;
          _hyperInvincibilityTimer = 0.5f;
          SoundManager.Instance.Play(SoundEffect.CurioUse);
          if (GlobalPosition.Y <= 0) {
            Velocity += new Vector3(0, HYPER_JUMP_SPEED, 0);
          }
        } else {
          SoundManager.Instance.Play(SoundEffect.CurioWrong);
        }
      }
    }
  }

  private void UpdateHyperState(float scaledDelta) {
    var gm = GameManager.Instance;

    if (_hyperInvincibilityTimer > 0) {
      _hyperInvincibilityTimer -= scaledDelta;
    }

    if (IsHyperActive) {
      _hyperTimer -= scaledDelta;
      if (_hyperTimer <= 0) {
        IsHyperActive = false;
        gm.HyperGauge = 0f;
      } else {
        // 根据剩余时间更新 Hyper 条
        gm.HyperGauge = _hyperTimer / Stats.HyperDuration;
      }
    }
  }

  private void UpdateCameraTransform(float delta) {
    if (_camera == null) return;

    Vector3 targetPosition = _timeSlowPressed ? _slowCameraPosition : _normalCameraPosition;
    Vector3 targetRotationDegrees = _timeSlowPressed ? _slowCameraRotationDegrees : _normalCameraRotationDegrees;
    _camera.Position = _camera.Position.Lerp(targetPosition, _cameraSmoothingSpeed * delta);
    _camera.RotationDegrees = _camera.RotationDegrees.Lerp(targetRotationDegrees, _cameraSmoothingSpeed * delta);
  }

  private void UpdateVisualizer() {
    _hitPointSprite.Visible = _timeSlowPressed;
    _landingIndicator.Visible = GlobalPosition.Y > 0;
    _landingIndicator.GlobalPosition = GlobalPosition with { Y = 0 };

    _sprite.FlipH = _flipSprite;
    _sprite.Position = _sprite.Position with { X = SpriteBasePositionX * (_flipSprite ? -1 : 1) };

    if (IsGoldenBody)
      _sprite.Modulate = Colors.Yellow;
    else
      _sprite.Modulate = Colors.White;
  }

  private void StartReload() {
    if (IsReloading) return;
    IsReloading = true;
    TimeToReloaded = Stats.ReloadTime;
  }

  private void FinishReload() {
    SoundManager.Instance.Play(SoundEffect.PlayerReloadComplete);
    IsReloading = false;
    CurrentAmmo = Stats.MaxAmmoInt;
    TimeToReloaded = 0.0f;
  }

  private void OnGrazeAreaBodyEntered(Node3D body) {
    if (body is BaseBullet bullet) {
      if (bullet.IsPlayerBullet) return;
      bullet.OnGrazeEnter();
      if (bullet.WasGrazed) return;
      bullet.WasGrazed = true;

      SoundManager.Instance.Play(SoundEffect.Graze);
      var gm = GameManager.Instance;

      if (IsHyperActive) {
        // Hyper 期间：续时
        _hyperTimer = Mathf.Min(Stats.HyperDuration, _hyperTimer + Stats.HyperGrazeExtension);
      } else {
        // 非 Hyper 期间：填充 Hyper 条
        gm.HyperGauge = Mathf.Min(1.0f, gm.HyperGauge + Stats.HyperGrazeFillAmount);
      }


      var shard = GrazeTimeShard.Instantiate<TimeShard>();
      shard.TimeBonus = Stats.GrazeTimeBonus;
      shard.StartPosition = bullet.GlobalPosition;
      shard.MapGeneratorRef = _mapGenerator;
      GameRootProvider.CurrentGameRoot.CallDeferred(Node.MethodName.AddChild, shard);
    }
  }

  private void OnGrazeAreaBodyExited(Node3D body) {
    if (body is BaseBullet bullet) {
      if (bullet.IsPlayerBullet) return;
      bullet.OnGrazeExit();
    }
  }

  private void OnHitAreaBodyEntered(Node3D body) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding || IsGoldenBody || _hyperInvincibilityTimer > 0) return;

    if (body.IsInGroup("enemies")) {
      GD.Print("Player hit by enemy");
      Die();
      return;
    }
    if (body is BaseBullet bullet) {
      if (bullet.IsPlayerBullet) return;
      GD.Print("Player hit by bullet");
      Die();
      return;
    }
  }

  private void HandleInteractionInput() {
    if (Input.IsActionJustPressed("ui_accept") && _closestInteractable != null) {
      _closestInteractable.Interact();
    }
  }

  // 更新交互目标
  private void UpdateInteractionTarget() {
    IInteractable newClosest = null;
    float minDistanceSq = float.MaxValue;

    // 遍历所有在范围内的可交互对象
    foreach (var interactable in _nearbyInteractables) {
      if (interactable is not Node3D node) continue;

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
  private void OnInteractionAreaEntered(Area3D area) {
    if (area is IInteractable interactable) {
      if (!_nearbyInteractables.Contains(interactable)) {
        _nearbyInteractables.Add(interactable);
      }
    }
  }

  // 处理离开交互范围的信号
  private void OnInteractionAreaExited(Area3D area) {
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

    var tree = GetTree();
    if (tree == null) return;

    var enemies = tree.GetNodesInGroup("enemies");
    foreach (BaseEnemy enemy in enemies) {
      if (enemy.IsDestroyed) {
        continue;
      }
      if (enemy.IsDestroyed) {
        continue;
      }
      var collider = enemy.GetNodeOrNull<CollisionShape3D>("CollisionShape3D");
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

    --CurrentAmmo;
    ShootTimer = Stats.ShootCooldown;

    SoundManager.Instance.Play(SoundEffect.PlayerShoot);

    var bullet = BulletScene.Instantiate<SimpleBullet>();
    bullet.IsPlayerBullet = true;
    bullet.Damage = Stats.BulletDamage;

    var randomRotationSigma = _timeSlowPressed ? Stats.BulletSpreadSlow : Stats.BulletSpreadNormal;
    var randomRotation = _rnd.Randfn(0, randomRotationSigma);

    Vector3 direction;
    if (_currentTarget != null) {
      direction = (_currentTarget.GlobalPosition - GlobalPosition).Normalized().Rotated(Vector3.Up, randomRotation);
    } else {
      direction = Vector3.Right.Rotated(Vector3.Up, randomRotation);
    }

    var startPos = GlobalPosition;
    bullet.UpdateFunc = (time) => {
      SimpleBullet.UpdateState state = new();
      state.position = startPos + direction * (time * 10f);
      Vector3 upVector = Vector3.Up;
      if (direction.Cross(upVector).IsZeroApprox()) {
        upVector = Vector3.Forward;
      }
      state.rotation = Basis.LookingAt(direction, upVector).GetEuler();
      state.modulate = new Color(1, 1, 1, 0.5f);
      return state;
    };

    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  private void HandleMovement(float scaledDelta) {
    if (IsGoldenBody) {
      Velocity = Vector3.Zero;
      MoveAndSlide();
      return;
    }
    Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
    var velocity2D = direction * Stats.MovementSpeed * (_timeSlowPressed ? Stats.SlowMovementSpeedScale : 1f);

    Velocity = new Vector3(velocity2D.X, Velocity.Y, velocity2D.Y);
    MoveAndSlide();
    if (GlobalPosition.Y < 0) {
      GlobalPosition = GlobalPosition with { Y = 0 };
      Velocity = Velocity with { Y = 0 };
    } else {
      Velocity = Velocity with { Y = Velocity.Y - Gravity * scaledDelta };
    }

    if (direction.X < 0) {
      _flipSprite = true;
    } else if (direction.X > 0) {
      _flipSprite = false;
    }
  }

  public void ResetState() {
    var gm = GameManager.Instance;

    GlobalPosition = SpawnPosition;
    Velocity = Vector3.Zero;
    Health = _beginningHealth;
    gm.TimeBond = _beginningTimeBond;
    Stats.RecalculateStats(gm.GetCurrentAndPendingUpgrades(), Health);
    (_grazeAreaShape.Shape as SphereShape3D).Radius = Stats.GrazeRadius;
    IsReloading = false;
    TimeToReloaded = 0.0f;
    ShootTimer = 0.0f;
    CurrentAmmo = Stats.MaxAmmoInt;
    IsPermanentlyDead = false;
    IsGoldenBody = false;
    gm.HyperGauge = _beginningHyperGauge;
    IsHyperActive = false;
    _hyperTimer = 0f;
    _hyperInvincibilityTimer = 0f;
    foreach (var curio in gm.GetCurrentAndPendingCurios()) {
      curio.CurrentCooldown = 0f;
    }
    RemoveDecoyTarget();
    _hitPointShape.Disabled = false;
    UpdateVisualizer();
  }

  public void Die() {
    if (IsPermanentlyDead) return;

    SoundManager.Instance.Play(SoundEffect.PlayerDeath);

    if (DeathRingEffectScene != null) {
      var effect = DeathRingEffectScene.Instantiate<InvertRingEffect>();
      GetTree().Root.AddChild(effect);
      effect.StartEffect(this.GlobalPosition);
    }

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

  public void CreateDecoyTarget(float duration) {
    if (DecoyTargetScene == null || IsInstanceValid(DecoyTarget)) {
      return;
    }

    DecoyTarget = DecoyTargetScene.Instantiate<Node3D>();
    DecoyTarget.GlobalPosition = this.GlobalPosition;
    GameRootProvider.CurrentGameRoot.AddChild(DecoyTarget);

    // 创建一个计时器来移除诱饵
    _decoyRemoveTimer = duration;
  }

  public void RemoveDecoyTarget() {
    if (IsInstanceValid(DecoyTarget)) {
      DecoyTarget.QueueFree();
    }
    DecoyTarget = null;
    _decoyRemoveTimer = 0;
  }

  public RewindState CaptureState() {
    Dictionary<CurioType, float> curioCooldowns = new();
    foreach (var curio in GameManager.Instance.GetCurrentAndPendingCurios()) {
      curioCooldowns[curio.Type] = curio.CurrentCooldown;
    }

    return new PlayerState {
      GlobalPosition = this.GlobalPosition,
      Velocity = this.Velocity,
      FlipSprite = this._flipSprite,
      CurrentAmmo = this.CurrentAmmo,
      IsReloading = this.IsReloading,
      TimeToReloaded = this.TimeToReloaded,
      ShootTimer = this.ShootTimer,
      IsInvincible = this.IsGoldenBody,
      CurioCooldowns = curioCooldowns,
      DecoyActive = IsInstanceValid(DecoyTarget),
      DecoyPosition = IsInstanceValid(DecoyTarget) ? DecoyTarget.GlobalPosition : Vector3.Zero,
      DecoyRemoveTimer = _decoyRemoveTimer,
      HyperGauge = GameManager.Instance.HyperGauge,
      IsHyperActive = this.IsHyperActive,
      HyperTimer = this._hyperTimer,
      HyperInvincibilityTimer = this._hyperInvincibilityTimer,
      // Health 和 TimeBond 仅用于阶段重启，回溯系统不会使用它们
      Health = this.Health,
      TimeBond = GameManager.Instance.TimeBond
    };
  }

  public void RestoreState(RewindState state) {
    if (state is not PlayerState ps) return;

    this.GlobalPosition = ps.GlobalPosition;
    this.Velocity = ps.Velocity;
    this._flipSprite = ps.FlipSprite;
    this.CurrentAmmo = ps.CurrentAmmo;
    this.IsReloading = ps.IsReloading;
    this.TimeToReloaded = ps.TimeToReloaded;
    this.ShootTimer = ps.ShootTimer;
    this.IsGoldenBody = ps.IsInvincible;
    foreach (var curio in GameManager.Instance.GetCurrentAndPendingCurios()) {
      if (ps.CurioCooldowns.TryGetValue(curio.Type, out var cd)) {
        curio.CurrentCooldown = cd;
      }
    }

    // --- Hyper 状态恢复 ---
    GameManager.Instance.HyperGauge = ps.HyperGauge;
    this.IsHyperActive = ps.IsHyperActive;
    this._hyperTimer = ps.HyperTimer;
    this._hyperInvincibilityTimer = ps.HyperInvincibilityTimer;

    // --- 诱饵状态恢复 ---
    _decoyRemoveTimer = ps.DecoyRemoveTimer;

    bool shouldBeActive = ps.DecoyActive;
    bool isActive = IsInstanceValid(DecoyTarget);

    if (shouldBeActive && !isActive) {
      // 诱饵需要被创建
      if (DecoyTargetScene != null) {
        DecoyTarget = DecoyTargetScene.Instantiate<Node3D>();
        GameRootProvider.CurrentGameRoot.AddChild(DecoyTarget);
      }
    } else if (!shouldBeActive && isActive) {
      // 诱饵需要被移除
      RemoveDecoyTarget();
    }

    // 如果诱饵应该激活（并且现在是），更新其状态
    if (shouldBeActive && IsInstanceValid(DecoyTarget)) {
      DecoyTarget.GlobalPosition = ps.DecoyPosition;
    }

    // 注意：回溯系统不应该恢复 Health 和 TimeBond，
    // 但「从当前阶段重来」功能会手动恢复它们．
  }

  // Player 不会被 Destroy 或 Resurrect，所以提供空实现
  public void Destroy() { }
  public void Resurrect() { }

  public override void _ExitTree() {
    RemoveDecoyTarget();
    if (RewindManager.Instance != null) {
      RewindManager.Instance.Unregister(this);
    }
    base._ExitTree();
  }
}
