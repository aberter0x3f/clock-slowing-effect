using Godot;
using Rewind;

public partial class GameWorldEnvironment : WorldEnvironment {
  [Export]
  public Texture RewindingColorCorrection { get; set; }
  [Export]
  public Texture HyperColorCorrection { get; set; }

  private Player _player;

  public override void _Ready() {
    CallDeferred(nameof(FetchGameReferences));
  }

  private void FetchGameReferences() {
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");
  }

  public override void _Process(double delta) {
    base._Process(delta);

    var rm = RewindManager.Instance;

    if (rm.IsPreviewing || rm.IsRewinding) {
      Environment.AdjustmentEnabled = true;
      Environment.AdjustmentColorCorrection = RewindingColorCorrection;
    } else if (_player.IsHyperActive) {
      Environment.AdjustmentEnabled = true;
      Environment.AdjustmentColorCorrection = HyperColorCorrection;
    } else {
      Environment.AdjustmentEnabled = false;
      Environment.AdjustmentColorCorrection = null;
    }
  }
}
