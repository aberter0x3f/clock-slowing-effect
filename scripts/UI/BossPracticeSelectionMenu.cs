using System.Collections.Generic;
using Enemy.Boss;
using Godot;

namespace UI;

public partial class BossPracticeSelectionMenu : CanvasLayer {
  [Signal]
  public delegate void PhaseSelectedEventHandler(int plane, int phaseIndex);
  [Signal]
  public delegate void MenuCancelledEventHandler();

  [Export]
  public PackedScene ButtonScene { get; set; }

  private Container _buttonContainer;
  private readonly List<Button> _buttons = new();
  private int _selectedIndex = 0;

  public override void _Ready() {
    ProcessMode = ProcessModeEnum.Always;
    _buttonContainer = GetNode<Container>("Panel/CenterContainer/GridContainer");
    Visible = false;
  }

  public void ShowMenu(BossPhaseData phaseData) {
    GetTree().Paused = true;
    Visible = true;

    // 清理旧按钮
    foreach (var button in _buttons) {
      button.QueueFree();
    }
    _buttons.Clear();

    // 用于填充一个位面所有阶段的辅助函数
    void PopulateSet(Godot.Collections.Array<PackedScene> phaseSet, int planeNum) {
      if (phaseSet == null) return;
      for (int i = 0; i < phaseSet.Count; ++i) {
        var phaseScene = phaseSet[i];
        if (phaseScene == null) continue;

        var button = ButtonScene.Instantiate<Button>();
        button.Text = $"Plane {planeNum} - Phase {i}";

        int plane = planeNum;
        int index = i;
        button.Pressed += () => OnPhaseSelected(plane, index);
        _buttonContainer.AddChild(button);
        _buttons.Add(button);
      }
    }

    PopulateSet(phaseData.PhaseSet1, 1);
    PopulateSet(phaseData.PhaseSet2, 2);
    PopulateSet(phaseData.PhaseSet3, 3);

    if (_buttons.Count > 0) {
      _selectedIndex = 0;
      UpdateSelection();
    }
  }

  public override void _Input(InputEvent @event) {
    if (!Visible) return;

    GetViewport().SetInputAsHandled();

    if (@event.IsActionPressed("ui_down")) {
      _selectedIndex = (_selectedIndex + 1) % _buttons.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_up")) {
      _selectedIndex = (_selectedIndex - 1 + _buttons.Count) % _buttons.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_accept")) {
      if (_selectedIndex >= 0 && _selectedIndex < _buttons.Count) {
        _buttons[_selectedIndex].EmitSignal(Button.SignalName.Pressed);
      }
    } else if (@event.IsActionPressed("ui_cancel")) {
      EmitSignal(SignalName.MenuCancelled);
      HideMenu();
    }
  }

  private void UpdateSelection() {
    if (_buttons.Count > 0 && _selectedIndex < _buttons.Count) {
      _buttons[_selectedIndex].GrabFocus();
    }
  }

  private void OnPhaseSelected(int plane, int phaseIndex) {
    HideMenu();
    EmitSignal(SignalName.PhaseSelected, plane, phaseIndex);
  }

  private void HideMenu() {
    Visible = false;
    GetTree().Paused = false;
    QueueFree();
  }
}
