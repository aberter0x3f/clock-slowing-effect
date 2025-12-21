using System.Collections.Generic;
using System.Linq;
using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseLoopState : BasePhaseState {
  public PhaseLoop.AttackState CurrentState;
  public float Timer;
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

  private AttackState _currentState = AttackState.Waiting;
  private float _timer;
  private float _homingBulletTimer;
  private float _defenseBulletTimer;
  private float _radiusA;
  private float _radiusB;

  private readonly List<SimpleBullet> _aBullets = new();
  private readonly List<SimpleBullet> _bBullets = new();

  private MapGenerator _mapGenerator;

  [ExportGroup("Timing")]
  [Export] public float InitialWaitTime { get; set; } = 1.0f;
  [Export] public float HomingBulletInterval { get; set; } = 1f;
  [Export] public float DefenseBulletInterval { get; set; } = 0.1f;

  [ExportGroup("Orbit")]
  [Export] public float OrbitSpeedA { get; set; } = 0.2f; // Radians per second

  [ExportGroup("Height Oscillation")]
  [Export] public float HeightAmplitudeH { get; set; } = 0.5f;
  [Export] public float HeightFrequencyK { get; set; } = 5.0f;

  [ExportGroup("Bullet Counts")]
  [Export(PropertyHint.Range, "2, 100, 2")]
  public int BigBulletCount { get; set; } = 6;
  [Export(PropertyHint.Range, "1, 1000, 1")]
  public int SmallBulletCount { get; set; } = 200;
  [Export(PropertyHint.Range, "1, 1000, 1")]
  public int InactiveBigBulletCount { get; set; } = 100;

  [ExportGroup("Scene References")]
  [Export] public PackedScene BigBulletScene { get; set; }
  [Export] public PackedScene InactiveBigBulletScene { get; set; }
  [Export] public PackedScene SmallBulletScene { get; set; }
  [Export] public PackedScene HomingBulletScene { get; set; }
  [Export] public PackedScene DefenseBulletScene { get; set; }

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    parent.SetCollisionEnabled(false);

    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    var rank = GameManager.Instance.EnemyRank;
    TimeScaleSensitivity = 5f / (rank + 5);
    float minMapDim = Mathf.Min(_mapGenerator.MapWidth, _mapGenerator.MapHeight) * _mapGenerator.TileSize;
    _radiusA = minMapDim * 0.4f;
    _radiusB = _radiusA * 1.2f;

    OrbitSpeedA *= (rank + 5) / 10f;
    HomingBulletInterval /= (rank + 5) / 10f;

    _currentState = AttackState.Waiting;
    _timer = InitialWaitTime;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
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
    SoundManager.Instance.Play(SoundEffect.FireBig);

    _aBullets.Clear();
    _bBullets.Clear();

    var gr = GameRootProvider.CurrentGameRoot;
    var rank = GameManager.Instance.EnemyRank;
    var deltaPhiPiCount = Mathf.RoundToInt(rank / 2f) * 4;

    for (int i = 0; i < BigBulletCount; ++i) {
      float theta0 = Mathf.Tau / BigBulletCount * i;
      float phiA = (i % 2) * Mathf.Pi;
      float phiB = (deltaPhiPiCount + i % 2 + 1) * Mathf.Pi;

      // --- 轨道器 A ---
      var bulletA = BigBulletScene.Instantiate<SimpleBullet>();
      bulletA.TimeScaleSensitivity = TimeScaleSensitivity;
      bulletA.UpdateFunc = (t) => {
        SimpleBullet.UpdateState s = new();
        float currentTheta = theta0 + (OrbitSpeedA * t * 3f);
        var pos2d = Vector2.Right.Rotated(currentTheta) * _radiusA;
        float h = HeightAmplitudeH * Mathf.Max(0f, Mathf.Sin(HeightFrequencyK * t + phiA));
        s.position = new Vector3(pos2d.X, h, -pos2d.Y);
        return s;
      };
      gr.AddChild(bulletA);
      _aBullets.Add(bulletA);

      // --- 轨道器 B ---
      var bulletB = BigBulletScene.Instantiate<SimpleBullet>();
      bulletB.TimeScaleSensitivity = TimeScaleSensitivity;
      bulletB.UpdateFunc = (t) => {
        SimpleBullet.UpdateState s = new();
        float currentTheta = theta0 + (OrbitSpeedA * t);
        var pos2d = Vector2.Right.Rotated(currentTheta) * _radiusB;
        float h = HeightAmplitudeH * Mathf.Max(0f, Mathf.Sin(HeightFrequencyK * t + phiB));
        s.position = new Vector3(pos2d.X, h, -pos2d.Y);
        return s;
      };
      gr.AddChild(bulletB);
      _bBullets.Add(bulletB);

      // --- 连接线 C ---
      for (int j = 0; j < SmallBulletCount; ++j) {
        float progress = (j + 1.0f) / (SmallBulletCount + 1.0f);
        float phiC = Mathf.Lerp(phiA, phiB, progress);

        var bulletC = SmallBulletScene.Instantiate<SimpleBullet>();
        bulletC.TimeScaleSensitivity = TimeScaleSensitivity;
        bulletC.UpdateFunc = (t) => {
          SimpleBullet.UpdateState s = new();

          float thetaA_t = theta0 + (OrbitSpeedA * t * 3f);
          var posA_2d = Vector2.Right.Rotated(thetaA_t) * _radiusA;

          float thetaB_t = theta0 + (OrbitSpeedA * t);
          var posB_2d = Vector2.Right.Rotated(thetaB_t) * _radiusB;

          var posC_2d = posA_2d.Lerp(posB_2d, progress);
          float h = HeightAmplitudeH * Mathf.Max(0f, Mathf.Sin(HeightFrequencyK * t + phiC));

          s.position = new Vector3(posC_2d.X, h, -posC_2d.Y);
          return s;
        };
        gr.AddChild(bulletC);
      }
    }

    // --- 背景环 ---
    for (int i = 0; i < InactiveBigBulletCount; ++i) {
      var inactiveBullet = InactiveBigBulletScene.Instantiate<BaseBullet>();
      float angle = Mathf.Tau / InactiveBigBulletCount * i;
      inactiveBullet.Position = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * _radiusA;
      gr.AddChild(inactiveBullet);
    }
  }

  private void FireHomingBullet() {
    if (HomingBulletScene == null || PlayerNode == null) return;
    var bullet = HomingBulletScene.Instantiate<SimpleBullet>();
    Vector3 startPos = ParentBoss.GlobalPosition;
    Vector3 direction = (PlayerNode.GlobalPosition - startPos).Normalized();
    float speed = 1.0f;
    bullet.UpdateFunc = (t) => {
      SimpleBullet.UpdateState s = new();
      s.position = startPos + direction * (speed * t);
      return s;
    };
    GameRootProvider.CurrentGameRoot.AddChild(bullet);
  }

  private void FireDefenseBullet() {
    if (DefenseBulletScene == null) return;
    foreach (var orbiter in _aBullets.Concat(_bBullets)) {
      if (!IsInstanceValid(orbiter) || orbiter.IsDestroyed) continue;

      var defenseBullet = DefenseBulletScene.Instantiate<SimpleBullet>();
      Vector3 startPos = orbiter.GlobalPosition;
      Vector3 direction = new Vector3(startPos.X, 0, startPos.Z).Normalized();
      float speed = 10f;

      defenseBullet.UpdateFunc = (t) => {
        SimpleBullet.UpdateState s = new();
        s.position = startPos + direction * (speed * t);
        return s;
      };
      GameRootProvider.CurrentGameRoot.AddChild(defenseBullet);
    }
  }

  public override RewindState CaptureInternalState() => new PhaseLoopState {
    CurrentState = _currentState,
    Timer = _timer,
    HomingBulletTimer = _homingBulletTimer,
    DefenseBulletTimer = _defenseBulletTimer
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseLoopState pls) return;
    _currentState = pls.CurrentState;
    _timer = pls.Timer;
    _homingBulletTimer = pls.HomingBulletTimer;
    _defenseBulletTimer = pls.DefenseBulletTimer;
  }
}
