using System.Collections.Generic;
using Event;
using Godot;

namespace UI;

public partial class EventMenu : CanvasLayer {
  [Signal]
  public delegate void OptionSelectedEventHandler(int index);

  private Label _titleLabel;
  private RichTextLabel _descriptionLabel;
  private VBoxContainer _optionsContainer;
  private readonly List<Button> _optionButtons = new();
  private int _selectedIndex = 0;

  [Export]
  public PackedScene OptionButtonScene { get; set; }

  public override void _Ready() {
    ProcessMode = ProcessModeEnum.Always;

    _titleLabel = GetNode<Label>("Panel/VBoxContainer/TitleLabel");
    _descriptionLabel = GetNode<RichTextLabel>("Panel/VBoxContainer/HBoxContainer/DescriptionLabel");
    _optionsContainer = GetNode<VBoxContainer>("Panel/VBoxContainer/HBoxContainer/MarginContainer/ScrollContainer/OptionsContainer");

    Visible = false;
  }

  public void ShowMenu() {
    Visible = true;
    GetTree().Paused = true;
    UpdateDisplay();
  }

  public void UpdateDisplay() {
    var gameEvent = GameManager.Instance.ActiveEvent;

    _titleLabel.Text = gameEvent.GetTitle();
    _descriptionLabel.Text = gameEvent.GetDescription();

    foreach (var button in _optionButtons) {
      button.QueueFree();
    }
    _optionButtons.Clear();

    // 清理旧选项
    foreach (Node child in _optionsContainer.GetChildren()) {
      child.QueueFree();
    }

    var options = gameEvent.GetOptions();
    for (int i = 0; i < options.Count; ++i) {
      var option = options[i];
      var button = OptionButtonScene.Instantiate<Button>();
      var titleLabel = button.GetNode<Label>("VBoxContainer/TitleLabel");
      var descLabel = button.GetNode<RichTextLabel>("VBoxContainer/DescriptionLabel");

      titleLabel.Text = option.Title;
      descLabel.Text = option.Description;
      button.Disabled = !option.IsEnabled;

      int index = i;
      button.Pressed += () => OnOptionPressed(index);
      _optionsContainer.AddChild(button);
      _optionButtons.Add(button);
    }

    _selectedIndex = 0;
    UpdateSelection();
  }

  public override void _Input(InputEvent @event) {
    if (!Visible) return;

    GetViewport().SetInputAsHandled();

    // 禁用 ui_cancel
    if (@event.IsActionPressed("ui_cancel")) {
      // 什么都不做
      return;
    }

    if (@event.IsActionPressed("ui_down")) {
      _selectedIndex = (_selectedIndex + 1) % _optionButtons.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_up")) {
      _selectedIndex = (_selectedIndex - 1 + _optionButtons.Count) % _optionButtons.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_accept")) {
      if (_selectedIndex >= 0 && _selectedIndex < _optionButtons.Count) {
        if (!_optionButtons[_selectedIndex].Disabled) {
          _optionButtons[_selectedIndex].EmitSignal(Button.SignalName.Pressed);
        }
      }
    }
  }

  private void UpdateSelection() {
    if (_optionButtons.Count > 0 && _selectedIndex < _optionButtons.Count) {
      _optionButtons[_selectedIndex].GrabFocus();
    }
  }

  private void OnOptionPressed(int index) {
    EmitSignal(SignalName.OptionSelected, index);
  }

  public void HideMenu() {
    Visible = false;
    GetTree().Paused = false;
  }
}
