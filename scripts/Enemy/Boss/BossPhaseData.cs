using Godot;

namespace Enemy.Boss;

/// <summary>
/// 一个用于存储 Boss 阶段配置的数据资源．
/// 这使得阶段数据可以与 Boss 场景本身解耦，便于在不同地方（如 Boss 战斗和练习菜单）重用．
/// </summary>
[GlobalClass]
public partial class BossPhaseData : Resource {
  [Export]
  public Godot.Collections.Array<PackedScene> PhaseSet1 { get; set; }
  [Export]
  public Godot.Collections.Array<PackedScene> PhaseSet2 { get; set; }
  [Export]
  public Godot.Collections.Array<PackedScene> PhaseSet3 { get; set; }
}
