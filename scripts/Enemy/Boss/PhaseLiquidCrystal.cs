using System.Collections.Generic;
using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseLiquidCrystalState : BasePhaseState {
  public PhaseLiquidCrystal.PhaseState CurrentState;
  public float Timer;
  public float OrbitAngle;
  public Vector2 LastPlayerPosition;
}

public partial class PhaseLiquidCrystal : BasePhase {
  public enum PhaseState {
    Waiting,
    Active,
  }

  [ExportGroup("Scene References")]
  [Export]
  public PackedScene LiquidCrystalBulletScene { get; set; }
  [Export]
  public PackedScene OrbitingBulletScene { get; set; }
  [Export]
  public PackedScene TrailBulletScene { get; set; }

  [ExportGroup("Pattern Configuration")]
  [Export]
  public float WaitDuration { get; set; } = 2f;
  [Export]
  public float BulletSpacing { get; set; } = 60f;
  [Export]
  public float BossChaseSpeed { get; set; } = 100f;
  [Export(PropertyHint.Range, "0.0, 2.0, 0.01")]
  public float BossInfluenceOnCrystals { get; set; } = 0.5f;

  [ExportGroup("Orbiting Bullet")]
  [Export]
  public float OrbitRadius { get; set; } = 50f;
  [Export]
  public float OrbitSpeed { get; set; } = 2.0f; // rad/s

  [ExportGroup("Player Trail")]
  [Export]
  public float TrailMinDistance { get; set; } = 8f;
  [Export]
  public float TrailClearanceRadius { get; set; } = 25f;

  [ExportGroup("Time")]
  [Export]
  public float TimeScaleSensitivity { get; set; } = 1f;

  private PhaseState _currentState;
  private float _timer;
  private MapGenerator _mapGenerator;
  private readonly List<PhaseLiquidCrystalBullet> _activeCrystals = new();
  private readonly List<BaseBullet> _trailBullets = new();
  private Rect2 _crystalBounds;
  private BaseBullet _orbitingBullet;
  private float _orbitAngle;
  private Vector2 _lastPlayerPosition;

  public override void StartPhase(Boss parent) {
    base.StartPhase(parent);
    _mapGenerator = GetTree().Root.GetNodeOrNull<MapGenerator>("GameRoot/MapGenerator");
    if (_mapGenerator == null) {
      GD.PrintErr("PhaseLiquidCrystal: MapGenerator not found. Phase cannot start.");
      EndPhase();
      return;
    }

    // 根据难度调整参数
    float rank = GameManager.Instance.EnemyRank;
    TimeScaleSensitivity = 5f / (rank + 5);
    BulletSpacing = Mathf.Max(45f, (BulletSpacing - 15f) * 10f / (rank + 5) + 15f);
    BossChaseSpeed *= (rank + 5f) / 10f;

    // 生成所有子弹并设置其属性
    SpawnCrystals();
    SpawnOrbitingBullet();

    // 初始化状态机
    _currentState = PhaseState.Waiting;
    _timer = WaitDuration;

    _lastPlayerPosition = PlayerNode.GlobalPosition;
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) {
      if (_currentState == PhaseState.Waiting) {
        _orbitingBullet.GlobalPosition = ParentBoss.GlobalPosition;
      } else {
        UpdateOrbitingBulletPosition();
      }
      return;
    }
    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;

    _timer -= scaledDelta;

    switch (_currentState) {
      case PhaseState.Waiting:
        ParentBoss.Velocity = Vector2.Zero;
        _orbitingBullet.GlobalPosition = ParentBoss.GlobalPosition;
        if (_timer <= 0) {
          _currentState = PhaseState.Active;
        }
        break;

      case PhaseState.Active:
        if (IsInstanceValid(PlayerNode)) {
          var direction = ParentBoss.GlobalPosition.DirectionTo(PlayerNode.GlobalPosition);
          ParentBoss.Velocity = direction * BossChaseSpeed;
        }
        _orbitAngle += OrbitSpeed * scaledDelta;
        UpdateOrbitingBulletPosition();
        break;
    }

    // 将 Boss 的速度传递给所有液晶分子
    foreach (var crystal in _activeCrystals) {
      if (IsInstanceValid(crystal)) {
        crystal.BossVelocity = ParentBoss.Velocity;
      }
    }

