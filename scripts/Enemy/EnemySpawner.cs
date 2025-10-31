using System.Collections.Generic;
using System.Linq;
using Enemy;
using Godot;
using Rewind;

public class EnemySpawnerState : RewindState {
  public float CurrentConcurrentDifficulty;
  public int CurrentSpawnIndex;
  public int SpawnedEnemiesAliveCount;
  public bool IsWaveCompleted;
}

[GlobalClass]
public partial class EnemySpawner : Node, IRewindable {
  [Signal]
  public delegate void WaveCompletedEventHandler();

  [ExportGroup("Spawning Configuration")]
  [Export]
  public Godot.Collections.Array<EnemyData> EnemyDatabase { get; set; }
  [Export]
  public float TotalDifficultyBudget { get; set; } = 100.0f;
  [Export]
  public float MaxConcurrentDifficulty { get; set; } = 30.0f;
  [Export]
  public float MinPlayerSpawnDistance { get; set; } = 500.0f;

  private List<EnemyData> _spawnQueue = new();
  private float _currentConcurrentDifficulty = 0.0f;
  private int _currentSpawnIndex = 0;
  private int _spawnedEnemiesAliveCount = 0;
  private List<Vector2I> _walkableTiles;
  private MapGenerator _mapGenerator;
  private Player _player;
  private readonly RandomNumberGenerator _rnd = new();

  public ulong InstanceId => GetInstanceId();
  public bool IsWaveCompleted { get; private set; } = false;

  public override void _Ready() {
    base._Ready();
    // 在 RewindManager 中注册自己
    if (RewindManager.Instance != null) {
      RewindManager.Instance.Register(this);
    }
  }

  public override void _ExitTree() {
    if (RewindManager.Instance != null) {
      RewindManager.Instance.Unregister(this);
    }
    base._ExitTree();
  }

  public RewindState CaptureState() {
    return new EnemySpawnerState {
      CurrentConcurrentDifficulty = this._currentConcurrentDifficulty,
      CurrentSpawnIndex = this._currentSpawnIndex,
      SpawnedEnemiesAliveCount = this._spawnedEnemiesAliveCount,
      IsWaveCompleted = this.IsWaveCompleted,
    };
  }

  public void RestoreState(RewindState state) {
    if (state is not EnemySpawnerState ess) return;

    this._currentConcurrentDifficulty = ess.CurrentConcurrentDifficulty;
    this._currentSpawnIndex = ess.CurrentSpawnIndex;
    this._spawnedEnemiesAliveCount = ess.SpawnedEnemiesAliveCount;
    this.IsWaveCompleted = ess.IsWaveCompleted;
  }

  // Spawner 本身不会被 Destroy 或 Resurrect，所以提供空实现
  public void Destroy() { }
  public void Resurrect() { }

  public void ResetSpawner() {
    _currentConcurrentDifficulty = 0.0f;
    _currentSpawnIndex = 0;
    _spawnedEnemiesAliveCount = 0;
    IsWaveCompleted = false;
    // 不重新生成队列，而是从头开始尝试生成
    TrySpawnNext();
  }

  public void StartSpawning(MapGenerator mapGenerator, Player player) {
    _mapGenerator = mapGenerator;
    _walkableTiles = new List<Vector2I>(mapGenerator.WalkableTiles);
    _player = player;

    GenerateSpawnQueue();
    TrySpawnNext();
  }

