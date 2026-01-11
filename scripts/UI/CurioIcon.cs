using Curio;
using Godot;

namespace UI;

/// <summary>
/// 代表奇物总览菜单网格中的一个可交互图标．
/// 它持有一个 BaseCurio 资源的引用，并在获得焦点时发出信号．
/// </summary>
public partial class CurioIcon : Button {
  [Signal]
  public delegate void IconFocusedEventHandler(BaseCurio curio);

  public BaseCurio RepresentedCurio { get; set; }

  public override void _Ready() {
    FocusEntered += OnFocusEntered;
  }

  private void OnFocusEntered() {
    EmitSignal(SignalName.IconFocused, RepresentedCurio);
  }
}
