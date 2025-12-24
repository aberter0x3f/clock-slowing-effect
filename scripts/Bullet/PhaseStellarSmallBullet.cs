using Godot;
using Rewind;

namespace Bullet;

public class PhaseStellarSmallBulletState : BaseBulletState {
  public PhaseStellarSmallBullet.State CurrentState;
  public bool IsActive;
  public float SupernovaTimer;
}

public partial class PhaseStellarSmallBullet : BaseBullet {
  [Signal] public delegate void ReachedTargetEventHandler(int ringIndex);

  public enum BulletColor { Blue, Green, Yellow, Orange, Red }
  public enum BulletType { BlackHole, Supernova }
  public enum State { Inactive, MovingToTarget, WaitingForRingCompletion, Rotating, SupernovaSeeking, SupernovaHoming, BlackHoleAccretion }

  [ExportGroup("Sprites")]
  [Export] public SpriteFrames BlueSprite { get; set; }
  [Export] public SpriteFrames GreenSprite { get; set; }
  [Export] public SpriteFrames YellowSprite { get; set; }
  [Export] public SpriteFrames OrangeSprite { get; set; }
  [Export] public SpriteFrames RedSprite { get; set; }

  [Export] public BulletType Type { get; set; }
  public State CurrentState { get; private set; } = State.Inactive;
  public BulletColor CurrentColor { get; set; }
  public Vector3 TargetPosition { get; set; }
  public bool DropWhenArrive { get; set; }
  public float MoveSpeed { get; set; }
  public int RingIndex { get; set; }
  public float RingRotationSpeed { get; set; }
  public float SupernovaDelay { get; set; }
  public float FireAngle { get; set; }

  private Vector3 _velocity;
  private float _supernovaTimer = 0f;
  private AnimatedSprite3D _animatedSprite;

  public override void _Ready() {
    base._Ready();
    _animatedSprite = _sprite as AnimatedSprite3D;
    SetProcess(false);
    Visible = false;
    UpdateSpriteForColor();
  }

  public void Activate(Vector3 startPosition) {
    if (CurrentState != State.Inactive) return;

    GlobalPosition = startPosition;
    _velocity = (TargetPosition - GlobalPosition).Normalized() * MoveSpeed;
    CurrentState = State.MovingToTarget;

    SetProcess(true);
    Visible = true;
  }

  public override void UpdateBullet(float scaledDelta) {
    if (IsDestroyed || RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding) return;

    switch (CurrentState) {
      case State.MovingToTarget:
        GlobalPosition = GlobalPosition.MoveToward(TargetPosition, MoveSpeed * scaledDelta);
        if (GlobalPosition.DistanceSquaredTo(TargetPosition) < 0.0001f) {
          GlobalPosition = TargetPosition;
          CurrentState = State.WaitingForRingCompletion;
          if (DropWhenArrive)
            Destroy();
          else {
            EmitSignal(SignalName.ReachedTarget, RingIndex);
          }
        }
        break;

      case State.Rotating:
        GlobalPosition = GlobalPosition.Rotated(Vector3.Up, RingRotationSpeed * scaledDelta);
        break;

      case State.SupernovaSeeking:
        GlobalPosition = GlobalPosition.Rotated(Vector3.Up, RingRotationSpeed * scaledDelta);
        _supernovaTimer -= scaledDelta;
        if (_supernovaTimer <= 0) {
          SwitchToSupernovaHoming();
        }
        break;

      case State.SupernovaHoming:
        GlobalPosition += _velocity * scaledDelta;
        _supernovaTimer -= scaledDelta;
        if (_supernovaTimer <= 0) {
          Destroy();
        }
        break;

      case State.BlackHoleAccretion:
        GlobalPosition = GlobalPosition.Rotated(Vector3.Up, RingRotationSpeed * scaledDelta);
        _sprite.Modulate = Colors.Black;
        break;
    }
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
    _supernovaTimer = 4f;
    var player = GameRootProvider.CurrentGameRoot.GetNode<Player>("Player");
    var target = player.DecoyTarget ?? player;
    if (IsInstanceValid(target)) {
      var dir = (target.GlobalPosition - GlobalPosition).Normalized();
      dir = dir.Rotated(Vector3.Up, (float) GD.Randfn(0, 0.1));
      if (GD.Randi() % 2 == 0) dir *= -1;
      _velocity = dir * MoveSpeed;
    }
  }

  public void SwitchToBlackHole() {
    CurrentState = State.BlackHoleAccretion;
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
    var bs = (BaseBulletState) base.CaptureState();
    return new PhaseStellarSmallBulletState {
      GlobalPosition = bs.GlobalPosition,
      GlobalRotation = bs.GlobalRotation,
      WasGrazed = bs.WasGrazed,
      IsGrazing = bs.IsGrazing,
      Modulate = bs.Modulate,
      TimeAlive = bs.TimeAlive,
      CurrentState = this.CurrentState,
      IsActive = this.IsProcessing(),
      SupernovaTimer = this._supernovaTimer,
    };
  }

  public override void RestoreState(RewindState state) {
    base.RestoreState(state);
    if (state is not PhaseStellarSmallBulletState s) return;
    CurrentState = s.CurrentState;
    _supernovaTimer = s.SupernovaTimer;
    SetProcess(s.IsActive);
    Visible = s.IsActive;
  }
}
