using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseWaveState : BasePhaseState {
  public PhaseWave.AttackState CurrentState;
  public float WaveTimer;
  public float TargetX;
  public float StartX;
  public int WaveCounter;
}

public partial class PhaseWave : BasePhase {
  public enum AttackState {
    MovingToStartPosition,
    MovingAndWaiting,
  }

  private AttackState _currentState;
  private float _waveTimer;
  private float _targetX;
  private float _startX;
  private int _waveCounter;

  private MapGenerator _mapGenerator;
  private readonly RandomNumberGenerator _rng = new();

  public override float MaxHealth { get; protected set; } = 30f;

  [ExportGroup("Movement")]
  [Export]
  public float MoveToStartSpeed { get; set; } = 600f;

  [ExportGroup("Attack Pattern")]
  [Export]
  public PackedScene WaveBulletScene { get; set; }
  [Export(PropertyHint.Range, "0.1, 10.0, 0.1")]
  public float WaveInterval { get; set; } = 1f; // T
  [Export(PropertyHint.Range, "1, 100, 1")]
  public float BulletSpacing { get; set; } = 10f;

  [ExportGroup("Bullet Properties")]
  [Export(PropertyHint.Range, "50, 1000, 10")]
  public float BulletForwardSpeed { get; set; } = 400f;
  [Export(PropertyHint.Range, "10, 500, 10")]
  public float BulletMaxHeight { get; set; } = 100f;
  [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
  public float BulletT1 { get; set; } = 0.5f; // 余弦拱形的持续时间
  [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
  public float BulletT2 { get; set; } = 1f; // 在地面上的持续时间
  [Export(PropertyHint.Range, "0.001, 0.01, 0.001")]
  public float BulletPhaseScale { get; set; } = 0.003f;

  [ExportGroup("Difficulty Scaling")]
  [Export(PropertyHint.Range, "0.1, 5.0, 0.1")]
  public float MinBulletT1 { get; set; } = 0.2f;
  [Export(PropertyHint.Range, "0.1, 10.0, 0.1")]
  public float MaxWaveInterval { get; set; } = 2f;

  public override void StartPhase(Boss parent) {
    base.StartPhase(parent);
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr("PhaseWave: MapGenerator not found. Phase cannot start.");
      EndPhase();
      return;
    }

    // 根据难度调整参数
    float rank = GameManager.Instance.EnemyRank;
    WaveInterval = Mathf.Min(MaxWaveInterval, WaveInterval / (rank * 2 / (rank + 5)));
    BulletT1 = Mathf.Max(MinBulletT1, BulletT1 * 5f / rank);
    BulletForwardSpeed *= rank / 5f;

    _currentState = AttackState.MovingToStartPosition;
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    switch (_currentState) {
      case AttackState.MovingToStartPosition:
        var startPos = new Vector2(0, -_mapGenerator.MapHeight * _mapGenerator.TileSize / 2f);
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(startPos, MoveToStartSpeed * scaledDelta);
        if (ParentBoss.GlobalPosition.IsEqualApprox(startPos)) {
          // 到达后，准备并立即发射第一波
          PrepareNextWave();
          FireWave();
        }
        break;

      case AttackState.MovingAndWaiting:
        _waveTimer -= scaledDelta;

        // 并行移动：根据计时器进度用 Lerp 计算当前 X 坐标
        float progress = 1.0f - Mathf.Clamp(_waveTimer / WaveInterval, 0.0f, 1.0f);
        float newX = Mathf.Lerp(_startX, _targetX, progress);
        ParentBoss.GlobalPosition = new Vector2(newX, ParentBoss.GlobalPosition.Y);

        if (_waveTimer <= 0) {
          // 时间到，发射下一波，并准备再下一波的移动
          FireWave();
          PrepareNextWave();
        }
        break;
    }
  }

  /// <summary>
  /// 准备下一次移动和攻击计时．
  /// </summary>
  private void PrepareNextWave() {
    _startX = ParentBoss.GlobalPosition.X;
    float halfWidth = _mapGenerator.MapWidth * _mapGenerator.TileSize / 2.0f;
    _targetX = (float) _rng.RandfRange(-halfWidth, halfWidth);

    // 设置状态和计时器
    _currentState = AttackState.MovingAndWaiting;
    _waveTimer = WaveInterval;
  }

  /// <summary>
  /// 发射一波覆盖全图的子弹．
  /// </summary>
  private void FireWave() {
    if (WaveBulletScene == null) {
      GD.PrintErr("PhaseWave: WaveBulletScene is not set!");
      return;
    }

    PlayAttackSound();

    ++_waveCounter;
    float mapWidth = _mapGenerator.MapWidth * _mapGenerator.TileSize;
    float startX = -mapWidth / 2.0f;
    float endX = mapWidth / 2.0f;
    float bossX = ParentBoss.GlobalPosition.X;

    for (float x = startX; x <= endX; x += BulletSpacing) {
      var bullet = WaveBulletScene.Instantiate<PhaseWaveBullet>();

      // 初始化子弹属性
      bullet.ForwardSpeed = BulletForwardSpeed;
      bullet.Direction = Vector2.Down;
      bullet.MaxHeight = BulletMaxHeight;
      bullet.T1 = BulletT1;
      bullet.T2 = BulletT2;
      bullet.InitialPhase = (x - bossX) * BulletPhaseScale;
      bullet.InvertWave = (_waveCounter % 2 != 0);

      // 设置子弹的 3D 初始位置
      bullet.RawPosition = new Vector3(x, ParentBoss.GlobalPosition.Y, 0);

      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  public override RewindState CaptureInternalState() {
    return new PhaseWaveState {
      CurrentState = this._currentState,
      WaveTimer = this._waveTimer,
      TargetX = this._targetX,
      StartX = this._startX,
      WaveCounter = this._waveCounter
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseWaveState pws) return;
    this._currentState = pws.CurrentState;
    this._waveTimer = pws.WaveTimer;
    this._targetX = pws.TargetX;
    this._startX = pws.StartX;
    this._waveCounter = pws.WaveCounter;
  }
}
