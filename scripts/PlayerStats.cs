using System.Collections.Generic;
using Godot;

public class PlayerStats {
  // 基础属性
  public PlayerBaseStats BaseStats { get; }

  // 计算后的属性
  public float MaxHealth { get; private set; }
  public float MovementSpeed { get; private set; }
  public float SlowMovementSpeedScale { get; private set; }

  // 武器修饰符 (Modifiers) - 基础值为 0 (0% 加成)
  public float FireRate { get; private set; } // +10% = 0.1
  public float ReloadSpeed { get; private set; }
  public float MaxAmmoMultiplier { get; private set; } // +50% ammo = 0.5
  public float BulletDamageMultiplier { get; private set; }

  // 精度修饰符 (Accuracy Bonus, higher is tighter spread)
  public float BulletAccuracyNormal { get; private set; }
  public float BulletAccuracySlow { get; private set; }

  public float GrazeRadius { get; private set; }
  public float GrazeTimeBonus { get; private set; }
  public float HyperDuration { get; private set; }
  public float HyperGrazeFillAmount { get; private set; }
  public float HyperGrazeExtension => 0.3f;

  private float _adrenalineBonus = 0f;

  public PlayerStats(PlayerBaseStats baseStats) {
    BaseStats = baseStats;
    RecalculateStats(new HashSet<Upgrade>(), BaseStats.MaxHealth);
  }

  public void RecalculateStats(HashSet<Upgrade> activeUpgrades, float currentHealth) {
    // 重置通用属性
    MaxHealth = BaseStats.MaxHealth;
    MovementSpeed = BaseStats.MovementSpeed;
    SlowMovementSpeedScale = BaseStats.SlowMovementSpeedScale;
    GrazeRadius = BaseStats.GrazeRadius;
    GrazeTimeBonus = BaseStats.GrazeTimeBonus;
    HyperDuration = BaseStats.HyperDuration;
    HyperGrazeFillAmount = 1f / BaseStats.GrazeForFullHyper;

    // 重置武器修饰符
    FireRate = 0f;
    ReloadSpeed = 0f;
    MaxAmmoMultiplier = 0f;
    BulletDamageMultiplier = 0f;
    BulletAccuracyNormal = 0f;
    BulletAccuracySlow = 0f;
    _adrenalineBonus = 0f;

    // 累加 Upgrade
    var bonusTotals = new Dictionary<UpgradeType, float>();
    var bonusTotals2 = new Dictionary<UpgradeType, float>();

    foreach (var upgrade in activeUpgrades) {
      bonusTotals.TryAdd(upgrade.Type, 0f);
      bonusTotals[upgrade.Type] += upgrade.Value1;
      bonusTotals2.TryAdd(upgrade.Type, 0f);
      bonusTotals2[upgrade.Type] += upgrade.Value2;
    }

    // 应用通用属性加成
    MaxHealth += BaseStats.MaxHealth * bonusTotals.GetValueOrDefault(UpgradeType.MaxHealth, 0f);
    GrazeRadius += BaseStats.GrazeRadius * bonusTotals.GetValueOrDefault(UpgradeType.GrazeRadius, 0f);
    GrazeTimeBonus += BaseStats.GrazeTimeBonus * bonusTotals.GetValueOrDefault(UpgradeType.GrazeBonus, 0f);
    HyperDuration += BaseStats.HyperDuration * bonusTotals.GetValueOrDefault(UpgradeType.HyperDuration, 0f);
    HyperGrazeFillAmount += 1f / BaseStats.GrazeForFullHyper * bonusTotals.GetValueOrDefault(UpgradeType.HyperEfficiency, 0f);

    // 应用武器修饰符
    BulletDamageMultiplier += bonusTotals.GetValueOrDefault(UpgradeType.BulletDamage, 0f);
    MaxAmmoMultiplier += bonusTotals.GetValueOrDefault(UpgradeType.MaxAmmo, 0f);
    FireRate += bonusTotals.GetValueOrDefault(UpgradeType.FireRate, 0f);
    ReloadSpeed += bonusTotals.GetValueOrDefault(UpgradeType.ReloadSpeed, 0f);

    // 精度逻辑：Value1 通常用于提升 Normal 精度
    BulletAccuracyNormal += bonusTotals.GetValueOrDefault(UpgradeType.BulletAccuracy, 0f);
    BulletAccuracySlow += bonusTotals.GetValueOrDefault(UpgradeType.BulletAccuracy, 0f);

    // 特殊处理 MovementSpecialist
    BulletAccuracyNormal += bonusTotals.GetValueOrDefault(UpgradeType.MovementSpecialist, 0f);
    BulletAccuracySlow += bonusTotals2.GetValueOrDefault(UpgradeType.MovementSpecialist, 0f);

    _adrenalineBonus = bonusTotals.GetValueOrDefault(UpgradeType.Adrenaline, 0f);

    ApplyDynamicBonuses(currentHealth);
  }

  private void ApplyDynamicBonuses(float currentHealth) {
    if (_adrenalineBonus > 0f) {
      float missingHealthRatio = Mathf.Clamp(1.0f - (currentHealth / MaxHealth), 0f, 1f);
      BulletDamageMultiplier += _adrenalineBonus * missingHealthRatio * missingHealthRatio;
    }
  }
}
