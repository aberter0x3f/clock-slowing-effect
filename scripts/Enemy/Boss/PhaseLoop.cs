using System.Collections.Generic;
using System.Linq;
using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseLoopState : BasePhaseState {
  public PhaseLoop.AttackState CurrentState;
  public float Timer;
  public float OrbitAngle;
  public float HomingBulletTimer;
  public float DefenseBulletTimer;
}

public partial class PhaseLoop : BasePhase {
  public override float MaxHealth { get; protected set; } = 50f;
  public override float DamageReduction => 1f;

  public enum AttackState {
    Waiting,
    Active
  }

  // 状态机
  private AttackState _currentState = AttackState.Waiting;
  private float _timer;
  private float _orbitBngle;
  private float _homingBulletTimer;
  private float _defenseBulletTimer;
  private float _radiusA;
  private float _radiusB;

  // 子弹引用
  private readonly List<PhaseLoopBullet> _aBullets = new();
  private readonly List<PhaseLoopBullet> _bBullets = new();
  private readonly List<List<PhaseLoopBullet>> _cBullets = new();
  private readonly List<float> _initialThetas = new();

  private MapGenerator _mapGenerator;

  [ExportGroup("Timing")]
  [Export]
  public float InitialWaitTime { get; set; } = 1.0f;
  [Export]
  public float HomingBulletInterval { get; set; } = 1f;
  [Export]
  public float DefenseBulletInterval { get; set; } = 0.1f;

  [ExportGroup("Orbit")]
  [Export]
  public float OrbitSpeedA { get; set; } = 0.2f; // Radians per second

  [ExportGroup("Height Oscillation")]
  [Export]
  public float HeightAmplitudeH { get; set; } = 50f;
  [Export]
  public float HeightFrequencyK { get; set; } = 5.0f;

  [ExportGroup("Bullet Counts")]
  [Export(PropertyHint.Range, "2, 100, 2")]
  public int BigBulletCount { get; set; } = 6;
  [Export(PropertyHint.Range, "1, 1000, 1")]
  public int SmallBulletCount { get; set; } = 200;
  [Export(PropertyHint.Range, "1, 1000, 1")]
  public int InactiveBigBulletCount { get; set; } = 100;

  [ExportGroup("Scene References")]
  [Export]
  public PackedScene BigBulletScene { get; set; }
  [Export]
  public PackedScene InactiveBigBulletScene { get; set; }
  [Export]
  public PackedScene SmallBulletScene { get; set; }
  [Export]
  public PackedScene HomingBulletScene { get; set; }
  [Export]
  public PackedScene DefenseBulletScene { get; set; }

  [ExportGroup("Time")]
  [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
  public float TimeScaleSensitivity { get; set; } = 1f;

  public override void StartPhase(Boss parent) {
    base.StartPhase(parent);

    parent.SetCollisionEnabled(false);

    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr("PhaseLoop: MapGenerator not found. Phase cannot start.");
      EndPhase();
      return;
    }

    var rank = GameManager.Instance.EnemyRank;
    TimeScaleSensitivity = 5f / (rank + 5);
    float minMapDim = Mathf.Min(_mapGenerator.MapWidth, _mapGenerator.MapHeight) * _mapGenerator.TileSize;
    _radiusA = minMapDim * 0.4f;
    _radiusB = _radiusA * 1.2f;

    OrbitSpeedA *= (rank + 5) / 10f;
    HomingBulletInterval /= (rank + 5) / 10f;

    // 初始化状态机
    _currentState = AttackState.Waiting;
    _timer = InitialWaitTime;
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) {
      // 在预览模式下，仅根据已恢复的状态确保位置正确
      if (_currentState == AttackState.Active) {
        UpdateBulletPositions();
      }
      return;
    }
    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;

