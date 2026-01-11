using Godot;

[GlobalClass]
public partial class PlayerBaseStats : Resource {
  [Export]
  public float MaxHealth { get; set; } = 60f;
  [Export]
  public float MovementSpeed { get; set; } = 3.5f;
  [Export]
  public float SlowMovementSpeedScale { get; set; } = 0.45f;
  [Export]
  public float GrazeRadius { get; set; } = 0.4f;
  [Export]
  public float GrazeTimeBonus { get; set; } = 0.3f;
  [Export]
  public float HyperDuration { get; set; } = 5.0f;
  [Export]
  public int GrazeForFullHyper { get; set; } = 50;
}
