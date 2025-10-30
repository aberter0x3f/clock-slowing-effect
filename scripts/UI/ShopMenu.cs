using System.Collections.Generic;
using Godot;

namespace UI;

public partial class ShopMenu : CanvasLayer {
  [Signal]
  public delegate void PurchaseMadeEventHandler(Upgrade purchasedUpgrade, float cost);

  [Export]
  public PackedScene ShopIconScene { get; set; }

  // UI 节点引用
  private GridContainer _grid;
  private Label _previewName;
  private Label _previewCost;
  private RichTextLabel _previewDescription;
  private Button _cancelButton;
  private Label _purchasesRemainingLabel;

  private readonly List<ShopIcon> _icons = new();
  private List<(Upgrade upgrade, float cost)> _currentInventory = new();
  private int _purchasesRemaining;

  // 当前选中的物品
  private Upgrade _selectedUpgrade;
  private float _selectedCost;

  public override void _Ready() {
    ProcessMode = ProcessModeEnum.Always;

    // 获取所有 UI 节点的引用
    _purchasesRemainingLabel = GetNode<Label>("VBoxContainer/Header/PurchasesRemaining/ValueLabel");
    _grid = GetNode<GridContainer>("VBoxContainer/Main/ScrollContainer/Grid");
    _previewName = GetNode<Label>("VBoxContainer/Main/VBoxContainer/NameLabel");
    _previewDescription = GetNode<RichTextLabel>("VBoxContainer/Main/VBoxContainer/DescriptionLabel");
    _previewCost = GetNode<Label>("VBoxContainer/Main/VBoxContainer/CostLabel");
    _cancelButton = GetNode<Button>("VBoxContainer/Footer/CancelButton");

    _cancelButton.Pressed += OnCancelPressed;

    Visible = false;
  }

  public void Popup(List<(Upgrade, float)> inventory, int purchasesRemaining) {
    _purchasesRemaining = purchasesRemaining;
    _currentInventory = inventory;

    _purchasesRemainingLabel.Text = _purchasesRemaining.ToString();

    // 清理旧图标
    foreach (var icon in _icons) {
      icon.QueueFree();
    }
    _icons.Clear();

    // 清理旧选项
    foreach (Node child in _grid.GetChildren()) {
      child.QueueFree();
    }

    if (_currentInventory.Count == 0) {
      ClearPreview();
      _previewName.Text = "Sold Out";
      _previewDescription.Text = "There are no more items to purchase.";
    } else {
      var gm = GameManager.Instance;
      var currentlyOwned = gm.GetCurrentAndPendingUpgrades();
      // 填充网格
      foreach (var (upgrade, cost) in _currentInventory) {
        var icon = ShopIconScene.Instantiate<ShopIcon>();
        var button = icon.GetNode<Button>("Button");
        var shortNameLabel = button.GetNode<Label>("ShortNameLabel");
        var costLabel = icon.GetNode<Label>("CostLabel");

        shortNameLabel.Text = upgrade.ShortName;

        if (currentlyOwned.Contains(upgrade)) {
          button.Disabled = true;
          costLabel.Text = "Obtained";
        } else {
          costLabel.Text = $"{cost:F0}s";
        }

        shortNameLabel.Modulate = Upgrade.LevelColors[upgrade.Level];

        icon.RepresentedUpgrade = upgrade;
        icon.Cost = cost;
        icon.IconFocused += OnIconFocused;

        _grid.AddChild(icon);
        _icons.Add(icon);
      }
    }

    Visible = true;
    GetTree().Paused = true;

    // 设置初始焦点并更新预览
    if (_icons.Count > 0) {
      _icons[0].GetNode<Button>("Button").GrabFocus();
      OnIconFocused(_icons[0].RepresentedUpgrade, _icons[0].Cost);
    }
  }

  public override void _Input(InputEvent @event) {
    if (!Visible) return;

    if (@event.IsActionPressed("ui_accept")) {
      GetViewport().SetInputAsHandled();
      if (_cancelButton.HasFocus()) {
        OnCancelPressed();
      } else {
        OnPurchasePressed();
      }
    } else if (@event.IsActionPressed("ui_cancel")) {
      GetViewport().SetInputAsHandled();
      OnCancelPressed();
    }
  }

  private void OnIconFocused(Upgrade upgrade, float cost) {
    _selectedUpgrade = upgrade;
    _selectedCost = cost;
    UpdatePreview();
  }

  private void UpdatePreview() {
    if (_selectedUpgrade == null) {
      ClearPreview();
      return;
    }

    _previewName.Text = _selectedUpgrade.Name;
    _previewName.Modulate = Upgrade.LevelColors[_selectedUpgrade.Level];
    _previewCost.Text = $"Cost: {_selectedCost:F0}s Time Bond";
    _previewDescription.Text = _selectedUpgrade.Description;
  }

  private void ClearPreview() {
    _previewName.Text = "";
    _previewCost.Text = "";
    _previewDescription.Text = "";
    _selectedUpgrade = null;
  }

  private void OnPurchasePressed() {
    var gm = GameManager.Instance;
    var currentlyOwned = gm.GetCurrentAndPendingUpgrades();
    if (_selectedUpgrade == null ||
      _purchasesRemaining <= 0 ||
      currentlyOwned.Contains(_selectedUpgrade)) return;

    gm.AddUpgrade(_selectedUpgrade);
    gm.AddPendingTimeBond(_selectedCost);
    GD.Print($"Purchased '{_selectedUpgrade.Name}' for {_selectedCost} time bond.");

    EmitSignal(SignalName.PurchaseMade, _selectedUpgrade, _selectedCost);
    --_purchasesRemaining;

    // 刷新菜单，而不是直接关闭
    _currentInventory.RemoveAll(item => item.upgrade == _selectedUpgrade);
    Popup(_currentInventory, _purchasesRemaining);
  }

  private void OnCancelPressed() {
    CloseMenu();
  }

  public void CloseMenu() {
    Visible = false;
    GetTree().Paused = false;
  }
}
