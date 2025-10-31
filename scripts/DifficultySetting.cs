using Godot;

[GlobalClass]
public partial class DifficultySetting : Resource {
  [Export]
  public string Name { get; set; } = "Difficulty Name";

  [Export(PropertyHint.MultilineText)]
  public string Description { get; set; } = "Difficulty Description";

  [Export]
  public float InitialTotalDifficulty { get; set; } = 40.0f;

  [Export]
  public float InitialMaxConcurrentDifficulty { get; set; } = 20.0f;

  [Export(PropertyHint.Range, "1.0, 2.0, 0.01")]
  public float PerLevelDifficultyMultiplier { get; set; } = 1.1f;

  [Export]
  public int EnemyRank { get; set; } = 5;
}
