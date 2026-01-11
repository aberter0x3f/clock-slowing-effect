using Bullet;
using Godot;
using Rewind;

namespace Weapon;

public class GuitarState : WeaponState {
  public Guitar.GuitarMode Mode;
  public float AimProgress;
  public float BurstTimer;
  public int BurstShotsRemaining;
  public float AimDirectionAngle;
  public float CachedSpreadHalfAngle; // 记录爆发时的散布参数
}

public partial class Guitar : Weapon {
  public enum GuitarMode { Idle, HighSpeedFire, Aiming, Bursting }

  [Export] public PackedScene BulletScene { get; set; }

  public override float BaseShootCooldown { get; } = 1f;
  public override float BaseDamage { get; } = 3f;
  public override float BaseMaxAmmo { get; } = 5f;
  public override float BaseReloadTime { get; } = 3f;
  public override float BaseSpreadNormal { get; } = Mathf.Pi / 12;
  public override float BaseSpreadSlow { get; } = Mathf.Pi / 6;

  private GuitarMode _mode = GuitarMode.Idle;
  private float _aimProgress = 0f; // 0 = 初始散布, 1 = 最小散布
  private float _aimDirectionAngle; // 锁定时的瞄准方向
  private float _burstTimer = 0f;
  private int _burstShotsRemaining = 0;
  private float _cachedSpreadHalfAngle; // 缓存的本次爆发参数 (弧度)
  private AimingCone _aimingCone;

  public override void Initialize(Player player) {
    base.Initialize(player);
    _aimingCone = GetNode<AimingCone>("AimingCone");
    _aimingCone.Visible = false;
  }

  public override void ResetState() {
    base.ResetState();
    _mode = GuitarMode.Idle;
    _aimProgress = 0f;
    _burstTimer = 0f;
    _burstShotsRemaining = 0;
    _aimingCone.Visible = false;
  }

  protected override void UpdateOrbit(float delta) {
    if (_mode == GuitarMode.Idle) {
      base.UpdateOrbit(delta);
    }
  }

  protected override void HandleInput(float scaledDelta) {
    bool isSlow = Input.IsActionPressed("time_slow");
    bool shootPressed = Input.IsActionPressed("shoot");
    bool shootJustReleased = Input.IsActionJustReleased("shoot");
    bool reloadPressed = Input.IsActionJustPressed("weapon_reload");

    // 计算实际冷却时间（缩圈时间）
    float actualCooldown = BaseShootCooldown / (1.0f + _player.Stats.FireRate);

    switch (_mode) {
      case GuitarMode.Idle:
        if (IsReloading) break;

        // 手动换弹
        if (reloadPressed && CurrentAmmo < MaxAmmoCalculated) {
          StartReload();
          return;
        }

        // 没子弹尝试射击 -> 自动换弹
        if (CurrentAmmo <= 0 && shootPressed) {
          StartReload();
          return;
        }

        if (shootPressed && CurrentAmmo > 0) {
          if (isSlow) {
            EnterAiming();
          } else if (ShootTimer <= 0) {
            EnterHighSpeedFire();
          }
        }
        break;

      case GuitarMode.HighSpeedFire:
        if (IsReloading || CurrentAmmo <= 0) {
          _mode = GuitarMode.Idle;
          return;
        }

        if (!shootPressed) {
          _mode = GuitarMode.Idle;
        } else if (isSlow) {
          // 在高速射击时按下低速键，转入瞄准
          EnterAiming();
        } else {
          if (ShootTimer <= 0) {
            FireNormal();
          }
        }
        break;

      case GuitarMode.Aiming:
        // 任何时候松开低速键，打断瞄准回到 Idle
        if (!isSlow) {
          CancelAiming();
          _mode = GuitarMode.Idle;
          return;
        }

        // 持续更新朝向
        UpdateAimDirection();

        // 更新瞄准进度
        // 如果玩家正在移动，则暂停缩圈并显示红色提示；否则显示蓝色并继续缩圈
        bool isMoving = _player.Velocity.LengthSquared() > 0.01f;
        if (isMoving) {
          _aimingCone.Color = new Color(1f, 0.2f, 0.2f, 0.2f);
        } else {
          _aimingCone.Color = new Color(0.2f, 0.6f, 1f, 0.2f);
          _aimProgress += scaledDelta / actualCooldown;
          _aimProgress = Mathf.Clamp(_aimProgress, 0f, 1f);
        }

        // 更新扇形可视化
        UpdateAimingVisuals();

        // 触发爆发：松开射击键 或 瞄准进度已满 (缩圈完成)
        if (shootJustReleased || _aimProgress >= 1.0f) {
          StartBurst(actualCooldown);
        }
        break;

      case GuitarMode.Bursting:
        // 爆发模式下忽略输入，只处理自动射击逻辑
        _burstTimer -= scaledDelta;
        if (_burstTimer <= 0) {
          FireBurstShot(actualCooldown);
        }
        break;
    }
  }

  private void EnterHighSpeedFire() {
    _mode = GuitarMode.HighSpeedFire;
    FireNormal();
  }

  private void FireNormal() {
    if (CurrentAmmo <= 0) {
      StartReload();
      _mode = GuitarMode.Idle;
      return;
    }

    --CurrentAmmo;
    float cooldown = BaseShootCooldown / (1.0f + _player.Stats.FireRate);
    ShootTimer = cooldown;
    SoundManager.Instance.Play(SoundEffect.PlayerShoot);

    // 高速模式：正态分布 (Gaussian/Normal distribution)
    float baseSpread = BaseSpreadNormal;
    float accBonus = _player.Stats.BulletAccuracyNormal;
    float finalSpreadSigma = baseSpread / (1.0f + accBonus);

    Vector3 dir = GetShootingDirection(finalSpreadSigma);
    SpawnBullet(dir);
  }

