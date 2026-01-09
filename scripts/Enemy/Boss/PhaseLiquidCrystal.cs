using System.Collections.Generic;
using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseLiquidCrystalState : BasePhaseState {
  public PhaseLiquidCrystal.PhaseState CurrentState;
  public float Timer;
  public float OrbitAngle;
  public Vector3 LastPlayerPosition;
}

public partial class PhaseLiquidCrystal : BasePhase {
  public enum PhaseState { Waiting, Active }

  [ExportGroup("Scene References")]
  [Export] public PackedScene LiquidCrystalBulletScene { get; set; }
  [Export] public PackedScene OrbitingBulletScene { get; set; }
  [Export] public PackedScene TrailBulletScene { get; set; }

  [ExportGroup("Configuration")]
  [Export] public float WaitDuration { get; set; } = 2f;
  [Export] public float BulletSpacing { get; set; } = 0.65f;
  [Export] public float BossChaseSpeed { get; set; } = 1.0f;
  [Export] public float OrbitRadius { get; set; } = 1.0f;
  [Export] public float OrbitSpeed { get; set; } = 2.0f;
  [Export] public float TrailMinDistance { get; set; } = 0.08f;
  [Export] public float TrailClearanceRadius { get; set; } = 0.25f;

  private PhaseState _currentState;
  private float _timer;
  private MapGenerator _mapGenerator;
  private readonly List<PhaseLiquidCrystalBullet> _activeCrystals = new();
  private readonly List<BaseBullet> _trailBullets = new();
  private SimpleBullet _orbitingBullet;
  private float _orbitAngle;
  private Vector3 _lastPlayerPosition;

  public override void PhaseStart(Boss parent) {
    base.PhaseStart(parent);
    _mapGenerator = GetTree().Root.GetNode<MapGenerator>("GameRoot/MapGenerator");

    var rank = GameManager.Instance.EnemyRank;
    BulletSpacing = Mathf.Max(0.5f, BulletSpacing * 15f / (rank + 10));
    BossChaseSpeed *= (rank + 10f) / 15f;

    SpawnCrystals();
    SpawnOrbitingBullet();

    _currentState = PhaseState.Waiting;
    _timer = WaitDuration;
    _lastPlayerPosition = PlayerNode.GlobalPosition;
  }

  public override void UpdatePhase(float scaledDelta, float effectiveTimeScale) {
    _timer -= scaledDelta;

    switch (_currentState) {
      case PhaseState.Waiting:
        ParentBoss.Velocity = Vector3.Zero;
        if (_timer <= 0) _currentState = PhaseState.Active;
        break;

      case PhaseState.Active:
        Vector3 dir = (PlayerNode.GlobalPosition - ParentBoss.GlobalPosition).Normalized();
        ParentBoss.Velocity = dir * BossChaseSpeed;
        _orbitAngle += OrbitSpeed * scaledDelta;
        break;
    }

    // 物理移动
    ParentBoss.MoveAndSlide();

    // 更新轨道弹位置
    if (IsInstanceValid(_orbitingBullet)) {
      Vector3 offset = _currentState == PhaseState.Waiting ? Vector3.Zero :
                       new Vector3(Mathf.Cos(_orbitAngle), 0, Mathf.Sin(_orbitAngle)) * OrbitRadius;
      _orbitingBullet.GlobalPosition = ParentBoss.GlobalPosition + offset;
    }

    // 同步 Boss 速度给液晶分子
    foreach (var crystal in _activeCrystals) {
      if (IsInstanceValid(crystal)) crystal.BossVelocity = ParentBoss.Velocity;
    }

    HandlePlayerTrail();
    HandleTrailClearing();
  }

  private void SpawnCrystals() {
    float hw = (_mapGenerator.MapWidth / 2f) * _mapGenerator.TileSize;
    float hh = (_mapGenerator.MapHeight / 2f) * _mapGenerator.TileSize;

    Rect2 bounds = new Rect2(new Vector2(-hw, -hh), new Vector2(hw * 2, hh * 2));
    Vector3 playerPos = PlayerNode.GlobalPosition;

    for (float z = -hh; z <= hh; z += BulletSpacing) {
      for (float x = -hw; x <= hw; x += BulletSpacing) {
        Vector3 spawnPos = new Vector3(x, 0, z);
        if (spawnPos.DistanceTo(playerPos) < BulletSpacing * 0.5f) continue;

        var crystal = LiquidCrystalBulletScene.Instantiate<PhaseLiquidCrystalBullet>();
        crystal.Position = spawnPos;
        crystal.SetBounds(bounds);
        GameRootProvider.CurrentGameRoot.AddChild(crystal);
        _activeCrystals.Add(crystal);
      }
    }
  }

  private void SpawnOrbitingBullet() {
    _orbitingBullet = OrbitingBulletScene.Instantiate<SimpleBullet>();
    _orbitingBullet.UpdateFunc = (t) => { return new SimpleBullet.UpdateState { position = _orbitingBullet.GlobalPosition }; };
    GameRootProvider.CurrentGameRoot.AddChild(_orbitingBullet);
  }

  private void HandlePlayerTrail() {
    Vector3 currentPos = PlayerNode.GlobalPosition;
    if (currentPos.DistanceTo(_lastPlayerPosition) > TrailMinDistance) {
      var trail = TrailBulletScene.Instantiate<BaseBullet>();
      trail.Position = _lastPlayerPosition;
      GameRootProvider.CurrentGameRoot.AddChild(trail);
      _trailBullets.Add(trail);
      _lastPlayerPosition = currentPos;
    }
  }

  private void HandleTrailClearing() {
    if (!IsInstanceValid(_orbitingBullet)) return;
    Vector3 cleanerPos = _orbitingBullet.GlobalPosition;

    for (int i = _trailBullets.Count - 1; i >= 0; i--) {
      var b = _trailBullets[i];
      if (!IsInstanceValid(b) || b.IsDestroyed) { _trailBullets.RemoveAt(i); continue; }
      if (b.GlobalPosition.DistanceTo(cleanerPos) < TrailClearanceRadius) {
        b.Destroy();
        _trailBullets.RemoveAt(i);
      }
    }
  }

  public override RewindState CaptureInternalState() => new PhaseLiquidCrystalState {
    CurrentState = _currentState,
    Timer = _timer,
    OrbitAngle = _orbitAngle,
    LastPlayerPosition = _lastPlayerPosition
  };

  public override void RestoreInternalState(RewindState state) {
    if (state is not PhaseLiquidCrystalState s) return;
    _currentState = s.CurrentState; _timer = s.Timer; _orbitAngle = s.OrbitAngle; _lastPlayerPosition = s.LastPlayerPosition;
  }
}
