using Godot;

[GlobalClass]
public partial class UpgradeDatabase : Resource {
  [Export]
  public Godot.Collections.Array<Upgrade> AllUpgrades { get; set; }
}
