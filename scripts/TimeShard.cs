using Godot;

/// <summary>
/// 代表敌人死亡后掉落的时间碎片．
/// 玩家接触后会获得时间奖励，并触发飞向玩家的视觉效果．
/// </summary>
[GlobalClass]
public partial class TimeShard : Area2D {
  private enum State {
    Spawning, // 正在生成，从空中飘落
    Idle,     // 落在地上，等待被拾取或超时
    Collected // 已被玩家拾取，正在飞向玩家
  }

  private State _currentState = State.Spawning;
  private CollisionShape2D _collisionShape;
  private Node3D _visualizer;
  private Player _targetPlayer;
  private Vector2 _startPosition; // 用于动画的起始位置
  private Vector2 _landingPosition;
  private float _currentHeight;
  private float _lifetimeTimer;
  private float _animationTimer = 0.0f; // 用于手动控制生成动画的计时器

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

  public Vector2 SpawnCenter { get; set; }
  public MapGenerator MapGeneratorRef { get; set; }

  public override void _Ready() {
    _collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
    _visualizer = GetNode<Node3D>("Visualizer");

    // 初始时禁用碰撞，直到它落地
    _collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
    _lifetimeTimer = MaxLifetime;

    // 确定一个有效的随机落点
    _landingPosition = FindValidLandingSpot(SpawnCenter, MapGeneratorRef);

    // 将初始 2D 位置设置在生成中心，并记录下来
    GlobalPosition = SpawnCenter;
    _startPosition = SpawnCenter;
  }

  public override void _Process(double delta) {
    var scaledDelta = (float) delta * TimeManager.Instance.TimeScale;

    switch (_currentState) {
      case State.Spawning:
        // 手动处理生成动画，以响应 TimeScale
        _animationTimer += scaledDelta;
        float progress = Mathf.Clamp(_animationTimer / FallDuration, 0.0f, 1.0f);

        // 1. 水平位置插值
        GlobalPosition = _startPosition.Lerp(_landingPosition, progress);

        // 2. 垂直高度模拟抛物线 (先上后下)
        // 动画分为两部分：上升 (前 1/3 时间) 和下降 (后 2/3 时间)
        float upDuration = FallDuration / 3.0f;
        if (_animationTimer <= upDuration) {
          // 上升阶段
          _currentHeight = (float) Tween.InterpolateValue(
              0f, // initialValue
              BurstHeight, // deltaValue (final - initial)
              _animationTimer, // elapsedTime
              upDuration, // duration
              Tween.TransitionType.Cubic,
              Tween.EaseType.Out
          );
        } else {
          // 下降阶段
          float downDuration = FallDuration - upDuration;
          float timeInDownPhase = _animationTimer - upDuration;
          _currentHeight = (float) Tween.InterpolateValue(
              BurstHeight, // initialValue
              -BurstHeight, // deltaValue (0 - BurstHeight)
              timeInDownPhase, // elapsedTime
              downDuration, // duration
              Tween.TransitionType.Cubic,
              Tween.EaseType.In
          );
        }

        // 动画结束
        if (progress >= 1.0f) {
          _currentState = State.Idle;
          _collisionShape.Disabled = false;
          GlobalPosition = _landingPosition; // 确保最终位置精确
          _currentHeight = 0;
        }
        break;

      case State.Idle:
        _lifetimeTimer -= scaledDelta;
        if (_lifetimeTimer <= 0) {
          QueueFree();
        }
        break;

      case State.Collected:
        if (!IsInstanceValid(_targetPlayer)) {
          QueueFree();
          return;
        }
        GlobalPosition = GlobalPosition.MoveToward(_targetPlayer.GlobalPosition, FlyToPlayerSpeed * scaledDelta);
        if (GlobalPosition.DistanceTo(_targetPlayer.GlobalPosition) < 10.0f) {
          QueueFree();
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
    if (_currentState == State.Idle && body is Player player) {
      player.Health += TimeBonus;
      _currentState = State.Collected;
      _targetPlayer = player;
      _collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
    }
  }

  private Vector2 FindValidLandingSpot(Vector2 center, MapGenerator mapGenerator) {
    if (mapGenerator == null) {
      GD.PrintErr("TimeShard: MapGenerator not found. Spawning at enemy death location.");
      return center;
    }

    var rnd = new RandomNumberGenerator();
    for (int i = 0; i < 20; i++) {
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
}
