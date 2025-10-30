using System.Collections.Generic;
using Godot;
using UI;

public partial class ShopDevice : Area2D, IInteractable {
  private Label3D _label;
  private Node3D _visualizer;
  private ShopMenu _shopMenuInstance;
  private int _purchasesMade = 0;
  private List<(Upgrade, float)> _originalInventory;

  public PackedScene ShopMenuScene { get; set; }
  public List<(Upgrade, float)> Inventory { get; set; }
  public int MaxPurchases { get; set; } = 1;

  public override void _Ready() {
    _visualizer = GetNode<Node3D>("Visualizer");
    _label = _visualizer.GetNode<Label3D>("Label3D");

    _visualizer.GlobalPosition = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY + 0.2f,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );
    // 保存一份原始库存，用于重置
    _originalInventory = new List<(Upgrade, float)>(Inventory);
  }

  public void Interact() {
    if (ShopMenuScene == null) {
      GD.PrintErr("ShopDevice: ShopMenuScene is not set!");
      return;
    }

    if (!IsInstanceValid(_shopMenuInstance)) {
      _shopMenuInstance = ShopMenuScene.Instantiate<ShopMenu>();
      GetTree().Root.AddChild(_shopMenuInstance);
      _shopMenuInstance.PurchaseMade += OnPurchaseMade;
    }
    // 总是打开菜单，并将剩余购买次数传递进去
    _shopMenuInstance.Popup(Inventory, MaxPurchases - _purchasesMade);
  }

  private void OnPurchaseMade(Upgrade purchasedUpgrade, float cost) {
    _purchasesMade++;
    // 从可购买列表中移除已购买的物品
    Inventory.RemoveAll(item => item.Item1 == purchasedUpgrade);
  }

  public void SetHighlight(bool highlighted) {
    if (_label == null) return;
    // 即使购买次数用完，也可以高亮，因为仍然可以交互打开菜单
    _label.Modulate = highlighted ? new Color(1.0f, 1.0f, 0.5f) : Colors.White;
  }

  /// <summary>
  /// 重置商店状态．
  /// </summary>
  public void Reset() {
    _purchasesMade = 0;
    Inventory = new List<(Upgrade, float)>(_originalInventory);
    if (IsInstanceValid(_shopMenuInstance)) {
      _shopMenuInstance.CloseMenu();
    }
    GD.Print("Shop device has been reset.");
  }
}
