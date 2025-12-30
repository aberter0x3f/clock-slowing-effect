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

  public override float MaxHealth { get; protected set; } = 30f;

  [ExportGroup("Movement")]
  [Export] public float MoveToStartSpeed { get; set; } = 6.0f;

  [ExportGroup("Attack Pattern")]
  [Export] public PackedScene BulletScene { get; set; }
  [Export] public float WaveInterval { get; set; } = 1.0f;
  [Export] public float BulletSpacing { get; set; } = 0.12f;

  [ExportGroup("Bullet Properties")]
  [Export] public float BulletForwardSpeed { get; set; } = 4.0f;
  [Export] public float BulletMaxHeight { get; set; } = 1.0f;
  [Export] public float BulletT1 { get; set; } = 0.5f;
  [Export] public float BulletT2 { get; set; } = 1.0f;
  [Export] public float BulletPhaseScale { get; set; } = 0.3f;

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");

    float rank = GameManager.Instance.EnemyRank;
    // 难度缩放
    WaveInterval = Mathf.Min(2f, WaveInterval / (rank * 2 / (rank + 5)));
    BulletT1 = Mathf.Max(0.2f, BulletT1 * 5f / rank);
    BulletForwardSpeed *= rank / 5f;

    _currentState = AttackState.MovingToStartPosition;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    switch (_currentState) {
      case AttackState.MovingToStartPosition:
        float halfHeight = (_mapGenerator.MapHeight / 2f - 1) * _mapGenerator.TileSize;
        var startPos = new Vector3(0, 0, -halfHeight);
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition.MoveToward(startPos, MoveToStartSpeed * scaledDelta);
        if (ParentBoss.GlobalPosition.IsEqualApprox(startPos)) {
          PrepareNextWave();
          FireWave();
        }
        break;

      case AttackState.MovingAndWaiting:
        _waveTimer -= scaledDelta;
        float progress = 1.0f - Mathf.Clamp(_waveTimer / WaveInterval, 0.0f, 1.0f);
        float newX = Mathf.Lerp(_startX, _targetX, progress);
        ParentBoss.GlobalPosition = ParentBoss.GlobalPosition with { X = newX };

        if (_waveTimer <= 0) {
          FireWave();
          PrepareNextWave();
        }
        break;
    }
  }

  private void PrepareNextWave() {
    _startX = ParentBoss.GlobalPosition.X;
    float halfWidth = (_mapGenerator.MapWidth / 2f - 2) * _mapGenerator.TileSize;
    _targetX = (float) GD.RandRange(-halfWidth, halfWidth);
    _currentState = AttackState.MovingAndWaiting;
    _waveTimer = WaveInterval;
  }

  private void FireWave() {
    SoundManager.Instance.Play(SoundEffect.FireBig);
    ++_waveCounter;

    float halfWidth = (_mapGenerator.MapWidth / 2f - 1) * _mapGenerator.TileSize;
    float bossX = ParentBoss.GlobalPosition.X;
    float spawnZ = ParentBoss.GlobalPosition.Z;

    bool invert = _waveCounter % 2 != 0;
    for (float x = -halfWidth; x <= halfWidth; x += BulletSpacing) {
      var bullet = BulletScene.Instantiate<SimpleBullet>();
      Vector3 startPos = new Vector3(x, 0, spawnZ);
      float phaseOff = (x - bossX) * BulletPhaseScale;
      bullet.UpdateFunc = (t) => {
        SimpleBullet.UpdateState s = new();
        float period = BulletT1 + BulletT2;
        float time = Mathf.PosMod(phaseOff + (invert ? -t : t), period);

        float h = 0;
        if (time <= BulletT1)
          h = BulletMaxHeight * Mathf.Sin(time / BulletT1 * Mathf.Pi);

        s.position = startPos + Vector3.Back * (BulletForwardSpeed * t) + Vector3.Up * h;
        return s;
      };

      GameRootProvider.CurrentGameRoot.AddChild(bullet);
    }
  }

  public override RewindState CaptureInternalState() => new PhaseWaveState {
    CurrentState = _currentState,
    WaveTimer = _waveTimer,
    TargetX = _targetX,
    StartX = _startX,
    WaveCounter = _waveCounter
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseWaveState pws) return;
    _currentState = pws.CurrentState;
    _waveTimer = pws.WaveTimer;
    _targetX = pws.TargetX;
    _startX = pws.StartX;
    _waveCounter = pws.WaveCounter;
  }
}
