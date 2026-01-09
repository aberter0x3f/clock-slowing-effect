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
  public enum AttackState { MovingToPosition, Spawning }

  private AttackState _currentState;
  private float _spawnTimer;
  private int _rowCounter;
  private float _spawnZ;

  private MapGenerator _mapGenerator;
  private readonly RandomNumberGenerator _rng = new();

  private float _hexSize;
  private float _hexWidth;
  private float _hexHeight;
  private float _rowOffsetZ;
  private int _hexesPerRow;

  [ExportGroup("Scene References")]
  [Export] public PackedScene LowSpeedPhaseBulletScene { get; set; }
  [Export] public PackedScene HighSpeedPhaseBulletScene { get; set; }
  [Export] public PackedScene GrazeSlowdownBulletScene { get; set; }
  [Export] public PackedScene NormalBulletScene { get; set; }

  [ExportGroup("Movement")]
  [Export] public float MoveSpeed { get; set; } = 5.0f; // 500 * 0.01

  [ExportGroup("Pattern Configuration")]
  [Export] public float BaseHexSize { get; set; } = 0.8f; // 80 * 0.01
  [Export] public float MinHexSize { get; set; } = 0.6f;
  [Export] public float BulletSpacingOnEdge { get; set; } = 0.1f;
  [Export] public float BulletDownwardSpeed { get; set; } = 1.0f; // 100 * 0.01

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _mapGenerator = GetTree().Root.GetNode<MapGenerator>("GameRoot/MapGenerator");
    _rng.Seed = ((ulong) GD.Randi() << 32) | (ulong) GD.Randi();

    float rank = GameManager.Instance.EnemyRank;
    _hexSize = Mathf.Max(MinHexSize, BaseHexSize * 30f / (rank + 25f));
    BulletDownwardSpeed *= (rank + 15f) / 20f;

    _hexWidth = Mathf.Sqrt(3) * _hexSize;
    _hexHeight = 2 * _hexSize;
    _rowOffsetZ = _hexHeight * 0.75f;

    float mapWidth = _mapGenerator.MapWidth * _mapGenerator.TileSize;
    _hexesPerRow = Mathf.CeilToInt(mapWidth / _hexWidth);

    _currentState = AttackState.MovingToPosition;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    switch (_currentState) {
      case AttackState.MovingToPosition:
        // 移动到 3D 顶部 (2D Y- 为 3D Z+)
        float halfH = (_mapGenerator.MapHeight / 2f - 1) * _mapGenerator.TileSize;
        var targetPos = new Vector3(0, 0, -halfH);
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(targetPos, MoveSpeed * scaledDelta);

        if (ParentBoss.GlobalPosition.IsEqualApprox(targetPos)) {
          _currentState = AttackState.Spawning;
          _spawnZ = targetPos.Z;
          _spawnTimer = 0;
        }
        break;

      case AttackState.Spawning:
        _spawnTimer -= scaledDelta;
        if (_spawnTimer <= 0) {
          SpawnHexRow();
          ++_rowCounter;
          _spawnTimer = _rowOffsetZ / BulletDownwardSpeed;
        }
        break;
    }
  }

  private void SpawnHexRow() {
    SoundManager.Instance.Play(SoundEffect.FireBig);
    var verticalEdges = new List<(Vector2, Vector2)>();
    var slantedEdges = new List<(Vector2, Vector2)>();

    float startX = -(_hexesPerRow / 2f) * _hexWidth;
    if (_rowCounter % 2 != 0) startX += _hexWidth / 2f;

    for (int q = 0; q < _hexesPerRow; ++q) {
      var hexCenter = new Vector2(startX + q * _hexWidth, 0);
      var v = GetPointTopHexagonVertices(hexCenter, _hexSize);
      verticalEdges.Add((v[5], v[4]));
      slantedEdges.Add((v[0], v[1]));
      slantedEdges.Add((v[5], v[0]));
      if (q == 0) verticalEdges.Add((v[1], v[2]));
    }

    // 分配边缘类型并生成
    verticalEdges.Shuffle(_rng);
    int lowCount = verticalEdges.Count / 2;
    for (int i = 0; i < verticalEdges.Count; ++i) {
      var scn = (i < lowCount) ? LowSpeedPhaseBulletScene : HighSpeedPhaseBulletScene;
      var type = (i < lowCount) ? PhaseMazeBullet.MazeBulletType.LowSpeedPhase : PhaseMazeBullet.MazeBulletType.HighSpeedPhase;
      SpawnAlongLine(verticalEdges[i].Item1, verticalEdges[i].Item2, scn, type);
    }

    int grazeCount = Mathf.CeilToInt(slantedEdges.Count / 6f);
    var grazeIdx = new HashSet<int>(Enumerable.Range(0, slantedEdges.Count).OrderBy(_ => _rng.Randf()).Take(grazeCount));
    for (int i = 0; i < slantedEdges.Count; ++i) {
      var scn = grazeIdx.Contains(i) ? GrazeSlowdownBulletScene : NormalBulletScene;
      var type = grazeIdx.Contains(i) ? PhaseMazeBullet.MazeBulletType.Graze : PhaseMazeBullet.MazeBulletType.Normal;
      SpawnAlongLine(slantedEdges[i].Item1, slantedEdges[i].Item2, scn, type);
    }
  }

  private void SpawnAlongLine(Vector2 start, Vector2 end, PackedScene scene, PhaseMazeBullet.MazeBulletType type) {
    if (scene == null) return;
    float dist = start.DistanceTo(end);
    int count = Mathf.Max(1, Mathf.FloorToInt(dist / BulletSpacingOnEdge));

    for (int i = 1; i <= count; ++i) {
      Vector2 p = start.Lerp(end, (float) i / count);
      var b = scene.Instantiate<PhaseMazeBullet>();
      b.Position = new Vector3(p.X, 0, _spawnZ + p.Y);
      b.Type = type;
      b.VelocityZ = BulletDownwardSpeed;
      GameRootProvider.CurrentGameRoot.AddChild(b);
    }
  }

  private Vector2[] GetPointTopHexagonVertices(Vector2 center, float size) {
    var v = new Vector2[6];
    for (int i = 0; i < 6; ++i) {
      float ang = Mathf.DegToRad(60 * i + 90);
      v[i] = center + new Vector2(size * Mathf.Cos(ang), size * Mathf.Sin(ang));
    }
    return v;
  }

  public override RewindState CaptureInternalState() => new PhaseMazeState {
    CurrentState = _currentState,
    SpawnTimer = _spawnTimer,
    RowCounter = _rowCounter,
    RngState = _rng.State
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseMazeState p) return;
    _currentState = p.CurrentState; _spawnTimer = p.SpawnTimer; _rowCounter = p.RowCounter; _rng.State = p.RngState;
  }
}
