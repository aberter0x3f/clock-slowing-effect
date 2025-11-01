using Godot;
using Rewind;

public partial class GameWorldEnvironment : WorldEnvironment {
  [Export]
  public Texture RewindingColorCorrection { get; set; }

  public override void _Process(double delta) {
    base._Process(delta);

    var rm = RewindManager.Instance;

    if (rm.IsPreviewing || rm.IsRewinding) {
      Environment.AdjustmentEnabled = true;
      Environment.AdjustmentColorCorrection = RewindingColorCorrection;
    } else {
      Environment.AdjustmentEnabled = false;
      Environment.AdjustmentColorCorrection = null;
    }
  }
}
