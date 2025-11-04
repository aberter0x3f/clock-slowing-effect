using System.Collections.Generic;
using Godot;

[GlobalClass]
public partial class MapGenerator : Node {
  [ExportGroup("Map Configuration")]
  [Export]
  public int MapWidth { get; set; } = 40;
  [Export]
  public int MapHeight { get; set; } = 30;
  [Export]
  public int TileSize { get; set; } = 32;
  [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
  public float ObstacleProbability { get; set; } = 0.02f;

  [ExportGroup("Scene References")]
  [Export]
  public PackedScene ObstacleScene { get; set; } // 一个包含 StaticBody2D 和 3D Cube 的场景

  [ExportGroup("Floor")]
  [Export]
  public PackedScene FloorTileScene1 { get; set; }
  [Export]
  public PackedScene FloorTileScene2 { get; set; }

  private int[,] _grid;
  private List<Vector2I> _walkableTiles = new();

  public List<Vector2I> WalkableTiles => _walkableTiles;

  /// <summary>
  /// 生成地图并返回玩家的安全出生点（世界坐标）．
  /// </summary>
  public Vector2 GenerateMap() {
    // 循环直到生成一个有效的地图
    while (true) {
      _grid = new int[MapWidth, MapHeight];
      _walkableTiles.Clear();

      GenerateInitialGrid();
      if (EnsureConnectivity()) {
        break; // 成功生成连通地图
      }
      GD.Print("Map generation failed connectivity check, retrying...");
    }

    InstantiateTiles();

    Vector2I playerSpawnCell = new Vector2I(MapWidth / 2, MapHeight / 2);
    // 如果中心点是障碍，向外寻找最近的可行走地块
    if (_grid[playerSpawnCell.X, playerSpawnCell.Y] == 1) {
      playerSpawnCell = FindNearestWalkable(playerSpawnCell);
    }
    return MapToWorld(playerSpawnCell);
  }

  private void GenerateInitialGrid() {
    for (int x = 0; x < MapWidth; x++) {
      for (int y = 0; y < MapHeight; y++) {
        // 边界必须是障碍
        if (x == 0 || x == MapWidth - 1 || y == 0 || y == MapHeight - 1) {
          _grid[x, y] = 1;
        } else {
          _grid[x, y] = GD.Randf() < ObstacleProbability ? 1 : 0;
        }
      }
    }
  }

  private bool EnsureConnectivity() {
    Vector2I? startNode = null;
    for (int x = 0; x < MapWidth; x++) {
      for (int y = 0; y < MapHeight; y++) {
        if (_grid[x, y] == 0) {
          startNode = new Vector2I(x, y);
          break;
        }
      }
      if (startNode.HasValue) break;
    }

    if (!startNode.HasValue) return false; // 没有可走的路

    var visited = new bool[MapWidth, MapHeight];
    var queue = new Queue<Vector2I>();
    queue.Enqueue(startNode.Value);
    visited[startNode.Value.X, startNode.Value.Y] = true;
    int accessibleTileCount = 0;

    while (queue.Count > 0) {
      var current = queue.Dequeue();
      accessibleTileCount++;

      Vector2I[] neighbors = {
        current + Vector2I.Up, current + Vector2I.Down,
        current + Vector2I.Left, current + Vector2I.Right
      };

      foreach (var neighbor in neighbors) {
        if (neighbor.X >= 0 && neighbor.X < MapWidth && neighbor.Y >= 0 && neighbor.Y < MapHeight &&
            !visited[neighbor.X, neighbor.Y] && _grid[neighbor.X, neighbor.Y] == 0) {
          visited[neighbor.X, neighbor.Y] = true;
          queue.Enqueue(neighbor);
        }
      }
    }

    int totalWalkable = 0;
    for (int x = 0; x < MapWidth; x++) {
      for (int y = 0; y < MapHeight; y++) {
        if (_grid[x, y] == 0) {
          totalWalkable++;
          if (!visited[x, y]) {
            _grid[x, y] = 1; // 将不可达的 '0' 变成障碍 '1'
          }
        }
      }
    }

    // 如果可访问的格子太少，也认为生成失败
    return accessibleTileCount > (MapWidth * MapHeight * (1 - ObstacleProbability) * 0.5);
  }

  private void InstantiateTiles() {
    for (int x = 0; x < MapWidth; x++) {
      for (int y = 0; y < MapHeight; y++) {
        // 生成地面
        PackedScene tileSceneToUse = ((x + y) % 2 == 0) ? FloorTileScene1 : FloorTileScene2;
        if (_grid[x, y] == 0) {
          var floorTile = tileSceneToUse.Instantiate<Node3D>();
          Vector2 worldPos2D = MapToWorld(new Vector2I(x, y));
          AddChild(floorTile);
          // 地面放在 Y=0 的高度
          floorTile.GlobalPosition = new Vector3(
            worldPos2D.X * GameConstants.WorldScaleFactor,
            0,
            worldPos2D.Y * GameConstants.WorldScaleFactor
          );
          _walkableTiles.Add(new Vector2I(x, y));
        } else {
          // -生成障碍物或添加到可行走列表
          var obstacle = ObstacleScene.Instantiate<Obstacle>();
          var node3d = obstacle.GetNode<Node3D>("Node3D");
          if (x == 0 || x == MapWidth - 1 || y == 0 || y == MapHeight - 1) {
            node3d.Visible = false;
          }
          obstacle.TileSize = TileSize;
          obstacle.Position = MapToWorld(new Vector2I(x, y));
          AddChild(obstacle);
        }
      }
    }
  }

  private Vector2I FindNearestWalkable(Vector2I origin) {
    var queue = new Queue<Vector2I>();
    queue.Enqueue(origin);
    var visited = new HashSet<Vector2I> { origin };

    while (queue.Count > 0) {
      var current = queue.Dequeue();
      if (_grid[current.X, current.Y] == 0) {
        return current;
      }

      Vector2I[] neighbors = {
        current + Vector2I.Up, current + Vector2I.Down,
        current + Vector2I.Left, current + Vector2I.Right
      };

      foreach (var neighbor in neighbors) {
        if (neighbor.X >= 0 && neighbor.X < MapWidth && neighbor.Y >= 0 && neighbor.Y < MapHeight && !visited.Contains(neighbor)) {
          visited.Add(neighbor);
          queue.Enqueue(neighbor);
        }
      }
    }
    return new Vector2I(MapWidth / 2, MapHeight / 2); // Fallback
  }

  public Vector2 MapToWorld(Vector2I mapCoords) {
    return (mapCoords - new Vector2I(MapWidth / 2, MapHeight / 2)) * TileSize;
  }

  /// <summary>
  /// 将世界坐标转换为地图网格坐标．
  /// </summary>
  public Vector2I WorldToMap(Vector2 worldCoords) {
    // 这是 MapToWorld 的逆运算
    return new Vector2I(
      Mathf.RoundToInt(worldCoords.X / TileSize + (float) MapWidth / 2),
      Mathf.RoundToInt(worldCoords.Y / TileSize + (float) MapHeight / 2)
    );
  }

  /// <summary>
  /// 检查给定的网格坐标是否在地图边界内并且是可通行的．
  /// </summary>
  public bool IsWalkable(Vector2I mapCoords) {
    // 检查边界
    if (mapCoords.X < 0 || mapCoords.X >= MapWidth || mapCoords.Y < 0 || mapCoords.Y >= MapHeight) {
      return false;
    }
    // 检查地块类型 (0 = 可通行, 1 = 障碍)
    return _grid[mapCoords.X, mapCoords.Y] == 0;
  }
}