    switch (_currentState) {
      case AttackState.Waiting:
        _timer -= scaledDelta;
        if (_timer <= 0) {
          SpawnAllBullets();
          _currentState = AttackState.Active;
          _homingBulletTimer = HomingBulletInterval;
          _defenseBulletTimer = DefenseBulletInterval * 10f;
        }
        break;
      case AttackState.Active:
        _orbitBngle += OrbitSpeedA * scaledDelta;
        UpdateBulletPositions();

        _homingBulletTimer -= scaledDelta;
        if (_homingBulletTimer <= 0) {
          FireHomingBullet();
          _homingBulletTimer = HomingBulletInterval;
        }
        _defenseBulletTimer -= scaledDelta;
        if (_defenseBulletTimer <= 0) {
          FireDefenseBullet();
          _defenseBulletTimer = DefenseBulletInterval;
        }
        break;
    }
  }

  private void SpawnAllBullets() {
    PlayAttackSound();

    // 清理任何旧的子弹引用
    _aBullets.Clear();
    _bBullets.Clear();
    _cBullets.Clear();
    _initialThetas.Clear();

    var gr = GameRootProvider.CurrentGameRoot;
    var deltaPhiPiCount = Mathf.RoundToInt(GameManager.Instance.EnemyRank / 2f) * 4;

    for (int i = 0; i < BigBulletCount; ++i) {
      float theta = Mathf.Tau / BigBulletCount * i;
      _initialThetas.Add(theta);

      // 生成 A 子弹
      var bulletA = BigBulletScene.Instantiate<PhaseLoopBullet>();
      bulletA.H = HeightAmplitudeH;
      bulletA.K = HeightFrequencyK;
      bulletA.Phi = i % 2 * Mathf.Pi;
      bulletA.TimeScaleSensitivity = TimeScaleSensitivity;
      gr.AddChild(bulletA);
      _aBullets.Add(bulletA);

      // 生成 B 子弹
      var bulletB = BigBulletScene.Instantiate<PhaseLoopBullet>();
      bulletB.H = HeightAmplitudeH;
      bulletB.K = HeightFrequencyK;
      bulletB.Phi = (deltaPhiPiCount + i % 2 + 1) * Mathf.Pi;
      bulletB.TimeScaleSensitivity = TimeScaleSensitivity;
      gr.AddChild(bulletB);
      _bBullets.Add(bulletB);

      // 生成 C 子弹
      var cLine = new List<PhaseLoopBullet>();
      for (int j = 0; j < SmallBulletCount; ++j) {
        var bulletC = SmallBulletScene.Instantiate<PhaseLoopBullet>();
        bulletC.H = HeightAmplitudeH;
        bulletC.K = HeightFrequencyK;
        float progress = (j + 1.0f) / (SmallBulletCount + 1.0f);
        bulletC.Phi = Mathf.Lerp(bulletA.Phi, bulletB.Phi, progress);
        bulletC.TimeScaleSensitivity = TimeScaleSensitivity;
        gr.AddChild(bulletC);
        cLine.Add(bulletC);
      }
      _cBullets.Add(cLine);
    }

    for (int i = 0; i < InactiveBigBulletCount; ++i) {
      var inactiveBullet = InactiveBigBulletScene.Instantiate<BaseBullet>();
      inactiveBullet.GlobalPosition = Vector2.Right.Rotated(Mathf.Tau / InactiveBigBulletCount * i) * _radiusA;
      gr.AddChild(inactiveBullet);
    }

    // 设置初始位置
    UpdateBulletPositions();
  }

  private void UpdateBulletPositions() {
    if (_aBullets.Count != BigBulletCount || _bBullets.Count != BigBulletCount || _cBullets.Count != BigBulletCount) return;

    for (int i = 0; i < BigBulletCount; ++i) {
      float thetaA = _initialThetas[i] + _orbitBngle * 3f;
      var posA = new Vector2(Mathf.Cos(thetaA), Mathf.Sin(thetaA)) * _radiusA;
      _aBullets[i].RawPosition = new Vector3(posA.X, posA.Y, _aBullets[i].RawPosition.Z);

      float thetaB = _initialThetas[i] + _orbitBngle;
      var posB = new Vector2(Mathf.Cos(thetaB), Mathf.Sin(thetaB)) * _radiusB;
      _bBullets[i].RawPosition = new Vector3(posB.X, posB.Y, _bBullets[i].RawPosition.Z);

      for (int j = 0; j < SmallBulletCount; ++j) {
        float progress = (j + 1.0f) / (SmallBulletCount + 1.0f);
        var posC = posA.Lerp(posB, progress);
        _cBullets[i][j].RawPosition = new Vector3(posC.X, posC.Y, _cBullets[i][j].RawPosition.Z);
      }
    }
  }

  private void FireHomingBullet() {
    if (HomingBulletScene == null || PlayerNode == null || !IsInstanceValid(PlayerNode)) return;

    var homingBullet = HomingBulletScene.Instantiate<SimpleBullet>();
    var startPos = ParentBoss.GlobalPosition;
    var direction = (PlayerNode.GlobalPosition - startPos).Normalized();

    homingBullet.GlobalPosition = startPos;
    homingBullet.Rotation = direction.Angle();
    homingBullet.TimeScaleSensitivity = TimeScaleSensitivity;
    GameRootProvider.CurrentGameRoot.AddChild(homingBullet);
  }

  private void FireDefenseBullet() {
    if (DefenseBulletScene == null || PlayerNode == null || !IsInstanceValid(PlayerNode)) return;

    foreach (var bullet in _aBullets.Concat(_bBullets)) {
      var defenseBullet = DefenseBulletScene.Instantiate<SimpleBullet>();
      var startPos = bullet.GlobalPosition;
      var direction = startPos.Normalized();

      defenseBullet.GlobalPosition = startPos;
      defenseBullet.Rotation = direction.Angle();
      defenseBullet.TimeScaleSensitivity = TimeScaleSensitivity;
      GameRootProvider.CurrentGameRoot.AddChild(defenseBullet);
    }
  }

  public override RewindState CaptureInternalState() {
    return new PhaseLoopState {
      CurrentState = this._currentState,
      Timer = this._timer,
      OrbitAngle = this._orbitBngle,
      HomingBulletTimer = this._homingBulletTimer
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseLoopState pls) return;

    this._currentState = pls.CurrentState;
    this._timer = pls.Timer;
    this._orbitBngle = pls.OrbitAngle;
    this._homingBulletTimer = pls.HomingBulletTimer;
  }
}
