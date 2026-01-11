using System.Collections.Generic;
using Godot;

namespace UI;

public partial class PauseMenu : CanvasLayer {
  [Signal]
  public delegate void RestartRequestedEventHandler();
  [Signal]
  public delegate void RestartFromPhaseRequestedEventHandler();

  private Label _titleLabel;
  private Button _continueButton;
  private Button _restartFromPhaseButton;
  private Button _restartButton;
  private Button _returnToTitleButton;

  private readonly List<Button> _buttons = new();
  private int _selectedIndex = 0;

  private float _continueCooldownTimer;
  private bool _isContinueOnCooldown = false;

  [Export(PropertyHint.File, "*.tscn")]
  public string TitleScenePath { get; set; }

  [Export]
  public float ContinueCooldownDuration { get; set; } = 3.0f;

  public bool EnablePhaseRestart { get; set; } = false;

  public override void _Ready() {
    // 此节点应在游戏暂停时也能处理输入．
    ProcessMode = ProcessModeEnum.Always;

    _titleLabel = GetNode<Label>("Panel/CenterContainer/VBoxContainer/TitleLabel");
    _continueButton = GetNode<Button>("Panel/CenterContainer/VBoxContainer/ContinueButton");
    _restartFromPhaseButton = GetNode<Button>("Panel/CenterContainer/VBoxContainer/RestartFromPhaseButton");
    _restartButton = GetNode<Button>("Panel/CenterContainer/VBoxContainer/RestartButton");
    _returnToTitleButton = GetNode<Button>("Panel/CenterContainer/VBoxContainer/ReturnToTitleButton");

    _continueButton.Pressed += OnContinuePressed;
    _restartFromPhaseButton.Pressed += OnRestartFromPhasePressed;
    _restartButton.Pressed += OnRestartPressed;
    _returnToTitleButton.Pressed += OnReturnToTitlePressed;

    Visible = false; // 初始时隐藏
  }

  public override void _Process(double delta) {
    // 如果菜单不可见或冷却未激活，则不执行任何操作
    if (!Visible || !_isContinueOnCooldown) return;

    _continueCooldownTimer -= (float) delta;
    if (_continueCooldownTimer > 0) {
      // 更新按钮文本以显示倒计时
      _continueButton.Text = $"Continue ({_continueCooldownTimer:F1}s)";
    } else {
      // 冷却结束
      _isContinueOnCooldown = false;
      _continueButton.Disabled = false;
      _continueButton.Text = "Continue";
      // 冷却结束后，如果玩家的焦点还在这个按钮上，则重新聚焦以确保视觉效果正确
      if (_selectedIndex == _buttons.IndexOf(_continueButton)) {
        _continueButton.GrabFocus();
      }
    }
  }

  public override void _Input(InputEvent @event) {
    // 仅在菜单可见时处理输入
    if (!Visible) {
      return;
    }

    // 将输入事件标记为已处理，以防止游戏世界中的其他节点接收到它．
    GetViewport().SetInputAsHandled();

    if (@event.IsActionPressed("ui_down")) {
      _selectedIndex = (_selectedIndex + 1) % _buttons.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_up")) {
      _selectedIndex = (_selectedIndex - 1 + _buttons.Count) % _buttons.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_accept")) {
      _buttons[_selectedIndex].EmitSignal(Button.SignalName.Pressed);
    } else if (@event.IsActionPressed("ui_cancel")) {
      // 只有在「继续」按钮可见且不在冷却中时，才允许通过快捷键关闭菜单
      if (_continueButton.Visible) {
        if (!_isContinueOnCooldown) {
          OnContinuePressed();
        }
      } else if (EnablePhaseRestart) {
        OnRestartFromPhasePressed();
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

    _buttons.Clear();
    _restartFromPhaseButton.Visible = EnablePhaseRestart;

    if (isDeathMenu) {
      _titleLabel.Text = "GAME OVER";
      _continueButton.Visible = false;
      _isContinueOnCooldown = false; // 死亡菜单没有继续按钮，禁用冷却
      _selectedIndex = 0; // 默认选择「重新开始」
    } else {
      _titleLabel.Text = "PAUSED";
      _continueButton.Visible = true;
      _buttons.Add(_continueButton);
      _selectedIndex = 0; // 默认选择「继续」

      // 启动继续按钮的冷却
      _isContinueOnCooldown = true;
      _continueCooldownTimer = ContinueCooldownDuration;
      _continueButton.Disabled = true;
    }

    if (EnablePhaseRestart) {
      _buttons.Add(_restartFromPhaseButton);
    }
    _buttons.Add(_restartButton);
    _buttons.Add(_returnToTitleButton);

    UpdateSelection();
  }

  private void HideMenu() {
    Visible = false;
    GetTree().Paused = false;
  }

  private void UpdateSelection() {
    if (_buttons.Count == 0) return;
    if (_selectedIndex < 0) _selectedIndex = 0;
    if (_selectedIndex >= _buttons.Count) _selectedIndex = _buttons.Count - 1;
    _buttons[_selectedIndex].GrabFocus();
  }

  private void OnContinuePressed() {
    // 增加一个保护，防止在冷却期间通过某种方式（例如直接点击）触发
    if (_isContinueOnCooldown) return;
    HideMenu();
  }

  private void OnRestartPressed() {
    // 在发出信号前取消暂停并隐藏菜单
    HideMenu();
    EmitSignal(SignalName.RestartRequested);
  }

  private void OnRestartFromPhasePressed() {
    HideMenu();
    EmitSignal(SignalName.RestartFromPhaseRequested);
  }

  private void OnReturnToTitlePressed() {
    HideMenu();

    GetTree().Paused = false;
    if (TimeManager.Instance != null) {
      TimeManager.Instance.TimeScale = 1.0f;
    }

    GameManager.Instance.ResetPlayerStats();

    GetTree().ChangeSceneToFile(TitleScenePath);
  }
}
