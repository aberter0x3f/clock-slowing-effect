using System.Collections.Generic;
using Godot;

public class PlayerStats {
  // 基础属性
  public PlayerBaseStats BaseStats { get; }

  // 计算后的属性
  public float MaxHealth { get; private set; }
  public float MovementSpeed { get; private set; }
  public float SlowMovementSpeedScale { get; private set; }
  public float ShootCooldown =>
    FireRate >= 0 ?
      BaseStats.ShootCooldown / (1.0f + FireRate) :
      BaseStats.ShootCooldown * Mathf.Pow(2, FireRate);
  public float BulletSpreadNormal =>
    BulletAccuracyNormal >= 0 ?
      BaseStats.BulletSpreadNormal / (1.0f + BulletAccuracyNormal) :
      BaseStats.BulletSpreadNormal * Mathf.Pow(2, BulletAccuracyNormal);
  public float BulletSpreadSlow =>
    BulletAccuracySlow >= 0 ?
      BaseStats.BulletSpreadSlow / (1.0f + BulletAccuracySlow) :
      BaseStats.BulletSpreadSlow * Mathf.Pow(2, BulletAccuracySlow);
  public float BulletDamage { get; private set; }
  public float MaxAmmo { get; private set; }
  public int MaxAmmoInt => Mathf.RoundToInt(MaxAmmo);
  public float ReloadTime =>
    ReloadSpeed >= 0 ?
      BaseStats.ReloadTime / (1.0f + ReloadSpeed) :
      BaseStats.ReloadTime * Mathf.Pow(2, ReloadSpeed);
  public float GrazeRadius { get; private set; }
  public float GrazeTimeBonus { get; private set; }
  // 除法类型
  public float BulletAccuracyNormal { get; private set; }
  public float BulletAccuracySlow { get; private set; }
  public float FireRate { get; private set; }
  public float ReloadSpeed { get; private set; }

  // 用于动态计算的加成值
  private float _adrenalineBonus = 0f;

  public PlayerStats(PlayerBaseStats baseStats) {
    BaseStats = baseStats;
    RecalculateStats(new HashSet<Upgrade>(), BaseStats.MaxHealth);
  }

  /// <summary>
  /// 根据当前持有的强化列表，重新计算所有玩家属性．
  /// </summary>
  public void RecalculateStats(HashSet<Upgrade> activeUpgrades, float currentHealth) {
    // 重置为基础值
    MaxHealth = BaseStats.MaxHealth;
    MovementSpeed = BaseStats.MovementSpeed;
    SlowMovementSpeedScale = BaseStats.SlowMovementSpeedScale;
    BulletDamage = BaseStats.BulletDamage;
    MaxAmmo = BaseStats.MaxAmmo;
    GrazeRadius = BaseStats.GrazeRadius;
    GrazeTimeBonus = BaseStats.GrazeTimeBonus;
    BulletAccuracyNormal = 0;
    BulletAccuracySlow = 0;
    FireRate = 0;
    ReloadSpeed = 0;
    _adrenalineBonus = 0f;

    // 累加所有同类型强化效果
    var bonusTotals = new Dictionary<UpgradeType, float>();
    var bonusTotals2 = new Dictionary<UpgradeType, float>(); // 用于 Value2

    foreach (var upgrade in activeUpgrades) {
      bonusTotals.TryAdd(upgrade.Type, 0f);
      bonusTotals[upgrade.Type] += upgrade.Value1;

      if (upgrade.Type == UpgradeType.MovementSpecialist) {
        bonusTotals2.TryAdd(upgrade.Type, 0f);
        bonusTotals2[upgrade.Type] += upgrade.Value2;
      }
    }

    // 应用累加后的加成
    MaxHealth += BaseStats.MaxHealth * bonusTotals.GetValueOrDefault(UpgradeType.MaxHealth, 0f);
    BulletDamage += BaseStats.BulletDamage * bonusTotals.GetValueOrDefault(UpgradeType.BulletDamage, 0f);
    MaxAmmo += BaseStats.MaxAmmo * bonusTotals.GetValueOrDefault(UpgradeType.MaxAmmo, 0f);
    GrazeRadius += BaseStats.GrazeRadius * bonusTotals.GetValueOrDefault(UpgradeType.GrazeRadius, 0f);
    GrazeTimeBonus += BaseStats.GrazeRadius * bonusTotals.GetValueOrDefault(UpgradeType.GrazeBonus, 0f);

    // 特殊计算 (除法)
    BulletAccuracyNormal += bonusTotals.GetValueOrDefault(UpgradeType.BulletAccuracy, 0f);
    BulletAccuracySlow += bonusTotals.GetValueOrDefault(UpgradeType.BulletAccuracy, 0f);
    BulletAccuracyNormal += bonusTotals.GetValueOrDefault(UpgradeType.MovementSpecialist, 0f);
    BulletAccuracySlow = bonusTotals2.GetValueOrDefault(UpgradeType.MovementSpecialist, 0f);
    FireRate += bonusTotals.GetValueOrDefault(UpgradeType.FireRate, 0f);
    ReloadSpeed += bonusTotals.GetValueOrDefault(UpgradeType.ReloadSpeed, 0f);

    // 存储动态加成值
    _adrenalineBonus = bonusTotals.GetValueOrDefault(UpgradeType.Adrenaline, 0f);

    // 计算依赖于当前状态的最终值
    ApplyDynamicBonuses(currentHealth);
  }

  /// <summary>
  /// 应用那些每帧都可能变化的加成．
  /// </summary>
  private void ApplyDynamicBonuses(float currentHealth) {
    // 肾上腺素
    if (_adrenalineBonus > 0f) {
      float missingHealthRatio = Mathf.Clamp(1.0f - (currentHealth / MaxHealth), 0f, 1f);
      float adrenalineDamageMultiplier = _adrenalineBonus * missingHealthRatio * missingHealthRatio;
      BulletDamage += BaseStats.BulletDamage * adrenalineDamageMultiplier;
    }
  }
}
