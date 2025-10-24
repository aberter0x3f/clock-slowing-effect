using System;
using Bullet;
using Godot;

public partial class Player : CharacterBody2D {
  public enum AnimationState {
    Idle,
    Walk,
  }

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

  public override void _Ready() {
    _grazeArea = GetNode<Area2D>("GrazeArea");
    _visualizer = GetNode<Node3D>("Visualizer");
    _sprite = _visualizer.GetNode<AnimatedSprite3D>("AnimatedSprite3D");
    _hitPointSprite = _visualizer.GetNode<AnimatedSprite3D>("HitPointSprite");

    CurrentAmmo = MaxAmmo; // 初始化弹药
    _health = MaxHealth;

    _sprite.Play();
    _hitPointSprite.Play();
    UpdateVisualizer();
  }

  public override void _Process(double delta) {
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
    HandleTimeScaleInput();
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
    UpdateState();

    UpdateVisualizer();
  }

  private void UpdateVisualizer() {
    _visualizer.GlobalPosition = new Vector3(GlobalPosition.X * 0.01f, 0.3f, GlobalPosition.Y * 0.01f);
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
      Health += GrazeTimeBonus;
    }
  }

  private void OnGrazeAreaBodyExited(Node2D body) {
    if (body is BaseBullet bullet) {
      if (bullet.IsPlayerBullet) return;
      bullet.OnGrazeExit();
    }
  }

  private void OnHitAreaBodyEntered(Node2D body) {
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
    foreach (Node2D enemy in enemies) {
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

  private void HandleTimeScaleInput() {
    _timeSlowPressed = Input.IsActionPressed("time_slow");
    if (_timeSlowPressed) {
      TimeManager.Instance.TimeScale = 0.2f;
      _hitPointSprite.Visible = true;
    } else {
      TimeManager.Instance.TimeScale = 1f;
      _hitPointSprite.Visible = false;
    }
  }

  private void HandleMovement() {
    Vector2 direction = Input.GetVector("ui_left", "ui_right", "ui_up", "ui_down");
    Velocity = direction * (_timeSlowPressed ? Speed * SlowSpeedScale : Speed);
    if (!direction.IsZeroApprox()) {
      _currentState = AnimationState.Walk;
    }
    MoveAndSlide();
  }

  public void Die() {
    GD.Print("Game Over!");
    // 测试阶段限不死
    GetTree().Quit();
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
}
