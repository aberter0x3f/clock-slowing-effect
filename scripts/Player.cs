using Godot;

public partial class Player : CharacterBody2D {
  public enum AnimationState {
    Idle,
    Walk,
  }

  private AnimationState _currentState = AnimationState.Idle;
  private AnimationState _lastState = AnimationState.Idle;
  private double _health = 60.0;
  private Area2D _aimingArea;
  private AnimatedSprite2D _sprite;
  private Node2D _currentTarget = null;
  private Area2D _grazeArea; // 擦弹区域的引用
  private AnimatedSprite2D _hitPointSprite;
  private bool _timeSlowPressed = false;
  private RandomNumberGenerator _rnd = new RandomNumberGenerator();

  [Export]
  public float Speed { get; set; } = 450.0f;

  [Export]
  public float SlowSpeedScale { get; set; } = 0.45f;

  [Export]
  public double MaxHealth { get; set; } = 60.0;

  [Export]
  public double Health {
    get => _health;
    set {
      if (value <= 0) {
        _health = 0; // 确保不会变成负数
        GameOver();
      } else {
        _health = double.Min(value, MaxHealth);
      }
    }
  }

  [Export]
  public PackedScene Bullet { get; set; }

  [Export]
  public float ShootCooldown { get; set; } = 0.05f;

  public float ShootTimer { get; set; } = 0.0f;

  [Export]
  public float BulletSpreadNormal { get; set; } = float.Pi / 24.0f;

  [Export]
  public float BulletSpreadSlow { get; set; } = float.Pi / 48.0f;

  [Export]
  public double GrazeTimeBonus { get; set; } = 0.2;

  [Export]
  public int MaxAmmo { get; set; } = 20;

  public int CurrentAmmo { get; set; }

  // 单发子弹伤害
  [Export]
  public float BulletDamage { get; set; } = 0.5f;

  public bool IsReloading { get; private set; } = false;

  // 换弹时间
  [Export]
  public float ReloadTime { get; set; } = 3.0f;

  // 当前还有多长时间换完子弹
  public float ReloadTimer { get; set; }

  public override void _Ready() {
    _aimingArea = GetNode<Area2D>("AimingArea");
    _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
    _grazeArea = GetNode<Area2D>("GrazeArea");
    _hitPointSprite = GetNode<AnimatedSprite2D>("HitPointSprite");

    CurrentAmmo = MaxAmmo; // 初始化弹药

    _sprite.Play();
    _hitPointSprite.Play();
  }

  public override void _Process(double delta) {
    _currentState = AnimationState.Idle;

    if (IsReloading) {
      ReloadTimer -= (float) delta; // 换弹时间不受时间缩放影响
      if (ReloadTimer <= 0.0f) {
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
  }

  private void StartReload() {
    if (IsReloading) return;
    IsReloading = true;
    ReloadTimer = ReloadTime;
    GD.Print("Reloading...");
  }

  private void FinishReload() {
    IsReloading = false;
    CurrentAmmo = MaxAmmo;
    ReloadTimer = 0.0f;
    GD.Print("Reload finished!");
  }

  private void OnGrazeAreaBodyEntered(Node2D body) {
    if (body is Bullet bullet) {
      if (bullet.IsPlayerBullet) return;
      bullet.OnGrazeEnter();
      if (bullet.WasGrazed) return;
      bullet.WasGrazed = true;
      Health += GrazeTimeBonus;
      GD.Print($"Graze successful! Gained {GrazeTimeBonus}s. New TimeHP: {Health}");
    }
  }

  private void OnGrazeAreaBodyExited(Node2D body) {
    if (body is Bullet bullet) {
      if (bullet.IsPlayerBullet) return;
      bullet.OnGrazeExit();
    }
  }

  private void OnHitAreaBodyEntered(Node2D body) {
    if (body is Bullet bullet) {
      if (bullet.IsPlayerBullet) return;
      GameOver();
    }
  }

  private void FindAndAimTarget() {
    _currentTarget = null;
    float closestDistance = float.MaxValue;
    var bodies = _aimingArea.GetOverlappingBodies();
    foreach (var body in bodies) {
      if (body.IsInGroup("enemies")) {
        float distance = GlobalPosition.DistanceSquaredTo(body.GlobalPosition);
        if (distance < closestDistance) {
          closestDistance = distance;
          _currentTarget = body;
        }
      }
    }
  }

  private void Shoot() {
    if (CurrentAmmo <= 0) return;

    ShootTimer = ShootCooldown;
    --CurrentAmmo;

    Bullet bullet = Bullet.Instantiate<Bullet>();
    bullet.Damage = BulletDamage;
    bullet.GlobalPosition = GlobalPosition;
    var randomRotationSigma = _timeSlowPressed ? BulletSpreadSlow : BulletSpreadNormal;
    var randomRotation = _rnd.Randfn(0, randomRotationSigma);
    if (_currentTarget != null) {
      bullet.Direction = (_currentTarget.GlobalPosition - GlobalPosition).Normalized().Rotated(randomRotation);
    } else {
      bullet.Direction = Vector2.Right.Rotated(_sprite.GlobalRotation).Rotated(randomRotation);
    }
    bullet.GlobalRotation = bullet.Direction.Angle();
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

  public void TakeDamage(double amount) {
    GD.Print($"Player took {amount} damage, remaining TimeHP: {Health}");
    Health -= amount;
  }

  public void GameOver() {
    GD.Print("Game Over!");
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
