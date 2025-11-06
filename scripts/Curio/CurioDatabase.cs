using Godot;

namespace Curio;

[GlobalClass]
public partial class CurioDatabase : Resource {
  [Export]
  public Godot.Collections.Array<BaseCurio> AllCurios { get; set; }

  [Export(PropertyHint.Range, "1, 10, 1")]
  public int StartingCurioCount { get; set; } = 3;
}
