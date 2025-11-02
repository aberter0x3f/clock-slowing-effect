using System.Collections.Generic;
using Bullet;
using Godot;
using Rewind;

namespace Enemy.Boss;

public class PhaseLiquidCrystalState : BasePhaseState {
  public PhaseLiquidCrystal.PhaseState CurrentState;
  public float Timer;
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
  public PackedScene LeaderBulletScene { get; set; }

  [ExportGroup("Pattern Configuration")]
  [Export]
  public float WaitDuration { get; set; } = 2f;
  [Export]
  public float BulletSpacing { get; set; } = 60f;
  [Export]
  public float LeaderSpeed { get; set; } = 100f;

  [Export(PropertyHint.Range, "0.0, 2.0, 0.01")]
  public float LeaderInfluenceOnCrystals { get; set; } = 0.5f;

  [ExportGroup("")]
  [Export]
  public float TimeScaleSensitivity { get; set; } = 1f;

  private PhaseState _currentState;
  private float _timer;
  private MapGenerator _mapGenerator;
  private readonly List<PhaseLiquidCrystalBullet> _activeCrystals = new();
  private Rect2 _crystalBounds;
  private SimpleBullet _leaderBullet;

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
    LeaderSpeed *= rank / 5f;

    // 在一帧内生成所有子弹并计算精确边界
    SpawnCrystals();

    // 初始化状态机
    _currentState = PhaseState.Waiting;
    _timer = WaitDuration;
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;
    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;

    _timer -= scaledDelta;

    switch (_currentState) {
      case PhaseState.Waiting:
        if (_timer <= 0) {
          FireLeaderBullet();
          _currentState = PhaseState.Active;
        }
        break;

      case PhaseState.Active:
        if (IsInstanceValid(_leaderBullet) && IsInstanceValid(PlayerNode)) {
          var direction = _leaderBullet.GlobalPosition.DirectionTo(PlayerNode.GlobalPosition);
          _leaderBullet.Velocity = direction * LeaderSpeed;
        }
        break;
    }
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
    }
  }

  private void FireLeaderBullet() {
    if (LeaderBulletScene == null || !IsInstanceValid(PlayerNode)) return;

    _leaderBullet = LeaderBulletScene.Instantiate<SimpleBullet>();
    _leaderBullet.GlobalPosition = ParentBoss.GlobalPosition;
    _leaderBullet.InitialSpeed = LeaderSpeed;
    _leaderBullet.TimeScaleSensitivity = TimeScaleSensitivity;
    GameRootProvider.CurrentGameRoot.AddChild(_leaderBullet);

    // 将领导者和边界信息传递给所有液晶子弹
    foreach (var crystal in _activeCrystals) {
      if (IsInstanceValid(crystal)) {
        crystal.SetLeader(_leaderBullet);
        crystal.SetBounds(_crystalBounds);
        crystal.LeaderInfluence = LeaderInfluenceOnCrystals;
      }
    }
  }

  public override RewindState CaptureInternalState() {
    return new PhaseLiquidCrystalState {
      CurrentState = this._currentState,
      Timer = this._timer,
    };
  }

  public override void RestoreInternalState(RewindState state) {
    base.RestoreInternalState(state);
    if (state is not PhaseLiquidCrystalState plc) return;
    this._currentState = plc.CurrentState;
    this._timer = plc.Timer;
  }
}
