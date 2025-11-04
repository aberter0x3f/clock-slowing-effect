using System.Collections.Generic;
using System.Linq;
using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseMazeState : BasePhaseState {
  public PhaseMaze.AttackState CurrentState;
  public float SpawnTimer;
  public int RowCounter;
  public ulong RngState;
}

public partial class PhaseMaze : BasePhase {
  public enum AttackState {
    MovingToPosition,
    Spawning
  }

  private AttackState _currentState;
  private float _spawnTimer;
  private int _rowCounter;
  private float _spawnY; // 固定的生成 Y 坐标

  private MapGenerator _mapGenerator;
  private XorShift64Star _rng;

  // --- 六边形网格属性 ---
  private float _hexSize;
  private float _hexWidth;
  private float _hexHeight;
  private float _rowOffsetY;
  private float _bulletSpacing;
  private int _hexesPerRow;

  [ExportGroup("Scene References")]
  [Export]
  public PackedScene LowSpeedPhaseBulletScene { get; set; } // 红色
  [Export]
  public PackedScene HighSpeedPhaseBulletScene { get; set; } // 蓝色
  [Export]
  public PackedScene GrazeSlowdownBulletScene { get; set; } // 黄色
  [Export]
  public PackedScene NormalBulletScene { get; set; } // 绿色

  [ExportGroup("Movement")]
  [Export]
  public float MoveSpeed { get; set; } = 500f;

  [ExportGroup("Pattern Configuration")]
  [Export(PropertyHint.Range, "10, 1000, 1")]
  public float BaseHexSize { get; set; } = 80f;
  [Export(PropertyHint.Range, "10, 1000, 1")]
  public float MinHexSize { get; set; } = 60f;
  [Export(PropertyHint.Range, "1, 20, 1")]
  public float BulletSpacingOnEdge { get; set; } = 10f;
  [Export(PropertyHint.Range, "50, 1000, 10")]
  public float BulletDownwardSpeed { get; set; } = 100f;

  [ExportGroup("")]
  [Export]
  public float TimeScaleSensitivity { get; set; } = 0f;

  public override void StartPhase(Boss parent) {
    base.StartPhase(parent);
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr("PhaseMaze: MapGenerator not found. Phase cannot start.");
      EndPhase();
      return;
    }

    _rng = new XorShift64Star(((ulong) GD.Randi() << 32) | (ulong) GD.Randi());

    // 根据难度调整属性
    float rank = GameManager.Instance.EnemyRank;
    _hexSize = Mathf.Max(MinHexSize, BaseHexSize * 12f / (rank + 7f));
    BulletDownwardSpeed *= (rank + 5f) / 10f;

    // 预计算点朝上 (point-top) 六边形尺寸
    _hexWidth = Mathf.Sqrt(3) * _hexSize;
    _hexHeight = 2 * _hexSize;
    _rowOffsetY = _hexHeight * 0.75f;
    _bulletSpacing = BulletSpacingOnEdge;

    float mapWidth = _mapGenerator.MapWidth * _mapGenerator.TileSize;
    _hexesPerRow = Mathf.CeilToInt(mapWidth / _hexWidth);

    _currentState = AttackState.MovingToPosition;
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;

