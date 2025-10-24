using Godot;

[GlobalClass]
public partial class EnemyData : Resource {
  [Export]
  public PackedScene Scene { get; set; }

  [Export(PropertyHint.Range, "1, 100, 1")]
  public float Difficulty { get; set; } = 1.0f;
}
