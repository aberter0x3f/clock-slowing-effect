using System.Collections.Generic;
using System.Linq;
using Godot;

namespace UI;

/// <summary>
/// 强化总览菜单，用于显示玩家已获得和待定的所有强化．
/// </summary>
public partial class UpgradeOverviewMenu : Control, IMenuPanel { // 实现 IMenuPanel 接口
  [Export]
  public PackedScene UpgradeIconScene { get; set; } // 用于网格中每个图标的场景

  // 预览面板中的节点引用
  private Label _previewName;
  private RichTextLabel _previewDescription;

  // 网格容器的引用
  private GridContainer _grid;

  private readonly List<UpgradeIcon> _icons = new();

  public override void _Ready() {
    // 获取节点引用
    _previewName = GetNode<Label>("HBoxContainer/VBoxContainer/NameLabel");
    _previewDescription = GetNode<RichTextLabel>("HBoxContainer/VBoxContainer/DescriptionLabel");
    _grid = GetNode<GridContainer>("HBoxContainer/ScrollContainer/Grid");

    PopulateGrids();
  }

  private void PopulateGrids() {
    // 清理旧图标
    foreach (var icon in _icons) {
      icon.QueueFree();
    }
    _icons.Clear();

    // 清理旧选项
    foreach (Node child in _grid.GetChildren()) {
      child.QueueFree();
    }

    var gm = GameManager.Instance;
    if (gm == null) return;

    // 填充玩家当前生效的所有强化
    foreach (var upgrade in gm.GetCurrentAndPendingUpgrades().OrderBy(u => u.Name)) {
      AddIconToGrid(_grid, upgrade);
    }

    // 设置初始焦点并更新预览
    if (_icons.Count > 0) {
      _icons[0].GrabFocus();
      UpdatePreview(_icons[0].RepresentedUpgrade);
    } else {
      ClearPreview();
    }
  }

  private void AddIconToGrid(GridContainer grid, Upgrade upgrade) {
    var icon = UpgradeIconScene.Instantiate<UpgradeIcon>();
    var label = icon.GetNode<Label>("Label");
    label.Text = upgrade.ShortName;
    label.Modulate = Upgrade.LevelColors[upgrade.Level];
    icon.RepresentedUpgrade = upgrade;
    icon.IconFocused += OnIconFocused;
    grid.AddChild(icon);
    _icons.Add(icon);
  }

  private void OnIconFocused(Upgrade upgrade) {
    UpdatePreview(upgrade);
  }

  private void UpdatePreview(Upgrade upgrade) {
    if (upgrade == null) {
      ClearPreview();
      return;
    }

    var nameColor = Upgrade.LevelColors[upgrade.Level];
    _previewName.Text = upgrade.Name;
    _previewName.Modulate = nameColor;
    _previewDescription.Text = upgrade.Description;
  }

  private void ClearPreview() {
    _previewName.Text = "Empty";
    _previewDescription.Text = "No upgrades acquired yet.";
  }

  /// <summary>
  /// 由父菜单调用，用于在此视图变为可见时设置初始焦点．
  /// </summary>
  public void GrabInitialFocus() {
    if (_icons.Count > 0) {
      _icons[0].GrabFocus();
    }
  }
}
