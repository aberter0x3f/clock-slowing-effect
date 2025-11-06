using System.Collections.Generic;
using Godot;

namespace UI;

public partial class UpgradeSelectionMenu : CanvasLayer {
  public enum Mode {
    Gain,
    Lose
  }

  [Signal]
  public delegate void UpgradeSelectionFinishedEventHandler();

  [Export]
  public PackedScene UpgradeCardScene { get; set; } // 用于显示单个强化选项的场景

  private Panel _panel;
  private Label _titleLabel;
  private HBoxContainer _cardContainer;
  private ConfirmationDialog _skipConfirmationDialog;
  private readonly List<Button> _cards = new();
  private readonly List<Upgrade> _currentChoices = new();
  private int _selectedIndex = 0;
  private Mode _currentMode = Mode.Gain;
  private int _picksRemaining = 0;
  private int _minLevel;
  private int _maxLevel;
  private int _choiceCount;
  private RandomNumberGenerator _rng;

  public override void _Ready() {
    ProcessMode = ProcessModeEnum.Always; // 暂停时也能响应

    _panel = GetNode<Panel>("Panel");
    _titleLabel = _panel.GetNode<Label>("CenterContainer/VBoxContainer/TitleLabel");
    _cardContainer = _panel.GetNode<HBoxContainer>("CenterContainer/VBoxContainer/ScrollContainer/UpgradeCardContainer");
    _skipConfirmationDialog = GetNode<ConfirmationDialog>("SkipConfirmationDialog");
    _skipConfirmationDialog.Confirmed += OnSkipConfirmed;

    Visible = false;
  }

  /// <summary>
  /// 显示强化选择菜单．
  /// </summary>
  /// <param name="picks">要进行的强化选择次数．</param>
  /// <param name="minLevel">可供选择的强化最低等级．</param>
  /// <param name="maxLevel">可供选择的强化最高等级．</param>
  /// <param name="rng">用于随机选择的随机数生成器．</param>
  public void StartUpgradeSelection(
      Mode mode,
      int picks,
      int minLevel,
      int maxLevel,
      int choiceCount,
      RandomNumberGenerator rng) {
    _currentMode = mode;
    _picksRemaining = picks;
    _minLevel = minLevel;
    _maxLevel = maxLevel;
    _choiceCount = choiceCount;
    _rng = rng;

    if (_currentMode == Mode.Gain) {
      _titleLabel.Text = "Select an Upgrade";
      _skipConfirmationDialog.DialogText = "Are you sure you want to skip this upgrade choice?";
    } else {
      _titleLabel.Text = "Choose an Upgrade to discard";
      _panel.SelfModulate = Colors.Red;
    }

    GetTree().Paused = true;
    ShowNextChoice();
  }

  private void ShowNextChoice() {
    if (_picksRemaining <= 0) {
      FinishSelection();
      return;
    }

    --_picksRemaining;

    // 清理旧选项
    foreach (Node child in _cardContainer.GetChildren()) {
      child.QueueFree();
    }
    _cards.Clear();
    _currentChoices.Clear();

    List<Upgrade> choices;
    if (_currentMode == Mode.Gain) {
      // 从 GameManager 获取选项，传入本关专用的 RNG
      choices = GameManager.Instance.GetUpgradeToGain(_minLevel, _maxLevel, _choiceCount, _rng);
    } else {
      // 获取要丢弃的选项
      choices = GameManager.Instance.GetUpgradesToLose(_minLevel, _maxLevel, _choiceCount, _rng);
    }
    _currentChoices.AddRange(choices);

    if (_currentChoices.Count == 0) {
      GD.Print("No available upgrades to choose from. Skipping.");
      // 如果没有可选项，直接进入下一轮或结束
      ShowNextChoice();
      return;
    }

    Visible = true;

    for (int i = 0; i < _currentChoices.Count; ++i) {
      var upgrade = _currentChoices[i];
      var card = UpgradeCardScene.Instantiate<Button>();
      var shortNameLabel = card.GetNode<Label>("VBoxContainer/ShortNameLabel");
      var nameLabel = card.GetNode<Label>("VBoxContainer/NameLabel");
      var descLabel = card.GetNode<RichTextLabel>("VBoxContainer/DescriptionLabel");

      shortNameLabel.Text = upgrade.ShortName;
      nameLabel.Text = upgrade.Name;
      descLabel.Text = upgrade.Description;

      var nameColor = Upgrade.LevelColors[upgrade.Level];

      shortNameLabel.Modulate = nameColor;
      nameLabel.Modulate = nameColor;
      int index = i;
      card.Pressed += () => OnCardSelected(index);
      _cardContainer.AddChild(card);
      _cards.Add(card);
    }

    _selectedIndex = 0;
    UpdateSelection();
  }

  public override void _Input(InputEvent @event) {
    if (!Visible) return;

    GetViewport().SetInputAsHandled();

    if (@event.IsActionPressed("ui_right")) {
      _selectedIndex = (_selectedIndex + 1) % _cards.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_left")) {
      _selectedIndex = (_selectedIndex - 1 + _cards.Count) % _cards.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_accept")) {
      OnCardSelected(_selectedIndex);
    } else if (@event.IsActionPressed("ui_cancel")) {
      if (_currentMode == Mode.Gain) {
        _skipConfirmationDialog.PopupCentered();
      } else {
        // 失去模式下不允许跳过
      }
    }
  }

  private void UpdateSelection() {
    if (_cards.Count > 0 && _selectedIndex < _cards.Count) {
      _cards[_selectedIndex].GrabFocus();
    }
  }

  private void OnCardSelected(int index) {
    if (index < 0 || index >= _currentChoices.Count) return;

    if (_currentMode == Mode.Gain) {
      var selectedUpgrade = _currentChoices[index];
      GameManager.Instance.AddUpgrade(selectedUpgrade);
    } else {
      var discardedUpgrade = _currentChoices[index];
      GameManager.Instance.RemoveUpgrade(discardedUpgrade);
    }

    ShowNextChoice();
  }

  private void OnSkipConfirmed() {
    if (_currentMode == Mode.Lose) return;
    ShowNextChoice();
  }

  private void FinishSelection() {
    Visible = false;
    GetTree().Paused = false;
    EmitSignal(SignalName.UpgradeSelectionFinished);
    // 自我销毁，因为每次都是新实例
    QueueFree();
  }
}
