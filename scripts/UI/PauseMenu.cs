using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class PauseMenu : CanvasLayer {
  [Signal]
  public delegate void RestartRequestedEventHandler();

  private Label _titleLabel;
  private Button _continueButton;
  private Button _restartButton;
  private Button _optionsButton;

  private readonly List<Button> _activeButtons = new();
  private int _selectedIndex = 0;

  public override void _Ready() {
    // 此节点应在游戏暂停时也能处理输入．
    ProcessMode = ProcessModeEnum.Always;

    _titleLabel = GetNode<Label>("CenterContainer/VBoxContainer/TitleLabel");
    _continueButton = GetNode<Button>("CenterContainer/VBoxContainer/ContinueButton");
    _restartButton = GetNode<Button>("CenterContainer/VBoxContainer/RestartButton");
    _optionsButton = GetNode<Button>("CenterContainer/VBoxContainer/OptionsButton");

    _continueButton.Pressed += OnContinuePressed;
    _restartButton.Pressed += OnRestartPressed;
    // 「选项」按钮是禁用的，所以不需要连接信号．

    Visible = false; // 初始时隐藏
  }

  public override void _Input(InputEvent @event) {
    // 仅在菜单可见时处理输入
    if (!Visible) {
      return;
    }

    // 将输入事件标记为已处理，以防止游戏世界中的其他节点接收到它．
    GetViewport().SetInputAsHandled();

    if (@event.IsActionPressed("ui_down")) {
      _selectedIndex = (_selectedIndex + 1) % _activeButtons.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_up")) {
      _selectedIndex = (_selectedIndex - 1 + _activeButtons.Count) % _activeButtons.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_accept")) {
      _activeButtons[_selectedIndex].EmitSignal(Button.SignalName.Pressed);
    } else if (@event.IsActionPressed("ui_cancel")) {
      // 如果是暂停菜单，「取消」意味着「继续」．
      // 如果是死亡菜单，「取消」意味着「重新开始」（默认选项）．
      if (_continueButton.Visible) {
        OnContinuePressed();
      } else {
        OnRestartPressed();
      }
    }
  }

  /// <summary>
  /// 显示菜单．
  /// </summary>
  /// <param name="isDeathMenu">如果为 true，则显示死亡菜单，否则显示暂停菜单．</param>
  public void ShowMenu(bool isDeathMenu) {
    Visible = true;
    GetTree().Paused = true;

    _activeButtons.Clear();

    if (isDeathMenu) {
      _titleLabel.Text = "YOU DIED";
      _continueButton.Visible = false;
      _activeButtons.Add(_restartButton);
      _activeButtons.Add(_optionsButton);
      _selectedIndex = 0; // 默认选择「重新开始」
    } else {
      _titleLabel.Text = "PAUSED";
      _continueButton.Visible = true;
      _activeButtons.Add(_continueButton);
      _activeButtons.Add(_restartButton);
      _activeButtons.Add(_optionsButton);
      _selectedIndex = 0; // 默认选择「继续」
    }

    UpdateSelection();
  }

  private void HideMenu() {
    Visible = false;
    GetTree().Paused = false;
  }

  private void UpdateSelection() {
    if (_selectedIndex >= 0 && _selectedIndex < _activeButtons.Count) {
      _activeButtons[_selectedIndex].GrabFocus();
    }
  }

  private void OnContinuePressed() {
    HideMenu();
  }

  private void OnRestartPressed() {
    // 在发出信号前取消暂停并隐藏菜单
    HideMenu();
    EmitSignal(SignalName.RestartRequested);
  }
}
