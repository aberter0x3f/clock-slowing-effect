using Godot;

namespace UI;

public partial class ShopIcon : VBoxContainer {
  [Signal]
  public delegate void IconFocusedEventHandler(Upgrade upgrade, float cost);

  public Upgrade RepresentedUpgrade { get; set; }
  public float Cost { get; set; }

  public override void _Ready() {
    GetNode<Button>("Button").FocusEntered += OnFocusEntered;
  }

  private void OnFocusEntered() {
    EmitSignal(SignalName.IconFocused, RepresentedUpgrade, Cost);
  }
}
