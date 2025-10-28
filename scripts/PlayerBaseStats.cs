using Godot;

[GlobalClass]
public partial class PlayerBaseStats : Resource {
  [Export]
  public float MaxHealth { get; set; } = 60f;
  [Export]
  public float MovementSpeed { get; set; } = 350f;
  [Export]
  public float SlowMovementSpeedScale { get; set; } = 0.45f;
  [Export]
  public float ShootCooldown { get; set; } = 0.03f;
  [Export]
  public float BulletSpreadNormal { get; set; } = float.Pi / 24.0f;
  [Export]
  public float BulletSpreadSlow { get; set; } = float.Pi / 60.0f;
  [Export]
  public float BulletDamage { get; set; } = 0.5f;
  [Export]
  public float MaxAmmo { get; set; } = 20f;
  [Export]
  public float ReloadTime { get; set; } = 3.0f;
  [Export]
  public float GrazeRadius { get; set; } = 40f;
  [Export]
  public float GrazeTimeBonus { get; set; } = 0.3f;
}
