using System.Collections.Generic;
using Godot;

public class HexMap {
  public class MapNode {
    public Vector2I Position { get; } // Axial coordinates (q,r)
    public bool IsCleared { get; set; } = false;
    public bool IsAccessible { get; set; } = false;
    public bool IsStart { get; set; } = false;

    public MapNode(Vector2I position) {
      Position = position;
    }
  }

  public Dictionary<Vector2I, MapNode> Nodes { get; } = new();
  public Vector2I StartPosition { get; private set; }
  public Vector2I TargetPosition { get; private set; }

  // 方向向量
  /*
   *  2 1
   * 3 X 0
   *  4 5
   */
  private static readonly Vector2I[] Dirs = {
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

  private void GenerateMap() {
    int[] rows = { 3, 4, 5, 4, 3 };
    int totalColumns = rows.Length;
    int centerColumn = totalColumns / 2;

    for (int q = -centerColumn; q <= centerColumn; q++) {
      int colIndex = q + centerColumn;
      int rowCount = rows[colIndex];
      int startR = (q >= 0 ? 0 : -q) - centerColumn;
      for (int i = 0; i < rowCount; i++) {
        int r = startR + i;
        var pos = new Vector2I(q, r);
        Nodes.Add(pos, new MapNode(pos));
      }
    }

    // 设置起点和终点
    StartPosition = new Vector2I(-centerColumn, 0);
    TargetPosition = new Vector2I(centerColumn, 0);

    Nodes[StartPosition].IsStart = true;
    Nodes[StartPosition].IsAccessible = true;
  }

  public MapNode GetNode(Vector2I position) {
    Nodes.TryGetValue(position, out var node);
    return node;
  }

  /// <summary>
  /// 在完成一个节点后，更新可访问的节点．
  /// </summary>
  public void UpdateAccessibleNodes(Vector2I clearedNodePosition) {
    var node = GetNode(clearedNodePosition);
    if (node == null || !node.IsCleared) return;

    // 玩家只能向右走（q 坐标增加）
    // 方向 0: (1, 0) -> 右
    // 方向 1: (1, -1) -> 右下
    // 方向 5: (0, 1) -> 右上
    Vector2I[] rightNeighbors = {
      clearedNodePosition + Dirs[0], // 正右
      clearedNodePosition + Dirs[1], // 右下
      clearedNodePosition + Dirs[5], // 右上
    };

    foreach (var neighborPos in rightNeighbors) {
      if (Nodes.ContainsKey(neighborPos)) {
        Nodes[neighborPos].IsAccessible = true;
      }
    }
  }
}
