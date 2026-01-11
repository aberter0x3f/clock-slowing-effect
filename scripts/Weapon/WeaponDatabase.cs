using Godot;

namespace Weapon;

[GlobalClass]
public partial class WeaponDatabase : Resource {
  [Export]
  public Godot.Collections.Array<WeaponDefinition> AllWeapons { get; set; }
}
