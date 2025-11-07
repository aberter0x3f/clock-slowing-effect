using Godot;
using Rewind;

namespace UI;

public partial class RewindLabel : Label {
  public override void _Process(double delta) {
    base._Process(delta);

    var rm = RewindManager.Instance;

    if (rm.IsPreviewing) {
      Visible = true;
      Text = $"-{rm.PreviewRewindTime:F2}";
      if (rm.IsAutoRewinding && rm.AutoRewindRemainingTime <= 0.2) {
        Modulate = new Color(1, 0.5f, 0.5f);
      } else {
        Modulate = Colors.White;
      }
    } else {
      Visible = false;
    }
  }
}
