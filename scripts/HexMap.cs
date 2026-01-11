using System.Collections.Generic;
using System.Linq;
using Godot;

public class HexMap {
  // 用于表示关卡通关分数的枚举
  public enum ClearScore {
    NotCleared = -1, // 尚未通关
    StandardClear = 0, // 普通通关
    NoMiss = 1, // ℕ
    NoMissNoSkill = 2, // ℕ²
    Perfect = 3 // ℕ³
  }

  public enum NodeType {
    Combat,
    Shop,
    Transmuter,
    Event,
    Boss
  }

  public class MapNode {
    public Vector2I Position { get; } // Axial coordinates (q,r)
    public NodeType Type { get; set; } = NodeType.Combat; // 默认为战斗
    public ClearScore Score { get; set; } = ClearScore.NotCleared;

    public MapNode(Vector2I position) {
      Position = position;
    }
  }

  public Dictionary<Vector2I, MapNode> Nodes { get; } = new();
  public Vector2I StartPosition { get; private set; }
  public Vector2I TargetPosition { get; private set; }
  public int Plane { get; set; } = 1;

  /*
   * 方向向量
   *  2 1
   * 3 X 0
   *  4 5
   */
  public static readonly Vector2I[] Dirs = {
    new(1, 0),
    new(1, -1),
    new(0, -1),
    new(-1, 0),
    new(-1, 1),
    new(0, 1)
  };

  public HexMap() {
    GenerateMap();
  }

  public void GenerateMap() {
    Nodes.Clear(); // 清理旧节点，以便重新生成

    int[] rows = { 3, 4, 5, 4, 3 };
    int totalColumns = rows.Length;
    int centerColumn = totalColumns / 2;

    for (int q = -centerColumn; q <= centerColumn; ++q) {
      int colIndex = q + centerColumn;
      int rowCount = rows[colIndex];
      int startR = (q >= 0 ? 0 : -q) - centerColumn;
      for (int i = 0; i < rowCount; ++i) {
        int r = startR + i;
        var pos = new Vector2I(q, r);
        Nodes.Add(pos, new MapNode(pos));
      }
    }

    // 设置起点和终点
    StartPosition = new Vector2I(-centerColumn, 0);
    TargetPosition = new Vector2I(centerColumn, 0);

    // foreach (var node in Nodes.Values) {
    //   node.Type = NodeType.Boss;
    // }

    // return;

    Nodes[StartPosition].Type = NodeType.Combat;
    Nodes[TargetPosition].Type = NodeType.Boss;

    // 随机分配特殊房间
    var potentialSpecialNodes = Nodes.Values
      .Where(n => n.Position != StartPosition && n.Position != TargetPosition)
      .ToList();

    const int SHOP_COUNT = 2, TRANSMUTER_COUNT = 2;

    for (int i = 0; i < SHOP_COUNT; ++i) {
      if (potentialSpecialNodes.Count == 0) break;
      int shopIndex = GD.RandRange(0, potentialSpecialNodes.Count - 1);
      potentialSpecialNodes[shopIndex].Type = NodeType.Shop;
      potentialSpecialNodes.RemoveAt(shopIndex);
    }

    for (int i = 0; i < TRANSMUTER_COUNT; ++i) {
      if (potentialSpecialNodes.Count == 0) break;
      int transmuterIndex = GD.RandRange(0, potentialSpecialNodes.Count - 1);
      potentialSpecialNodes[transmuterIndex].Type = NodeType.Transmuter;
      potentialSpecialNodes.RemoveAt(transmuterIndex);
    }
    // 剩余的节点一半战斗，一半事件
    int eventCount = (potentialSpecialNodes.Count + 1) / 2;
    potentialSpecialNodes.Shuffle(new RandomNumberGenerator());
    for (int i = 0; i < eventCount; ++i) {
      potentialSpecialNodes[i].Type = NodeType.Event;
    }
  }

  /// <summary>
  /// 重置地图并进入下一个位面．
  /// </summary>
  public void ResetForNewPlane() {
    ++Plane;
    GenerateMap();
  }

  public MapNode GetNode(Vector2I position) {
    Nodes.TryGetValue(position, out var node);
    return node;
  }
}
