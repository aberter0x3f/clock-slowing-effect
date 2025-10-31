using System.Collections.Generic;
using System.Linq;
using Godot;

namespace UI;

public partial class TransmuterMenu : CanvasLayer {
  [Signal]
  public delegate void TransmutationRequestedEventHandler(Godot.Collections.Array<Upgrade> discardedUpgrades, int targetLevel);

  [Export]
  public PackedScene UpgradeIconScene { get; set; }

  // UI 节点引用
  private GridContainer _grid;
  private Label _previewName;
  private RichTextLabel _previewDescription;
  private Button _transmuteButton;
  private Button _cancelButton;
  private Label _infoLabel;
  private Label _actionsRemainingLabel;

  private readonly List<UpgradeIcon> _icons = new();
  private readonly List<Upgrade> _selectedUpgrades = new();
  private bool _hasBeenUsed = false;

  public override void _Ready() {
    ProcessMode = ProcessModeEnum.Always;
    _actionsRemainingLabel = GetNode<Label>("Panel/VBoxContainer/Header/ActionsRemaining/ValueLabel");
    _grid = GetNode<GridContainer>("Panel/VBoxContainer/Main/ScrollContainer/Grid");
    _previewName = GetNode<Label>("Panel/VBoxContainer/Main/VBoxContainer/NameLabel");
    _previewDescription = GetNode<RichTextLabel>("Panel/VBoxContainer/Main/VBoxContainer/DescriptionLabel");
    _transmuteButton = GetNode<Button>("Panel/VBoxContainer/Footer/TransmuteButton");
    _cancelButton = GetNode<Button>("Panel/VBoxContainer/Footer/CancelButton");
    _infoLabel = GetNode<Label>("Panel/VBoxContainer/Footer/InfoLabel");

    _transmuteButton.Pressed += OnTransmutePressed;
    _cancelButton.Pressed += OnCancelPressed;

    Visible = false;
  }

  public void Popup(bool hasBeenUsed) {
    _hasBeenUsed = hasBeenUsed;

    // 清理
    foreach (var icon in _icons) {
      icon.QueueFree();
    }
    _icons.Clear();
    _selectedUpgrades.Clear();

    // 清理旧选项
    foreach (Node child in _grid.GetChildren()) {
      child.QueueFree();
    }

    // 填充玩家已有的强化
    var gm = GameManager.Instance;
    var allPlayerUpgrades = gm.GetCurrentAndPendingUpgrades().OrderBy(u => u.Level).ThenBy(u => u.Name).ToList();

    foreach (var upgrade in allPlayerUpgrades) {
      var icon = UpgradeIconScene.Instantiate<UpgradeIcon>();
      icon.ToggleMode = true; // 设置为可切换状态
      var label = icon.GetNode<Label>("Label");
      label.Text = upgrade.ShortName;
      label.Modulate = Upgrade.LevelColors[upgrade.Level];
      icon.RepresentedUpgrade = upgrade;
      icon.Toggled += (toggled) => OnIconToggled(icon, toggled);
      icon.IconFocused += OnIconFocused; // 连接焦点信号
      _grid.AddChild(icon);
      _icons.Add(icon);
    }

    Visible = true;
    GetTree().Paused = true;
    UpdateState();

    if (_icons.Count > 0) {
      _icons[0].GrabFocus();
      OnIconFocused(_icons[0].RepresentedUpgrade);
    } else {
      ClearPreview();
    }
  }

  public override void _Input(InputEvent @event) {
    if (!Visible) return;

    // 全局 "ui_accept" 触发交换
    if (@event.IsActionPressed("ui_accept")) {
      GetViewport().SetInputAsHandled();
      OnTransmutePressed();
    } else if (@event.IsActionPressed("ui_select")) {
      GetViewport().SetInputAsHandled();
      // "ui_select" 切换选中状态
      if (GetViewport().GuiGetFocusOwner() is UpgradeIcon focusedIcon) {
        focusedIcon.ButtonPressed = !focusedIcon.ButtonPressed; // 手动切换状态
      }
    } else if (@event.IsActionPressed("ui_cancel")) {
      GetViewport().SetInputAsHandled();
      OnCancelPressed();
    }
  }

