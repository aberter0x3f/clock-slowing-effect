using Godot;
using Rewind;

public class TimeShardState : RewindState {
  public TimeShard.State CurrentState;
  public Vector2 GlobalPosition;
  public float CurrentHeight;
  public float LifetimeTimer;
  public float AnimationTimer;
  public float TimeAppliedToHealth;
  public float TimeAppliedToBond;
}

/// <summary>
/// 代表敌人死亡后掉落的时间碎片．
/// 玩家接触后会获得时间奖励，并触发飞向玩家的视觉效果．
/// </summary>
[GlobalClass]
public partial class TimeShard : RewindableArea2D {
  public enum State {
    Spawning, // 正在生成，从空中飘落
    Idle,     // 落在地上，等待被拾取或超时
    Collected // 已被玩家拾取，正在飞向玩家
  }

  private CollisionShape2D _collisionShape;
  private Node3D _visualizer;
  private Player _targetPlayer;
  private Vector2 _startPosition; // 用于动画的起始位置
  private Vector2 _landingPosition;
  private float _currentHeight;
  private float _lifetimeTimer;
  private float _animationTimer = 0.0f; // 用于手动控制生成动画的计时器
  private float _timeAppliedToHealth;
  private float _timeAppliedToBond;

  [ExportGroup("Shard Properties")]
  [Export]
  public float TimeBonus { get; set; } = 1.0f; // 每个碎片增加的时间
  [Export]
  public float MaxLifetime { get; set; } = 5.0f; // 在地上的最大存在时间
  [Export]
  public float SpreadSigma { get; set; } = 80.0f; // 落地点的分布标准差
  [Export]
  public float BurstHeight { get; set; } = 100.0f; // 爆出时的最大高度
  [Export]
  public float FallDuration { get; set; } = 0.8f; // 飘落动画的持续时间
  [Export]
  public float FlyToPlayerSpeed { get; set; } = 800.0f; // 飞向玩家的速度

  [ExportGroup("Sound Effects")]
  [Export]
  public AudioStream CollectSound { get; set; }

  public State CurrentState { get; private set; } = State.Spawning;
  public Vector2 SpawnCenter { get; set; }
  public MapGenerator MapGeneratorRef { get; set; }

  public override void _Ready() {
    base._Ready();

    _collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
    _visualizer = GetNode<Node3D>("Visualizer");

    _lifetimeTimer = MaxLifetime;

    // 确定一个有效的随机落点
    _landingPosition = FindValidLandingSpot(SpawnCenter, MapGeneratorRef);

    // 将初始 2D 位置设置在生成中心，并记录下来
    GlobalPosition = SpawnCenter;
    _startPosition = SpawnCenter;

    // 这可以处理 TimeShard 生成时玩家就站在其范围内的边缘情况．
    // 为了确保物理服务器已更新，我们延迟一帧执行检查．
    CallDeferred(nameof(CheckInitialOverlap));
  }

