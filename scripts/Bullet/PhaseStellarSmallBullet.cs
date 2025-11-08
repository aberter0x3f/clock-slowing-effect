using Godot;
using Rewind;

namespace Bullet;

public class PhaseStellarSmallBulletState : BaseBulletState {
  public PhaseStellarSmallBullet.State CurrentState;
  public bool IsActive;
  public float SupernovaTimer;
}

public partial class PhaseStellarSmallBullet : BaseBullet {
  [Signal]
  public delegate void ReachedTargetEventHandler(int ringIndex);

  public enum BulletColor {
    Blue,
    Green,
    Yellow,
    Orange,
    Red
  }

  public enum BulletType {
    BlackHole,
    Supernova
  }

  public enum State {
    Inactive,
    MovingToTarget,
    WaitingForRingCompletion,
    Rotating,
    SupernovaSeeking,
    SupernovaHoming,
    BlackHoleAccretion
  }

  [ExportGroup("Sprites")]
  [Export] public SpriteFrames BlueSprite { get; set; }
  [Export] public SpriteFrames GreenSprite { get; set; }
  [Export] public SpriteFrames YellowSprite { get; set; }
  [Export] public SpriteFrames OrangeSprite { get; set; }
  [Export] public SpriteFrames RedSprite { get; set; }

  [Export]
  public BulletType Type { get; set; }
  public State CurrentState { get; private set; } = State.Inactive;
  public BulletColor CurrentColor { get; set; }
  public Vector2 TargetPosition { get; set; }
  public float MoveSpeed { get; set; }
  public int RingIndex { get; set; }
  public float RingRotationSpeed { get; set; }
  public float SupernovaDelay { get; set; }
  public float FireAngleOffset { get; set; }
  public float TimeScaleSensitivity { get; set; }

  private Vector2 _velocity;
  private Player _playerNode;
  private float _supernovaTimer = 0f;
  private AnimatedSprite3D _animatedSprite;

  public override void _Ready() {
    base._Ready();
    // _animatedSprite = GetNode<AnimatedSprite3D>("Visualizer/Sprite");
    _animatedSprite = _sprite as AnimatedSprite3D;
    SetProcess(false);
    Visible = false;
    _playerNode = GameRootProvider.CurrentGameRoot.GetNode<Player>("Player");
    UpdateSpriteForColor();
  }

  public void Activate(Vector2 startPosition) {
    if (CurrentState != State.Inactive) return;

    GlobalPosition = startPosition;
    _velocity = (TargetPosition - GlobalPosition).Normalized() * MoveSpeed;
    CurrentState = State.MovingToTarget;

    SetProcess(true);
    Visible = true;
  }

  public override void _Process(double delta) {
    base._Process(delta);
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;

    switch (CurrentState) {
      case State.MovingToTarget:
        GlobalPosition = GlobalPosition.MoveToward(TargetPosition, MoveSpeed * scaledDelta);
        if (GlobalPosition.DistanceSquaredTo(TargetPosition) < 1f) {
          GlobalPosition = TargetPosition;
          CurrentState = State.WaitingForRingCompletion;
          EmitSignal(SignalName.ReachedTarget, RingIndex);
        }
        break;

      case State.Rotating:
      case State.BlackHoleAccretion:
        GlobalPosition = GlobalPosition.Rotated(RingRotationSpeed * scaledDelta);
        break;

      case State.SupernovaSeeking:
        // 继续旋转
        GlobalPosition = GlobalPosition.Rotated(RingRotationSpeed * scaledDelta);
        // 更新计时器并检查是否到了该变轨的时间
        _supernovaTimer -= scaledDelta;
        if (_supernovaTimer <= 0) {
          SwitchToSupernovaHoming();
        }
        break;

      case State.SupernovaHoming:
        GlobalPosition += _velocity * scaledDelta;
        break;
    }
    UpdateVisualizer();
  }

  public void StartRotation() {
    if (CurrentState == State.WaitingForRingCompletion) {
      CurrentState = State.Rotating;
    }
  }

  /// <summary>
  /// 触发超新星模式．
  /// </summary>
  public void SwitchToSupernova() {
    if (CurrentState == State.Rotating) {
      CurrentState = State.SupernovaSeeking;
      _supernovaTimer = SupernovaDelay;
    }
  }

  private void SwitchToSupernovaHoming() {
    CurrentState = State.SupernovaHoming;
    var target = _playerNode.DecoyTarget ?? _playerNode;
    if (IsInstanceValid(target)) {
      var dir = (target.GlobalPosition - GlobalPosition).Normalized();
      dir = dir.Rotated((float) GD.Randfn(0, 0.1));
      if (GD.Randi() % 2 == 0) {
        dir *= -1;
      }
      _velocity = dir * MoveSpeed;
    }
  }

  public void SwitchToBlackHole() {
    CurrentState = State.BlackHoleAccretion;
    _sprite.Modulate = Colors.Black;
  }

  /// <summary>
  /// 在生成时设置子弹的初始颜色．
  /// </summary>
  public void SetColor(BulletColor color) {
    CurrentColor = color;
    UpdateSpriteForColor();
  }

  private void UpdateSpriteForColor() {
    _animatedSprite.SpriteFrames = CurrentColor switch {
      BulletColor.Blue => BlueSprite,
      BulletColor.Green => GreenSprite,
      BulletColor.Yellow => YellowSprite,
      BulletColor.Orange => OrangeSprite,
      BulletColor.Red => RedSprite,
      _ => _animatedSprite.SpriteFrames
    };
    _animatedSprite.Play();
  }

  public override RewindState CaptureState() {
    var baseState = (BaseBulletState) base.CaptureState();
    return new PhaseStellarSmallBulletState {
      GlobalPosition = baseState.GlobalPosition,
      GlobalRotation = baseState.GlobalRotation,
      WasGrazed = baseState.WasGrazed,
      Modulate = baseState.Modulate,
      CurrentState = this.CurrentState,
      IsActive = this.IsProcessing(),
      SupernovaTimer = this._supernovaTimer,
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not PhaseStellarSmallBulletState sss) return;
    this.CurrentState = sss.CurrentState;
    this._supernovaTimer = sss.SupernovaTimer;

    bool shouldBeActive = sss.IsActive;
    if (shouldBeActive) {
      SetProcess(true);
      Visible = true;
    } else if (!shouldBeActive) {
      SetProcess(false);
      Visible = false;
    }
  }
}