  private void OnIconFocused(Upgrade upgrade) {
    UpdatePreview(upgrade);
  }

  private void OnIconToggled(UpgradeIcon icon, bool isToggled) {
    if (isToggled) {
      if (!_selectedUpgrades.Contains(icon.RepresentedUpgrade)) {
        _selectedUpgrades.Add(icon.RepresentedUpgrade);
      }
    } else {
      _selectedUpgrades.Remove(icon.RepresentedUpgrade);
    }
    UpdateState();
  }

  private void UpdateState() {
    _transmuteButton.Disabled = true;
    _transmuteButton.Text = "Transmute";
    _infoLabel.Text = "Select upgrades to transmute.";
    _actionsRemainingLabel.Text = _hasBeenUsed ? "0" : "1";

    if (_hasBeenUsed) {
      _infoLabel.Text = "You have already used the exchanger.";
      return;
    }

    if (_selectedUpgrades.Count == 0) return;

    // 检查所有选中的强化等级是否相同
    int firstLevel = _selectedUpgrades[0].Level;
    if (_selectedUpgrades.Any(u => u.Level != firstLevel)) {
      _infoLabel.Text = "Error: All selected upgrades must be the same level.";
      return;
    }

    // 检查是否满足任何一个配方
    int count = _selectedUpgrades.Count;
    int targetLevel = -1;

    if (count == 1) {
      targetLevel = firstLevel;
      _transmuteButton.Text = $"Get 1x level {targetLevel} Upgrade";
      _transmuteButton.Disabled = false;
    } else if (count == 2) {
      targetLevel = firstLevel + 1;
      _transmuteButton.Text = $"Get 1x level {targetLevel} Upgrade";
      _transmuteButton.Disabled = targetLevel > 3; // 假设最高 3 级
    } else if (count == 4) {
      targetLevel = firstLevel + 2;
      _transmuteButton.Text = $"Get 1x level {targetLevel} Upgrade";
      _transmuteButton.Disabled = targetLevel > 3;
    }

    if (_transmuteButton.Disabled && targetLevel != -1) {
      _infoLabel.Text = $"Error: Cannot transmute to level {targetLevel} (max level is 3).";
    } else {
      _infoLabel.Text = $"Selected {count} upgrade(s) of level {firstLevel}.";
    }
  }

  private void UpdatePreview(Upgrade upgrade) {
    if (upgrade == null) {
      ClearPreview();
      return;
    }
    _previewName.Text = upgrade.Name;
    _previewName.Modulate = Upgrade.LevelColors[upgrade.Level];
    _previewDescription.Text = upgrade.Description;
  }

  private void ClearPreview() {
    _previewName.Text = "No Upgrade";
    _previewDescription.Text = "Select an upgrade from the left panel to see its details.";
  }

  private void OnTransmutePressed() {
    if (_transmuteButton.Disabled || _hasBeenUsed) return;

    int firstLevel = _selectedUpgrades[0].Level;
    int count = _selectedUpgrades.Count;
    int targetLevel = -1;

    if (count == 1) targetLevel = firstLevel;
    else if (count == 2) targetLevel = firstLevel + 1;
    else if (count == 4) targetLevel = firstLevel + 2;

    if (targetLevel != -1) {
      CloseMenu();
      EmitSignal(SignalName.TransmutationRequested, new Godot.Collections.Array<Upgrade>(_selectedUpgrades), targetLevel);
    }
  }

  private void OnCancelPressed() {
    CloseMenu();
  }

  public void CloseMenu() {
    Visible = false;
    GetTree().Paused = false;
  }
}