  private void EnterAiming() {
    _mode = GuitarMode.Aiming;
    _aimProgress = 0f;
    _aimingCone.Visible = true;
    // 初始半径
    _aimingCone.Radius = 10.0f;
    UpdateAimDirection();
    UpdateAimingVisuals();
  }

  private void UpdateAimDirection() {
    if (IsInstanceValid(_currentTarget)) {
      Vector3 toTarget = (_currentTarget.GlobalPosition - GlobalPosition).Normalized();
      _aimDirectionAngle = Mathf.Atan2(-toTarget.X, -toTarget.Z); // Godot -Z Forward
    } else {
      Vector3 dir = GlobalTransform.Basis.X;
      _aimDirectionAngle = Mathf.Atan2(-dir.X, -dir.Z);
    }
  }

  private void UpdateAimingVisuals() {
    // 计算当前的半角
    // 初始 SpreadSlow，最终 SpreadSlow / 20
    float startSpread = BaseSpreadSlow / (1.0f + _player.Stats.BulletAccuracySlow);
    float endSpread = startSpread / 20.0f;
    float currentSpreadRad = Mathf.Lerp(startSpread, endSpread, _aimProgress);

    // 扇形显示的是全角，所以 * 2，转为角度
    _aimingCone.AngleDeg = Mathf.RadToDeg(currentSpreadRad * 2);

    // 更新圆锥的旋转，使其指向目标
    _aimingCone.GlobalRotation = new Vector3(0, _aimDirectionAngle, 0);
    _aimingCone.GlobalPosition = GlobalPosition;
  }

  private void CancelAiming() {
    _aimingCone.Visible = false;
  }

  private void StartBurst(float actualCooldown) {
    _mode = GuitarMode.Bursting;
    _aimingCone.Visible = false;

    // 缓存当前的散布角度，爆发期间保持不变
    float startSpread = BaseSpreadSlow / (1.0f + _player.Stats.BulletAccuracySlow);
    float endSpread = startSpread / 20.0f;
    _cachedSpreadHalfAngle = Mathf.Lerp(startSpread, endSpread, _aimProgress);

    // 将剩余所有子弹全部打出
    _burstShotsRemaining = CurrentAmmo;
    // 立即发射第一发
    _burstTimer = 0f;
  }

  private void FireBurstShot(float actualCooldown) {
    if (_burstShotsRemaining <= 0) {
      // 爆发结束
      CurrentAmmo = 0;
      StartReload();
      _mode = GuitarMode.Idle;
      return;
    }

    --_burstShotsRemaining;
    CurrentAmmo = _burstShotsRemaining;

    SoundManager.Instance.Play(SoundEffect.PlayerShoot);

    // 均匀随机分布 (Uniform distribution)
    // 范围 [-cachedSpread, +cachedSpread]
    float randomAngleOffset = (float) _rnd.RandfRange(-_cachedSpreadHalfAngle, _cachedSpreadHalfAngle);

    // 基于锁定的瞄准方向 + 随机偏移
    Vector3 forward = new Vector3(0, 0, -1);
    Vector3 aimedDir = forward.Rotated(Vector3.Up, _aimDirectionAngle);
    Vector3 finalDir = aimedDir.Rotated(Vector3.Up, randomAngleOffset);

    SpawnBullet(finalDir);

    // 爆发射击间隔极短 (ShootCooldown / 10)
    _burstTimer = actualCooldown / 10.0f;
  }

  private void SpawnBullet(Vector3 dir) {
    float damage = BaseDamage * (1.0f + _player.Stats.BulletDamageMultiplier);

    var bullet = BulletScene.Instantiate<SimpleBullet>();
    bullet.IsPlayerBullet = true;
    bullet.Damage = damage;
    Vector3 startPos = GlobalPosition;

    bullet.UpdateFunc = (t) => new SimpleBullet.UpdateState {
      position = startPos + dir * (t * 20f), // Guitar 子弹速度较快
      rotation = new Vector3(0, Mathf.Atan2(-dir.X, -dir.Z), 0),
    };
    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  public override RewindState CaptureState() {
    var baseState = (WeaponState) base.CaptureState();
    return new GuitarState {
      CurrentAmmo = baseState.CurrentAmmo,
      IsReloading = baseState.IsReloading,
      TimeToReloaded = baseState.TimeToReloaded,
      ShootTimer = baseState.ShootTimer,
      OrbitAngle = baseState.OrbitAngle,
      Mode = _mode,
      AimProgress = _aimProgress,
      BurstTimer = _burstTimer,
      BurstShotsRemaining = _burstShotsRemaining,
      AimDirectionAngle = _aimDirectionAngle,
      CachedSpreadHalfAngle = _cachedSpreadHalfAngle
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not GuitarState gs) return;
    _mode = gs.Mode;
    _aimProgress = gs.AimProgress;
    _burstTimer = gs.BurstTimer;
    _burstShotsRemaining = gs.BurstShotsRemaining;
    _aimDirectionAngle = gs.AimDirectionAngle;
    _cachedSpreadHalfAngle = gs.CachedSpreadHalfAngle;

    _aimingCone.Visible = (_mode == GuitarMode.Aiming);
    if (_mode == GuitarMode.Aiming) {
      UpdateAimingVisuals();
    }
  }
}
