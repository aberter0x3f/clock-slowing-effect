using Enemy;
using Godot;
using Rewind;

namespace Bullet;

public class BaseBulletState : RewindState {
  public Vector3 GlobalPosition;
  public Vector3 GlobalRotation;
  public bool WasGrazed;
  public bool IsGrazing;
  public Color Modulate;
  public bool LandingIndicatorVisible;
  public Vector3 LandingIndicatorScale;
  public float TimeAlive;
}

public partial class BaseBullet : RewindableArea3D {
  public static readonly Color GRAZE_COLOR = new Color(1.0f, 0.5f, 0.5f);

  [ExportGroup("Basics")]
  [Export]
  public bool IsPlayerBullet { get; set; } = false;

  [Export]
  public float Damage { get; set; } = 0f;

  [ExportGroup("Indicator")]
  [Export]
  public float IndicatorStartHeight { get; set; } = 2.0f;
  public float IndicatorEndHeight { get; set; } = 0.07f;
  [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
  public float IndicatorMinScale { get; set; } = 0.2f; // 指示器的最小缩放比例．

  [ExportGroup("Time")]
  [Export(PropertyHint.Range, "0.0, 1.0, 0.01")]
  public float TimeScaleSensitivity { get; set; } = 1.0f; // 时间缩放敏感度．0=完全忽略, 1=完全受影响．
  [Export]
  public float MaxLifetime { get; set; } = 10.0f;

  public bool WasGrazed { get; set; } = false;
  public bool IsGrazing { get; set; } = false;
  public float TimeAlive { get; protected set; } = 0.0f;

  protected SpriteBase3D _sprite;
  protected Node3D _landingIndicator;
  protected bool _hasIndicator = false;

  public override void _Ready() {
    base._Ready();
    _sprite = GetNode<SpriteBase3D>("Sprite");
    _landingIndicator = GetNodeOrNull<Node3D>("LandingIndicator");
    _hasIndicator = IsInstanceValid(_landingIndicator);
    _sprite.Modulate = Colors.White;
    UpdateBullet(0);
    UpdateVisualizer();
  }

  public virtual void OnGrazeEnter() {
    IsGrazing = true;
  }

  public virtual void OnGrazeExit() {
    IsGrazing = false;
  }

  private void OnBodyEntered(Node3D body) {
    if (!IsPlayerBullet || !body.IsInGroup("enemies")) return;
    var enemy = body as BaseEnemy;
    enemy.TakeDamage(Damage);
    Destroy();
  }

  public override void _Process(double delta) {
    if (RewindManager.Instance.IsPreviewing) {
      UpdateVisualizer();
      return;
    }
    if (RewindManager.Instance.IsRewinding) return;
    _sprite.Modulate = Colors.White;

    float effectiveTimeScale = Mathf.Lerp(1.0f, TimeManager.Instance.TimeScale, TimeScaleSensitivity);
    var scaledDelta = (float) delta * effectiveTimeScale;
    UpdateBullet(scaledDelta);

    UpdateVisualizer();
  }

  public virtual void UpdateBullet(float scaledDelta) {
    TimeAlive += scaledDelta;
    if (TimeAlive > MaxLifetime) {
      Destroy();
      return;
    }
  }

  public virtual void UpdateVisualizer() {
    if (IsGrazing) _sprite.Modulate *= GRAZE_COLOR;

    if (!_hasIndicator) return;

    _landingIndicator.Visible = GlobalPosition.Y > IndicatorEndHeight;
    if (!_landingIndicator.Visible) return;

    // 计算从 IndicatorStartHeight 到 0 的下落进度．
    float progress = 1.0f - Mathf.Clamp((GlobalPosition.Y - IndicatorEndHeight) / (IndicatorStartHeight - IndicatorEndHeight), 0.0f, 1.0f);

    // 根据进度在最小和最大 (1.0) 缩放之间进行插值
    float currentScale = Mathf.Lerp(IndicatorMinScale, 1.0f, progress);
    _landingIndicator.Scale = new Vector3(currentScale, currentScale, currentScale);

    _landingIndicator.GlobalPosition = GlobalPosition with { Y = 0 };
  }

  public override RewindState CaptureState() => new BaseBulletState {
    GlobalPosition = this.GlobalPosition,
    GlobalRotation = this.GlobalRotation,
    WasGrazed = this.WasGrazed,
    IsGrazing = this.IsGrazing,
    Modulate = _sprite.Modulate,
    TimeAlive = this.TimeAlive,
  };

  public override void RestoreState(RewindState state) {
    if (state is not BaseBulletState bbs) return;
    this.GlobalPosition = bbs.GlobalPosition;
    this.GlobalRotation = bbs.GlobalRotation;
    this.WasGrazed = bbs.WasGrazed;
    this.IsGrazing = bbs.IsGrazing;
    _sprite.Modulate = bbs.Modulate;
    this.TimeAlive = bbs.TimeAlive;
  }
}