  private void CheckInitialOverlap() {
    // 如果在检查时已经被别的逻辑拾取了，就直接返回
    if (CurrentState == State.Collected) return;

    foreach (var body in GetOverlappingBodies()) {
      if (body is Player player) {
        CollectByPlayer(player);
        break;
      }
    }
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing) {
      UpdateVisualizer();
      return;
    }
    if (RewindManager.Instance.IsRewinding) return;

    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    switch (CurrentState) {
      case State.Spawning:
        // 手动处理生成动画，以响应 TimeScale
        _animationTimer += scaledDelta;
        float progress = Mathf.Clamp(_animationTimer / FallDuration, 0.0f, 1.0f);

        // 1. 水平位置插值
        GlobalPosition = _startPosition.Lerp(_landingPosition, progress);

        // 2. 垂直高度模拟抛物线 (先上后下)
        float upDuration = FallDuration / 3.0f;
        if (_animationTimer <= upDuration) {
          _currentHeight = (float) Tween.InterpolateValue(0f, BurstHeight, _animationTimer, upDuration, Tween.TransitionType.Cubic, Tween.EaseType.Out);
        } else {
          float downDuration = FallDuration - upDuration;
          float timeInDownPhase = _animationTimer - upDuration;
          _currentHeight = (float) Tween.InterpolateValue(BurstHeight, -BurstHeight, timeInDownPhase, downDuration, Tween.TransitionType.Cubic, Tween.EaseType.In);
        }

        // 动画结束
        if (progress >= 1.0f) {
          CurrentState = State.Idle;
          GlobalPosition = _landingPosition; // 确保最终位置精确
          _currentHeight = 0;
        }
        break;

      case State.Idle:
        _lifetimeTimer -= scaledDelta;
        if (_lifetimeTimer <= 0) {
          Destroy();
        }
        break;

      case State.Collected:
        if (!IsInstanceValid(_targetPlayer)) {
          Destroy();
          return;
        }
        GlobalPosition = GlobalPosition.MoveToward(_targetPlayer.GlobalPosition, FlyToPlayerSpeed * scaledDelta);
        if (GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition) < 10.0f) {
          Destroy();
        }
        break;
    }

    UpdateVisualizer();
  }

  private void UpdateVisualizer() {
    if (_visualizer != null) {
      var position3D = new Vector3(
        GlobalPosition.X * GameConstants.WorldScaleFactor,
        GameConstants.GamePlaneY,
        GlobalPosition.Y * GameConstants.WorldScaleFactor
      );
      position3D.Y += _currentHeight * GameConstants.WorldScaleFactor;
      _visualizer.GlobalPosition = position3D;
    }
  }

  private void OnBodyEntered(Node2D body) {
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;
    // Spawning 和 Idle 状态都可以被拾取
    if ((CurrentState == State.Spawning || CurrentState == State.Idle) && body is Player player) {
      CollectByPlayer(player);
    }
  }

  public void CollectByPlayer(Player player) {
    if (CurrentState == State.Collected) return;

    SoundManager.Instance.PlaySoundEffect(CollectSound, cooldown: 0.05f);

    var (appliedToBond, appliedToHealth) = GameManager.Instance.AddTime(TimeBonus);
    _timeAppliedToBond = appliedToBond;
    _timeAppliedToHealth = appliedToHealth;

    CurrentState = State.Collected;
    _targetPlayer = player;
    _collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
  }

  private Vector2 FindValidLandingSpot(Vector2 center, MapGenerator mapGenerator) {
    if (mapGenerator == null) {
      GD.PrintErr("TimeShard: MapGenerator not found. Spawning at enemy death location.");
      return center;
    }

    var rnd = new RandomNumberGenerator();
    for (int i = 0; i < 20; ++i) {
      float offsetX = (float) rnd.Randfn(0, SpreadSigma);
      float offsetY = (float) rnd.Randfn(0, SpreadSigma);
      Vector2 potentialPosition = center + new Vector2(offsetX, offsetY);
      Vector2I mapCoords = mapGenerator.WorldToMap(potentialPosition);

      if (mapGenerator.IsWalkable(mapCoords)) {
        return potentialPosition;
      }
    }

    GD.Print("TimeShard: Could not find a valid walkable landing spot after 20 attempts. Spawning at enemy death location.");
    return center;
  }

  public override RewindState CaptureState() {
    return new TimeShardState {
      CurrentState = this.CurrentState,
      GlobalPosition = this.GlobalPosition,
      CurrentHeight = this._currentHeight,
      LifetimeTimer = this._lifetimeTimer,
      AnimationTimer = this._animationTimer,
      TimeAppliedToBond = this._timeAppliedToBond,
      TimeAppliedToHealth = this._timeAppliedToHealth
    };
  }

  public override void RestoreState(RewindState state) {
    if (state is not TimeShardState tss) return;

    bool wasCollected = (this.CurrentState == State.Collected || this.IsDestroyed) && IsInstanceValid(_targetPlayer);
    bool isNowIdleOrSpawning = (tss.CurrentState == State.Idle || tss.CurrentState == State.Spawning);

    if (wasCollected && isNowIdleOrSpawning) {
      var gm = GameManager.Instance;
      // 精确地撤销时间和债券的增加
      gm.CurrentPlayerHealth -= _timeAppliedToHealth;
      gm.TimeBond += _timeAppliedToBond;
      _targetPlayer = null;
    }

    this.CurrentState = tss.CurrentState;
    this.GlobalPosition = tss.GlobalPosition;
    this._currentHeight = tss.CurrentHeight;
    this._lifetimeTimer = tss.LifetimeTimer;
    this._animationTimer = tss.AnimationTimer;
    this._timeAppliedToBond = tss.TimeAppliedToBond;
    this._timeAppliedToHealth = tss.TimeAppliedToHealth;

    _collisionShape.Disabled = CurrentState == State.Collected;
  }
}
