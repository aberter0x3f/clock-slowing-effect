using System.Collections.Generic;
using Curio;
using Godot;

namespace UI;

public partial class CurioSelectionMenu : CanvasLayer {
  [Signal]
  public delegate void CurioSelectionFinishedEventHandler();

  [Export]
  public PackedScene CurioCardScene { get; set; } // 用于显示单个奇物选项的场景

  private Panel _panel;
  private Label _titleLabel;
  private HBoxContainer _cardContainer;
  private ConfirmationDialog _skipConfirmationDialog;
  private readonly List<Button> _cards = new();
  private readonly List<BaseCurio> _currentChoices = new();
  private int _selectedIndex = 0;

  public override void _Ready() {
    ProcessMode = ProcessModeEnum.Always;

    _panel = GetNode<Panel>("Panel");
    _titleLabel = _panel.GetNode<Label>("CenterContainer/VBoxContainer/TitleLabel");
    _cardContainer = _panel.GetNode<HBoxContainer>("CenterContainer/VBoxContainer/ScrollContainer/CurioCardContainer");
    _skipConfirmationDialog = GetNode<ConfirmationDialog>("SkipConfirmationDialog");
    _skipConfirmationDialog.Confirmed += OnSkipConfirmed;

    Visible = false;
  }

  /// <summary>
  /// 显示奇物选择菜单，从所有奇物中随机挑选．
  /// </summary>
  public void StartCurioSelection(int choiceCount, RandomNumberGenerator rng) {
    var choices = GameManager.Instance.GetCuriosToGain(choiceCount, rng);
    ShowChoices(choices);
  }

  /// <summary>
  /// 显示奇物选择菜单，从一个给定的列表中选择．
  /// </summary>
  public void StartCurioSelection(Godot.Collections.Array<BaseCurio> availableChoices, RandomNumberGenerator rng) {
    var choices = new List<BaseCurio>(availableChoices);
    ShowChoices(choices);
  }

  private void ShowChoices(List<BaseCurio> choices) {
    _titleLabel.Text = "Select a Curio";
    _skipConfirmationDialog.DialogText = "Are you sure you want to skip this curio choice?";

    GetTree().Paused = true;

    // 清理旧选项
    foreach (Node child in _cardContainer.GetChildren()) {
      child.QueueFree();
    }
    _cards.Clear();
    _currentChoices.Clear();
    _currentChoices.AddRange(choices);

    if (_currentChoices.Count == 0) {
      GD.Print("No available curios to choose from. Skipping.");
      FinishSelection();
      return;
    }

    Visible = true;

    for (int i = 0; i < _currentChoices.Count; ++i) {
      var curio = _currentChoices[i];
      var card = CurioCardScene.Instantiate<Button>();
      var nameLabel = card.GetNode<Label>("VBoxContainer/NameLabel");
      var cdLabel = card.GetNode<Label>("VBoxContainer/CDLabel");
      var descLabel = card.GetNode<RichTextLabel>("VBoxContainer/DescriptionLabel");

      nameLabel.Text = curio.Name;
      cdLabel.Text = $"CD: {curio.Cooldown:F2} seconds";
      descLabel.Text = curio.Description;

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
      _skipConfirmationDialog.PopupCentered();
    }
  }

  private void UpdateSelection() {
    if (_cards.Count > 0 && _selectedIndex < _cards.Count) {
      _cards[_selectedIndex].GrabFocus();
    }
  }

  private void OnCardSelected(int index) {
    if (index < 0 || index >= _currentChoices.Count) return;

    var selectedCurio = _currentChoices[index];
    var player = GetTree().Root.GetNode<Player>("GameRoot/Player");
    GameManager.Instance.AddCurio(selectedCurio, player);

    FinishSelection();
  }

  private void OnSkipConfirmed() {
    FinishSelection();
  }

  private void FinishSelection() {
    Visible = false;
    GetTree().Paused = false;
    EmitSignal(SignalName.CurioSelectionFinished);
    QueueFree();
  }
}
