using Godot;

// 定义强化的类型，便于管理和扩展
public enum UpgradeType {
  BulletDamage,
  BulletAccuracy,
  MaxHealth,
  MaxAmmo,
  FireRate,
  ReloadSpeed,
  MovementSpecialist,
  GrazeRadius,
  GrazeBonus,
  Adrenaline
}

[GlobalClass]
public partial class Upgrade : Resource {
  [Export]
  public UpgradeType Type { get; set; }

  [Export(PropertyHint.Range, "1,3,1")]
  public int Level { get; set; } = 1;

  [Export]
  public string Name { get; set; } = "Upgrade Name";

  [Export]
  public string ShortName { get; set; } = "Upₙ";

  [Export(PropertyHint.MultilineText)]
  public string Description { get; set; } = "Upgrade Description";

  // 用于存储主要效果值，例如伤害 +10% 就存 0.1
  [Export]
  public float Value1 { get; set; } = 0.0f;

  // 用于存储次要效果值，例如「移动射击专精」的负面效果
  [Export]
  public float Value2 { get; set; } = 0.0f;
}
