using Bullet;
using Godot;

namespace Weapon;

public partial class Violin : Weapon {
  [Export] public PackedScene BulletScene { get; set; }

  public override float BaseShootCooldown { get; } = 0.05f;
  public override float BaseDamage { get; } = 0.5f;
  public override float BaseMaxAmmo { get; } = 20f;
  public override float BaseReloadTime { get; } = 3.0f;
  public override float BaseSpreadNormal { get; } = Mathf.Pi / 24;
  public override float BaseSpreadSlow { get; } = Mathf.Pi / 60;

  protected override void HandleInput(float scaledDelta) {
    bool isSlow = Input.IsActionPressed("time_slow");
    bool tryShoot = Input.IsActionPressed("shoot");
    bool tryReload = Input.IsActionJustPressed("weapon_reload");

    if (tryReload && CurrentAmmo < MaxAmmoCalculated && !IsReloading) {
      StartReload();
      return;
    }

    if (tryShoot) {
      if (!IsReloading && CurrentAmmo <= 0) {
        StartReload();
      } else if (!IsReloading && ShootTimer <= 0) {
        Fire(isSlow);
      }
    }
  }

  private void Fire(bool isSlow) {
    --CurrentAmmo;

    // 应用玩家射速加成
    float cooldown = BaseShootCooldown / (1.0f + _player.Stats.FireRate);
    ShootTimer = cooldown;

    SoundManager.Instance.Play(SoundEffect.PlayerShoot);

    // 计算散布
    float baseSpread = isSlow ? BaseSpreadSlow : BaseSpreadNormal;
    float accuracyBonus = isSlow ? _player.Stats.BulletAccuracySlow : _player.Stats.BulletAccuracyNormal;
    float finalSpread = baseSpread / (1.0f + accuracyBonus);

    Vector3 dir = GetShootingDirection(finalSpread);

    // 计算伤害
    float damage = BaseDamage * (1.0f + _player.Stats.BulletDamageMultiplier);

    var bullet = BulletScene.Instantiate<SimpleBullet>();
    bullet.IsPlayerBullet = true;
    bullet.Damage = damage;

    Vector3 startPos = GlobalPosition;
    bullet.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();
      s.position = startPos + dir * (t * 10f);
      s.rotation = new Vector3(0, Mathf.Atan2(-dir.X, -dir.Z), 0);
      s.modulate = new Color(1, 1, 1, 0.5f);
      return s;
    };

    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }
}
