using System.Collections.Generic;
using Godot;

namespace UI;

public partial class DifficultyMenu : CanvasLayer { // 继承 CanvasLayer
  [Signal]
  public delegate void DifficultySelectedEventHandler(DifficultySetting difficulty);

  [Export]
  public Godot.Collections.Array<DifficultySetting> Difficulties { get; set; }

  [Export]
  public PackedScene DifficultyButtonScene { get; set; }

  private VBoxContainer _buttonContainer;
  private RichTextLabel _descriptionLabel;
  private readonly List<Button> _buttons = new();
  private int _selectedIndex = 0;

  public override void _Ready() {
    // 确保在游戏暂停时也能处理输入
    ProcessMode = ProcessModeEnum.Always;

    _buttonContainer = GetNode<VBoxContainer>("Panel/CenterContainer/HBoxContainer/VBoxContainer");
    _descriptionLabel = GetNode<RichTextLabel>("Panel/CenterContainer/HBoxContainer/RichTextLabel");

    PopulateButtons();
    UpdateDescription();
    HideMenu(); // 默认隐藏
  }

  private void PopulateButtons() {
    // 清理旧按钮（如果需要动态更新）
    foreach (Node child in _buttonContainer.GetChildren()) {
      child.QueueFree();
    }
    _buttons.Clear();

    if (Difficulties == null || Difficulties.Count == 0) {
      GD.PrintErr("DifficultyMenu: No difficulties assigned in the inspector.");
      return;
    }

    for (int i = 0; i < Difficulties.Count; ++i) {
      var difficulty = Difficulties[i];
      var button = DifficultyButtonScene.Instantiate<Button>();
      button.Text = difficulty.Name;
      int index = i;
      button.Pressed += () => OnDifficultySelected(index);
      _buttonContainer.AddChild(button);
      _buttons.Add(button);
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
      OnDifficultySelected(_selectedIndex);
    } else if (@event.IsActionPressed("ui_cancel")) {
      HideMenu(); // 按取消键关闭菜单
    }
  }

  public void ShowMenu() {
    GetTree().Paused = true;
    Visible = true;
    _selectedIndex = 0;
    UpdateSelection();
  }

  public void HideMenu() {
    Visible = false;
    GetTree().Paused = false;
  }

  private void UpdateSelection() {
    if (_buttons.Count == 0) return;
    for (int i = 0; i < _buttons.Count; ++i) {
      if (i == _selectedIndex) {
        _buttons[i].GrabFocus();
      }
    }
    UpdateDescription();
  }

  private void UpdateDescription() {
    if (_selectedIndex < 0 || _selectedIndex >= Difficulties.Count) return;

    var difficulty = Difficulties[_selectedIndex];
    var desc = $"[b]{difficulty.Name}[/b]\n\n" +
               $"{difficulty.Description}\n\n" +
               $"- Initial difficulty: {difficulty.InitialTotalDifficulty}\n" +
               $"- Initial concurrent difficulty: {difficulty.InitialMaxConcurrentDifficulty}\n" +
               $"- Per level difficulty multiplier: x{difficulty.PerLevelDifficultyMultiplier}\n" +
               $"- Enemy rank: {difficulty.EnemyRank}";
    _descriptionLabel.Text = desc;
  }

  private void OnDifficultySelected(int index) {
    if (index < 0 || index >= Difficulties.Count) return;

    var selectedDifficulty = Difficulties[index];
    HideMenu();
    EmitSignal(SignalName.DifficultySelected, selectedDifficulty);
  }
}
