using System.Collections.Generic;
using Godot;

namespace UI;

public partial class InterLevelMenu : Control {
  private readonly List<Control> _menuPanels = new();
  private int _currentPanelIndex = 0;

  public override void _Ready() {
    // 自动查找所有作为直接子节点的、实现了 IMenuPanel 接口的 Control
    foreach (var child in GetChildren()) {
      if (child is Control panelControl && child is IMenuPanel) {
        _menuPanels.Add(panelControl);
      }
    }

    if (_menuPanels.Count > 0) {
      // 默认显示第一个面板，隐藏其他所有面板
      SwitchToPanel(0);
    } else {
      GD.PrintErr("InterLevelMenu has no valid menu panels as direct children.");
    }
  }

  public override void _Input(InputEvent @event) {
    if (_menuPanels.Count <= 1) return;

    if (@event.IsActionPressed("menu_switch_tab")) {
      // 循环切换到下一个面板
      _currentPanelIndex = (_currentPanelIndex + 1) % _menuPanels.Count;
      SwitchToPanel(_currentPanelIndex);
      GetViewport().SetInputAsHandled();
    }
  }

  /// <summary>
  /// 切换到指定索引的菜单面板．
  /// </summary>
  private void SwitchToPanel(int index) {
    if (index < 0 || index >= _menuPanels.Count) return;

    _currentPanelIndex = index;

    for (int i = 0; i < _menuPanels.Count; ++i) {
      var panel = _menuPanels[i];
      panel.Visible = (i == _currentPanelIndex);
    }

    // 为新显示的面板设置焦点
    if (_menuPanels[_currentPanelIndex] is IMenuPanel menuPanel) {
      menuPanel.GrabInitialFocus();
    }
  }
}