    // 处理玩家轨迹和子弹清理
    HandlePlayerTrail(scaledDelta);
    HandleTrailClearing();
  }

  public override void _PhysicsProcess(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;
    ParentBoss.MoveAndSlide();
  }

  private void SpawnCrystals() {
    if (LiquidCrystalBulletScene == null) return;

    float halfWidth = _mapGenerator.MapWidth * _mapGenerator.TileSize / 2f;
    float halfHeight = _mapGenerator.MapHeight * _mapGenerator.TileSize / 2f;

    float minX = float.MaxValue, minY = float.MaxValue;
    float maxX = float.MinValue, maxY = float.MinValue;

    var playerPosition = GameRootProvider.CurrentGameRoot.GetNode<Player>("Player").GlobalPosition;

    // 遍历所有生成点
    for (float y = -halfHeight; y <= halfHeight; y += BulletSpacing) {
      for (float x = -halfWidth; x <= halfWidth; x += BulletSpacing) {
        var pos = new Vector2(x, y);
        if ((playerPosition - pos).Length() < BulletSpacing / 2) {
          continue;
        }

        var crystal = LiquidCrystalBulletScene.Instantiate<PhaseLiquidCrystalBullet>();
        crystal.TimeScaleSensitivity = TimeScaleSensitivity;
        crystal.GlobalPosition = new Vector2(x, y);
        crystal.BossInfluence = BossInfluenceOnCrystals;
        GameRootProvider.CurrentGameRoot.AddChild(crystal);
        _activeCrystals.Add(crystal);

        // 根据初始位置更新边界
        minX = Mathf.Min(minX, x);
        minY = Mathf.Min(minY, y);
        maxX = Mathf.Max(maxX, x);
        maxY = Mathf.Max(maxY, y);
      }
    }

    // 如果成功生成了子弹，则计算最终的边界矩形
    if (_activeCrystals.Count > 0) {
      float margin = BulletSpacing / 2.0f;
      _crystalBounds = new Rect2(
        minX - margin,
        minY - margin,
        (maxX - minX) + BulletSpacing,
        (maxY - minY) + BulletSpacing
      );
      // 将边界信息传递给所有液晶子弹
      foreach (var crystal in _activeCrystals) {
        crystal.SetBounds(_crystalBounds);
      }
    }
  }

  private void SpawnOrbitingBullet() {
    if (OrbitingBulletScene == null) return;
    _orbitingBullet = OrbitingBulletScene.Instantiate<BaseBullet>();
    GameRootProvider.CurrentGameRoot.AddChild(_orbitingBullet);
    _orbitingBullet.GlobalPosition = ParentBoss.GlobalPosition;
  }

  private void UpdateOrbitingBulletPosition() {
    if (!IsInstanceValid(_orbitingBullet)) return;
    var offset = Vector2.Right.Rotated(_orbitAngle) * OrbitRadius;
    _orbitingBullet.GlobalPosition = ParentBoss.GlobalPosition + offset;
  }

  private void HandlePlayerTrail(float scaledDelta) {
    if (!IsInstanceValid(PlayerNode)) return;
    var currentPlayerPos = PlayerNode.GlobalPosition;

    if (currentPlayerPos.DistanceTo(_lastPlayerPosition) > TrailMinDistance) {
      SpawnTrailBullet(_lastPlayerPosition);
      _lastPlayerPosition = currentPlayerPos;
    }
  }

  private void SpawnTrailBullet(Vector2 position) {
    if (TrailBulletScene == null) return;
    var bullet = TrailBulletScene.Instantiate<BaseBullet>();
    bullet.GlobalPosition = position;
    GameRootProvider.CurrentGameRoot.AddChild(bullet);
    _trailBullets.Add(bullet);
  }

  private void HandleTrailClearing() {
    if (!IsInstanceValid(_orbitingBullet)) return;
    var orbiterPos = _orbitingBullet.GlobalPosition;

    // 从后向前遍历以安全地移除元素
    for (int i = _trailBullets.Count - 1; i >= 0; i--) {
      var trailBullet = _trailBullets[i];
      if (!IsInstanceValid(trailBullet)) {
        _trailBullets.RemoveAt(i);
        continue;
      }
      if (trailBullet.GlobalPosition.DistanceTo(orbiterPos) < TrailClearanceRadius) {
        trailBullet.Destroy();
        _trailBullets.RemoveAt(i);
      }
    }
  }

  public override RewindState CaptureInternalState() {
    return new PhaseLiquidCrystalState {
      CurrentState = this._currentState,
      Timer = this._timer,
      OrbitAngle = this._orbitAngle,
      LastPlayerPosition = this._lastPlayerPosition,
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseLiquidCrystalState plc) return;
    this._currentState = plc.CurrentState;
    this._timer = plc.Timer;
    this._orbitAngle = plc.OrbitAngle;
    this._lastPlayerPosition = plc.LastPlayerPosition;
  }
}
