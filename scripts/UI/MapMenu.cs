using System.Collections.Generic;
using System.Linq;
using Godot;

[GlobalClass]
public partial class MapMenu : Control {
  [Export(PropertyHint.File, "*.tscn")]
  public string CombatScenePath { get; set; }

  [Export]
  public PackedScene MapMenuNodeScene { get; set; } // 一个代表地图节点的场景

  [Export]
  public float HexagonSize { get; set; } = 50f; // 六边形的大小，用于计算位置

  private Label _clearedLevelsLabel;
  private Control _mapContainer;
  private List<TextureButton> _accessibleNodeButtons = new();
  private int _selectedIndex = 0;

  public override void _Ready() {
    _clearedLevelsLabel = GetNode<Label>("HBoxContainer/ClearedLevels/Count");
    _mapContainer = GetNode<Control>("MapContainer");

    if (GameManager.Instance?.GameMap == null) {
      GD.PrintErr("MapMenu: GameManager or GameMap not initialized!");
      // 可以在这里添加返回主菜单的逻辑
      return;
    }

    _clearedLevelsLabel.Text = GameManager.Instance.LevelsCleared.ToString();
    GenerateMapView();
    UpdateSelection();
  }

  private void GenerateMapView() {
    foreach (Node child in _mapContainer.GetChildren()) {
      child.QueueFree();
    }
    _accessibleNodeButtons.Clear();

    var map = GameManager.Instance.GameMap;
    foreach (var (pos, nodeData) in map.Nodes) {
      var mapNode = MapMenuNodeScene.Instantiate<TextureButton>();
      Vector2 nodeSize = mapNode.Size;
      mapNode.Position = AxialToPixel(pos) - nodeSize / 2;
      _mapContainer.AddChild(mapNode);
      var label = mapNode.GetNode<Label>("CenterContainer/Label");

      label.Text = "C";

      // 根据节点状态设置图标外观
      if (nodeData.IsCleared) {
        mapNode.SelfModulate = new Color(0f, 0.5f, 0f); // 已通关
      } else if (nodeData.IsAccessible) {
        mapNode.SelfModulate = new Color(0.5f, 0.5f, 0f); // 可选
        _accessibleNodeButtons.Add(mapNode);
        mapNode.Pressed += () => OnLevelSelected(nodeData.Position);
      } else {
        mapNode.SelfModulate = new Color(0.2f, 0.2f, 0.2f); // 锁定
      }
    }

    // 对可选节点进行排序，以便导航
    _accessibleNodeButtons = _accessibleNodeButtons.OrderBy(b => b.GlobalPosition.X).ThenBy(b => b.GlobalPosition.Y).ToList();
  }

  // 将轴向坐标转换为屏幕像素坐标
  private Vector2 AxialToPixel(Vector2I axial) {
    float x = HexagonSize * (Mathf.Sqrt(3) * axial.X + Mathf.Sqrt(3) / 2 * axial.Y);
    float y = HexagonSize * 3.0f / 2 * axial.Y;
    return new Vector2(x, y);
  }

  public override void _Input(InputEvent @event) {
    if (_accessibleNodeButtons.Count == 0) return;

    if (@event.IsActionPressed("ui_right") || @event.IsActionPressed("ui_down")) {
      _selectedIndex = (_selectedIndex + 1) % _accessibleNodeButtons.Count;
      UpdateSelection();
      GetViewport().SetInputAsHandled();
    } else if (@event.IsActionPressed("ui_left") || @event.IsActionPressed("ui_up")) {
      _selectedIndex = (_selectedIndex - 1 + _accessibleNodeButtons.Count) % _accessibleNodeButtons.Count;
      UpdateSelection();
      GetViewport().SetInputAsHandled();
    } else if (@event.IsActionPressed("ui_accept")) {
      _accessibleNodeButtons[_selectedIndex].EmitSignal(Button.SignalName.Pressed);
      GetViewport()?.SetInputAsHandled();
    }
  }

  private void UpdateSelection() {
    if (_accessibleNodeButtons.Count == 0) return;
    for (int i = 0; i < _accessibleNodeButtons.Count; i++) {
      if (i == _selectedIndex) {
        _accessibleNodeButtons[i].GrabFocus();
      }
    }
  }

  private void OnLevelSelected(Vector2I mapPosition) {
    GameManager.Instance.CurrentMapPosition = mapPosition;
    GetTree().ChangeSceneToFile(CombatScenePath);
  }
}
