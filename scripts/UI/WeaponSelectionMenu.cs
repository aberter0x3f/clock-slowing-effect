using System.Collections.Generic;
using Godot;
using Weapon;

namespace UI;

public partial class WeaponSelectionMenu : CanvasLayer {
  [Signal] public delegate void WeaponChangedEventHandler();

  [Export]
  public PackedScene WeaponButtonScene { get; set; }

  private VBoxContainer _listContainer;
  private Label _nameLabel;
  private RichTextLabel _descLabel;

  private List<WeaponDefinition> _weapons;
  private readonly List<Button> _buttons = new();
  private int _selectedIndex = 0;

  public override void _Ready() {
    ProcessMode = ProcessModeEnum.Always;

    _listContainer = GetNode<VBoxContainer>("Panel/CenterContainer/HBoxContainer/ListContainer");
    _nameLabel = GetNode<Label>("Panel/CenterContainer/HBoxContainer/DetailsContainer/NameLabel");
    _descLabel = GetNode<RichTextLabel>("Panel/CenterContainer/HBoxContainer/DetailsContainer/DescLabel");

    Visible = false;
  }

  public void ShowMenu() {
    Visible = true;
    GetTree().Paused = true;
    PopulateList();

    // 重置选择索引
    _selectedIndex = 0;
    // 如果之前已经选择了武器，尝试定位到该武器
    var currentWeapon = GameManager.Instance.SelectedWeaponDefinition;
    if (currentWeapon != null && _weapons != null) {
      int idx = _weapons.IndexOf(currentWeapon);
      if (idx != -1) _selectedIndex = idx;
    }

    UpdateSelection();
  }

  private void PopulateList() {
    // 清理旧按钮
    foreach (Node child in _listContainer.GetChildren()) {
      child.QueueFree();
    }
    _buttons.Clear();

    _weapons = new List<WeaponDefinition>(GameManager.Instance.WeaponDb.AllWeapons);

    if (WeaponButtonScene == null) {
      GD.PrintErr("WeaponSelectionMenu: WeaponButtonScene is not set!");
      return;
    }

    for (int i = 0; i < _weapons.Count; ++i) {
      var def = _weapons[i];
      var btn = WeaponButtonScene.Instantiate<Button>();
      btn.Text = def.Name;
      int idx = i;
      btn.Pressed += () => OnWeaponSelected(idx);
      _listContainer.AddChild(btn);
      _buttons.Add(btn);
    }
  }

  public override void _Input(InputEvent @event) {
    if (!Visible) return;

    GetViewport().SetInputAsHandled();

    if (_buttons.Count == 0) {
      if (@event.IsActionPressed("ui_cancel")) CloseMenu();
      return;
    }

    if (@event.IsActionPressed("ui_down")) {
      _selectedIndex = (_selectedIndex + 1) % _buttons.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_up")) {
      _selectedIndex = (_selectedIndex - 1 + _buttons.Count) % _buttons.Count;
      UpdateSelection();
    } else if (@event.IsActionPressed("ui_accept")) {
      OnWeaponSelected(_selectedIndex);
    } else if (@event.IsActionPressed("ui_cancel")) {
      CloseMenu();
    }
  }

  private void UpdateSelection() {
    if (_buttons.Count == 0) return;
    if (_selectedIndex < 0) _selectedIndex = 0;
    if (_selectedIndex >= _buttons.Count) _selectedIndex = _buttons.Count - 1;
    _buttons[_selectedIndex].GrabFocus();
    UpdateDetails();
  }

  private void UpdateDetails() {
    if (_selectedIndex < 0 || _selectedIndex >= _weapons.Count) return;
    var def = _weapons[_selectedIndex];
    _nameLabel.Text = def.Name;
    _descLabel.Text = def.Description;
  }

  private void OnWeaponSelected(int index) {
    if (index < 0 || index >= _weapons.Count) return;
    GameManager.Instance.SelectedWeaponDefinition = _weapons[index];
    CloseMenu();
    EmitSignal(SignalName.WeaponChanged);
  }

  private async void CloseMenu() {
    Visible = false;
    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    await ToSignal(GetTree(), SceneTree.SignalName.ProcessFrame);
    GetTree().Paused = false;
  }
}