    switch (_currentState) {
      case AttackState.MovingToPosition:
        var targetPos = new Vector2(0, -_mapGenerator.MapHeight * _mapGenerator.TileSize / 2f);
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(targetPos, MoveSpeed * scaledDelta);
        if (ParentBoss.GlobalPosition.IsEqualApprox(targetPos)) {
          _currentState = AttackState.Spawning;
          _spawnY = targetPos.Y; // 设置固定的生成 Y 坐标
          _spawnTimer = 0; // 立即生成第一行
        }
        break;

      case AttackState.Spawning:
        _spawnTimer -= scaledDelta;
        if (_spawnTimer <= 0) {
          SpawnHexRow();
          ++_rowCounter;
          // 下一行的生成时间，是子弹移动一个行间距所需的时间，以确保紧密拼接
          _spawnTimer = _rowOffsetY / BulletDownwardSpeed;
        }
        break;
    }
  }

  private void SpawnHexRow() {
    var verticalEdges = new List<(Vector2, Vector2)>();
    var slantedEdges = new List<(Vector2, Vector2)>();

    // 根据行号的奇偶性，计算 X 轴的起始偏移
    float startX = -(_hexesPerRow / 2f) * _hexWidth;
    if (_rowCounter % 2 != 0) {
      startX += _hexWidth / 2f;
    }

    // 遍历一行中的所有六边形，使用构造性算法生成无重复的边
    for (int q = 0; q < _hexesPerRow; ++q) {
      float xPos = startX + q * _hexWidth;
      var hexCenter = new Vector2(xPos, _spawnY);
      var v = GetPointTopHexagonVertices(hexCenter, _hexSize);

      // --- 主生成规则 ---
      // 每个六边形负责生成它的右竖直边和两条上斜边
      verticalEdges.Add((v[5], v[4]));
      slantedEdges.Add((v[0], v[1]));
      slantedEdges.Add((v[5], v[0]));

      // 仅为每行的第一个六边形生成左侧的竖直边来封口
      if (q == 0) {
        verticalEdges.Add((v[1], v[2])); // 左竖直边
      }
    }

    // 分配竖直边的子弹类型
    verticalEdges.Shuffle(_rng);
    int lowSpeedCount = verticalEdges.Count / 2;
    for (int i = 0; i < verticalEdges.Count; ++i) {
      var scene = (i < lowSpeedCount) ? LowSpeedPhaseBulletScene : HighSpeedPhaseBulletScene;
      SpawnBulletsAlongLine(verticalEdges[i].Item1, verticalEdges[i].Item2, scene);
    }

    // 分配倾斜边的子弹类型
    slantedEdges = slantedEdges.OrderBy(e => e.Item1.X).ThenBy(e => e.Item1.Y).ToList();
    var eligibleForGraze = Enumerable.Range(1, slantedEdges.Count - 2).ToList();
    eligibleForGraze.Shuffle(_rng);
    int grazeCount = Mathf.CeilToInt(slantedEdges.Count / 6f);
    var grazeIndices = new HashSet<int>(eligibleForGraze.Take(grazeCount));

    for (int i = 0; i < slantedEdges.Count; ++i) {
      var scene = grazeIndices.Contains(i) ? GrazeSlowdownBulletScene : NormalBulletScene;
      SpawnBulletsAlongLine(slantedEdges[i].Item1, slantedEdges[i].Item2, scene);
    }
  }

  private void SpawnBulletsAlongLine(Vector2 start, Vector2 end, PackedScene bulletScene) {
    if (bulletScene == null) return;

    float distance = start.DistanceTo(end);
    int bulletCount = Mathf.Max(1, Mathf.FloorToInt(distance / _bulletSpacing));

    for (int i = 1; i <= bulletCount; ++i) {
      float t = (float) i / bulletCount;
      Vector2 pos = start.Lerp(end, t);

      var bullet = bulletScene.Instantiate<PhaseMazeBullet>();
      bullet.RawPosition = new Vector3(pos.X, pos.Y, 0);
      bullet.VelocityY = BulletDownwardSpeed;
      bullet.TimeScaleSensitivity = TimeScaleSensitivity;
      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  /// <summary>
  /// 计算一个点朝上的正六边形的 6 个顶点．
  /// 顶点按顺时针方向从最顶部的点 (v0) 开始排序．
  /// </summary>
  private Vector2[] GetPointTopHexagonVertices(Vector2 center, float size) {
    var vertices = new Vector2[6];
    for (int i = 0; i < 6; i++) {
      float angle = Mathf.DegToRad(60 * i + 90); // +90 度使一个顶点正对下方
      vertices[i] = center + new Vector2(size * Mathf.Cos(angle), size * Mathf.Sin(angle));
    }
    return vertices;
  }

  public override RewindState CaptureInternalState() {
    return new PhaseMazeState {
      CurrentState = this._currentState,
      SpawnTimer = this._spawnTimer,
      RowCounter = this._rowCounter,
      RngState = _rng.State,
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseMazeState pms) return;
    this._currentState = pms.CurrentState;
    this._spawnTimer = pms.SpawnTimer;
    this._rowCounter = pms.RowCounter;
    this._rng.State = pms.RngState;
  }
}
