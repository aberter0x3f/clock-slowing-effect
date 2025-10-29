using Godot;

namespace UI;

/// <summary>
/// 代表强化总览菜单网格中的一个可交互图标．
/// 它持有一个 Upgrade 资源的引用，并在获得焦点时发出信号．
/// </summary>
[GlobalClass]
public partial class UpgradeIcon : Button {
  [Signal]
  public delegate void IconFocusedEventHandler(Upgrade upgrade);

  public Upgrade RepresentedUpgrade { get; set; }

  public override void _Ready() {
    FocusEntered += OnFocusEntered;
  }

  private void OnFocusEntered() {
    EmitSignal(SignalName.IconFocused, RepresentedUpgrade);
  }
}
