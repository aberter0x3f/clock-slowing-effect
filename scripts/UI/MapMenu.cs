using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

namespace UI;

[GlobalClass]
public partial class MapMenu : Control, IMenuPanel {
  [Export(PropertyHint.File, "*.tscn")]
  public string CombatScenePath { get; set; }

  [Export]
  public PackedScene MapMenuNodeScene { get; set; } // 一个代表地图节点的场景

  [Export]
  public float HexagonSize { get; set; } = 50f; // 六边形的大小，用于计算位置

  private Label _clearedLevelsLabel;
  private Label _levelScoreLabel;
  private Control _mapContainer;

  private readonly Dictionary<Vector2I, TextureButton> _nodeButtons = new();
  private Vector2I _selectedPosition;

  public override void _Ready() {
    _clearedLevelsLabel = GetNode<Label>("HBoxContainer/ClearedLevels/ValueLabel");
    _levelScoreLabel = GetNode<Label>("HBoxContainer/LevelScore/ValueLabel");
    _mapContainer = GetNode<Control>("MapContainer");

    if (GameManager.Instance?.GameMap == null) {
      GD.PrintErr("MapMenu: GameManager or GameMap not initialized!");
      return;
    }

    _clearedLevelsLabel.Text = GameManager.Instance.LevelsCleared.ToString();
    GenerateMapView();
  }

  private void GenerateMapView() {
    foreach (Node child in _mapContainer.GetChildren()) {
      child.QueueFree();
    }
    _nodeButtons.Clear();

    var gm = GameManager.Instance;
    var map = gm.GameMap;
    var accessibleNodePositions = new HashSet<Vector2I>(gm.GetAccessibleNodes());

    foreach (var (pos, nodeData) in map.Nodes) {
      var mapNode = MapMenuNodeScene.Instantiate<TextureButton>();
      Vector2 nodeSize = mapNode.Size;
      mapNode.Position = AxialToPixel(pos) - nodeSize / 2;
      _mapContainer.AddChild(mapNode);
      var label = mapNode.GetNode<Label>("CenterContainer/Label");

      label.Text = "C";

      // 根据节点状态设置图标外观
      if (gm.PlayerMapPosition.HasValue && pos == gm.PlayerMapPosition.Value) {
        mapNode.SelfModulate = new Color(0f, 0f, 0.5f); // 玩家当前所在的节点
      } else if (nodeData.Score != HexMap.ClearScore.NotCleared) {
        mapNode.SelfModulate = new Color(0f, 0.5f, 0f); // 已通关的节点
      } else if (accessibleNodePositions.Contains(pos)) {
        mapNode.SelfModulate = new Color(0.5f, 0.5f, 0f); // 可选的节点
      } else {
        mapNode.SelfModulate = new Color(0.2f, 0.2f, 0.2f); // 锁定的节点
      }

      // 为所有节点连接信号，并在回调中检查可访问性
      mapNode.Pressed += () => OnLevelSelected(nodeData.Position);
      _nodeButtons.Add(pos, mapNode);
    }

    // -初始化选中位置
    // 优先选择玩家当前位置，其次是第一个可访问节点，最后是地图上的任意一个节点作为备用
    var accessibleNodes = gm.GetAccessibleNodes();
    if (gm.PlayerMapPosition.HasValue && map.Nodes.ContainsKey(gm.PlayerMapPosition.Value)) {
      _selectedPosition = gm.PlayerMapPosition.Value;
    } else if (accessibleNodes.Any()) {
      _selectedPosition = accessibleNodes.First();
    } else if (map.Nodes.Any()) {
      _selectedPosition = map.Nodes.Keys.First();
    }
  }

  private Vector2 AxialToPixel(Vector2I axial) {
    float x = HexagonSize * (Mathf.Sqrt(3) * axial.X + Mathf.Sqrt(3) / 2 * axial.Y);
    float y = HexagonSize * 3.0f / 2 * axial.Y;
    return new Vector2(x, y);
  }

  public override void _Input(InputEvent @event) {
    if (!Visible) return;

    if (_nodeButtons.Count == 0) return;

    Vector2I direction = Vector2I.Zero;
    if (@event.IsActionPressed("ui_right")) direction = Vector2I.Right;
    else if (@event.IsActionPressed("ui_left")) direction = Vector2I.Left;
    else if (@event.IsActionPressed("ui_down")) direction = Vector2I.Down;
    else if (@event.IsActionPressed("ui_up")) direction = Vector2I.Up;

    if (direction != Vector2I.Zero) {
      MoveSelection(direction);
      GetViewport().SetInputAsHandled();
    } else if (@event.IsActionPressed("ui_accept")) {
      OnLevelSelected(_selectedPosition);
      GetViewport()?.SetInputAsHandled();
    }
  }

  private void MoveSelection(Vector2I direction) {
    Vector2I nextPos = _selectedPosition + direction;

    // 尝试直接移动到相邻节点
    if (_nodeButtons.ContainsKey(nextPos)) {
      _selectedPosition = nextPos;
      UpdateSelection();
      return;
    }

    // 如果直接移动失败，则执行循环逻辑
    if (direction.X != 0) { // 水平移动
      var rowNodes = _nodeButtons.Keys
        .Where(p => p.Y == _selectedPosition.Y)
        .OrderBy(p => p.X)
        .ToList();
      if (rowNodes.Count > 1) {
        _selectedPosition = (direction.X > 0) ? rowNodes.First() : rowNodes.Last();
      }
    } else if (direction.Y != 0) { // 垂直移动
      var colNodes = _nodeButtons.Keys
        .Where(p => p.X == _selectedPosition.X)
        .OrderBy(p => p.Y)
        .ToList();
      if (colNodes.Count > 1) {
        _selectedPosition = (direction.Y > 0) ? colNodes.First() : colNodes.Last();
      }
    }
    UpdateSelection();
  }

  private void UpdateSelection() {
    if (!_nodeButtons.ContainsKey(_selectedPosition)) return;

    _nodeButtons[_selectedPosition].GrabFocus();
    switch (GameManager.Instance.GameMap.Nodes[_selectedPosition].Score) {
      case HexMap.ClearScore.NotCleared:
        _levelScoreLabel.Text = "∅";
        break;
      case HexMap.ClearScore.StandardClear:
        _levelScoreLabel.Text = "0";
        break;
      case HexMap.ClearScore.NoMiss:
        _levelScoreLabel.Text = "ℕ";
        break;
      case HexMap.ClearScore.NoMissNoSkill:
        _levelScoreLabel.Text = "ℕ²";
        break;
      case HexMap.ClearScore.Perfect:
        _levelScoreLabel.Text = "ℕ³";
        break;
      default:
        throw new ArgumentOutOfRangeException();
    }
  }

  private void OnLevelSelected(Vector2I mapPosition) {
    var gm = GameManager.Instance;
    var accessibleNodes = new HashSet<Vector2I>(gm.GetAccessibleNodes());

    // 只有当选中的节点是可访问的时，才加载战斗场景
    if (accessibleNodes.Contains(mapPosition)) {
      gm.SelectedMapPosition = mapPosition;
      GetTree().ChangeSceneToFile(CombatScenePath);
    } else {
      // 可选：在这里添加一个音效或视觉提示，表示该节点不可进入
      GD.Print($"Node {mapPosition} is not accessible.");
    }
  }

  /// <summary>
  /// 实现 IMenuPanel 接口的方法．
  /// </summary>
  public void GrabInitialFocus() {
    // 当此面板变为可见时，确保正确的节点获得焦点．
    UpdateSelection();
  }
}