  private void GenerateSpawnQueue() {
    _spawnQueue.Clear();
    _currentSpawnIndex = 0;
    IsWaveCompleted = false;
    var availableEnemies = EnemyDatabase
        .Where(e => e.Difficulty <= MaxConcurrentDifficulty)
        .OrderByDescending(e => e.Difficulty)
        .ToList();

    if (availableEnemies.Count == 0) {
      GD.PrintErr("No enemies in database are spawnable with current MaxConcurrentDifficulty!");
      return;
    }

    float budget = TotalDifficultyBudget;
    while (budget > 0) {
      var spawnable = availableEnemies.Where(e => e.Difficulty <= budget).ToList();
      if (spawnable.Count == 0) {
        // 如果预算太小，无法生成任何怪物，则尝试用最弱的怪物填补
        var weakest = availableEnemies.Last();
        if (budget >= weakest.Difficulty) {
          _spawnQueue.Add(weakest);
          budget -= weakest.Difficulty;
        } else {
          break; // 预算实在太小，结束
        }
      } else {
        var enemyData = spawnable[_rnd.RandiRange(0, spawnable.Count - 1)];
        _spawnQueue.Add(enemyData);
        budget -= enemyData.Difficulty;
      }
    }
    // shuffle the spawn queue
    _spawnQueue.Shuffle(_rnd);
    GD.Print($"Generated spawn queue with {_spawnQueue.Count} enemies.");
  }

  private void TrySpawnNext() {
    while (_currentSpawnIndex < _spawnQueue.Count && _currentConcurrentDifficulty + _spawnQueue[_currentSpawnIndex].Difficulty <= MaxConcurrentDifficulty) {
      EnemyData enemyToSpawn = _spawnQueue[_currentSpawnIndex];
      ++_currentSpawnIndex;
      _currentConcurrentDifficulty += enemyToSpawn.Difficulty;
      SpawnEnemy(enemyToSpawn);
    }
  }

  private void SpawnEnemy(EnemyData enemyData) {
    Vector2 spawnPosition;
    int attempts = 0;
    // 尝试 20 次找到一个远离玩家的生成点
    while (attempts < 20) {
      int randomIndex = _rnd.RandiRange(0, _walkableTiles.Count - 1);
      Vector2I cell = _walkableTiles[randomIndex];
      Vector2 worldPos = _mapGenerator.MapToWorld(cell);

      if (worldPos.DistanceTo(_player.GlobalPosition) > MinPlayerSpawnDistance) {
        spawnPosition = worldPos;
        InstantiateEnemy(enemyData, spawnPosition);
        return;
      }
      attempts++;
    }

    // 如果找不到远离玩家的点，就随便找一个可走的点
    GD.Print("Could not find a spawn point far from player, spawning at any valid location.");
    int fallbackIndex = _rnd.RandiRange(0, _walkableTiles.Count - 1);
    spawnPosition = _mapGenerator.MapToWorld(_walkableTiles[fallbackIndex]);
    InstantiateEnemy(enemyData, spawnPosition);
  }

  private void InstantiateEnemy(EnemyData enemyData, Vector2 position) {
    var enemy = enemyData.Scene.Instantiate<BaseEnemy>();
    enemy.GlobalPosition = position;
    enemy.Difficulty = enemyData.Difficulty;
    enemy.Died += OnEnemyDied; // 连接信号
    ++_spawnedEnemiesAliveCount;
    GameRootProvider.CurrentGameRoot.CallDeferred(Node.MethodName.AddChild, enemy);
    GD.Print($"Spawning {enemy.Name} with difficulty {enemyData.Difficulty} at {position}.");
  }

  private void OnEnemyDied(float difficulty) {
    _currentConcurrentDifficulty -= difficulty;
    --_spawnedEnemiesAliveCount;
    GD.Print($"Enemy died (difficulty: {difficulty}). Current concurrent difficulty: {_currentConcurrentDifficulty}");
    TrySpawnNext();

    // 检查波次是否真正完成（生成队列为空，并且场上也没有敌人了）
    if (!IsWaveCompleted &&
        _currentSpawnIndex >= _spawnQueue.Count &&
        _spawnedEnemiesAliveCount == 0) {
      GD.Print("Spawn queue is empty and all spawned enemies are defeated. Wave complete!");
      IsWaveCompleted = true;
      EmitSignal(SignalName.WaveCompleted);
    }
  }
}
