using Godot;

namespace Weapon;

/// <summary>
/// 仅用于定义武器在 UI 中的显示信息以及对应的场景文件．
/// 实际的战斗数值和逻辑都在 WeaponScene 对应的脚本中．
/// </summary>
[GlobalClass]
public partial class WeaponDefinition : Resource {
  [Export] public string Name { get; set; } = "Weapon Name";
  [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "Description";

  /// <summary>
  /// 武器的实体场景．该场景根节点必须挂载继承自 Weapon 的脚本．
  /// </summary>
  [Export] public PackedScene WeaponScene { get; set; }
}
