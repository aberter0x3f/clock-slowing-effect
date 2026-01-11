using System.Collections.Generic;
using System.Linq;
using Curio;
using Godot;

namespace UI;

/// <summary>
/// 奇物总览菜单，用于显示玩家已获得和待定的所有奇物．
/// </summary>
public partial class CurioOverviewMenu : Control, IMenuPanel {
  [Export]
  public PackedScene CurioIconScene { get; set; } // 用于网格中每个图标的场景

  // 预览面板中的节点引用
  private Label _previewName;
  private RichTextLabel _previewDescription;

  // 网格容器的引用
  private GridContainer _grid;

  private readonly List<CurioIcon> _icons = new();

  public override void _Ready() {
    // 获取节点引用，假设场景结构与 UpgradeOverviewMenu 一致
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

    // 填充玩家当前生效的所有奇物
    foreach (var curio in gm.GetCurrentAndPendingCurios().OrderBy(c => c.Name)) {
      AddIconToGrid(_grid, curio);
    }

    // 设置初始焦点并更新预览
    if (_icons.Count > 0) {
      _icons[0].GrabFocus();
      UpdatePreview(_icons[0].RepresentedCurio);
    } else {
      ClearPreview();
    }
  }

  private void AddIconToGrid(GridContainer grid, BaseCurio curio) {
    if (CurioIconScene == null) {
      GD.PrintErr("CurioOverviewMenu: CurioIconScene is not assigned.");
      return;
    }

    var icon = CurioIconScene.Instantiate<CurioIcon>();
    var label = icon.GetNode<Label>("Label");

    string iconText = curio.Name.Length >= 1 ? curio.Name.Substring(0, 1) : curio.Name;
    label.Text = iconText;

    // 奇物统一使用一种特殊的颜色
    label.Modulate = new Color("e66060");

    icon.RepresentedCurio = curio;
    icon.IconFocused += OnIconFocused;

    grid.AddChild(icon);
    _icons.Add(icon);
  }

  private void OnIconFocused(BaseCurio curio) {
    UpdatePreview(curio);
  }

  private void UpdatePreview(BaseCurio curio) {
    if (curio == null) {
      ClearPreview();
      return;
    }

    _previewName.Text = curio.Name;
    _previewName.Modulate = new Color("e66060");

    string cooldownText = curio.Cooldown > 0 ? $"\n\n[color=gray]Cooldown: {curio.Cooldown:F1}s[/color]" : "";
    _previewDescription.Text = curio.Description + cooldownText;
  }

  private void ClearPreview() {
    _previewName.Text = "Empty";
    _previewDescription.Text = "No curios acquired yet.";
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
