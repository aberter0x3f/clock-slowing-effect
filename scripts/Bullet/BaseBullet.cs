using Enemy;
using Godot;
using Rewind;

namespace Bullet;

// 状态快照基类
public class BaseBulletState : RewindState {
  public Vector2 GlobalPosition;
  public float GlobalRotation;
  public bool WasGrazed;
  public Color Modulate;
}

public partial class BaseBullet : RewindableArea2D {
  public static readonly Color GRAZE_COLOR = new Color(1.0f, 0.5f, 0.5f);

  [Export]
  public bool IsPlayerBullet { get; set; } = false;

  [Export]
  public float Damage { get; set; } = 0f;

  // 是否被擦弹
  public bool WasGrazed { get; set; } = false;

  private Color _originalColor; // 用于存储子弹的原始颜色
  protected Node3D _visualizer;
  protected SpriteBase3D _sprite;

  public override void _Ready() {
    base._Ready();
    _visualizer = GetNodeOrNull<Node3D>("Visualizer");
    _sprite = _visualizer.GetNode<SpriteBase3D>("Sprite");
    _originalColor = _sprite.Modulate;
    UpdateVisualizer();
  }

  public virtual void OnGrazeEnter() {
    _sprite.Modulate = GRAZE_COLOR;
  }

  public virtual void OnGrazeExit() {
    _sprite.Modulate = _originalColor;
  }

  private void OnBodyEntered(Node2D body) {
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
    // 子类需要调用 base._Process(delta)
    // 但由于我们在这里处理了回溯检查，所以子类不需要再检查
    UpdateVisualizer();
  }

  protected virtual void UpdateVisualizer() {
    if (_visualizer != null) {
      _visualizer.GlobalPosition = new Vector3(
        GlobalPosition.X * GameConstants.WorldScaleFactor,
        GameConstants.GamePlaneY,
        GlobalPosition.Y * GameConstants.WorldScaleFactor
      );
      _visualizer.Rotation = new Vector3(0, -GlobalRotation, 0);
    }
  }

  public override RewindState CaptureState() {
    return new BaseBulletState {
      GlobalPosition = this.GlobalPosition,
      GlobalRotation = this.GlobalRotation,
      WasGrazed = this.WasGrazed,
      Modulate = _sprite.Modulate
    };
  }

  public override void RestoreState(RewindState state) {
    if (state is not BaseBulletState bbs) return;
    this.GlobalPosition = bbs.GlobalPosition;
    this.GlobalRotation = bbs.GlobalRotation;
    this.WasGrazed = bbs.WasGrazed;
    if (_sprite != null) {
      _sprite.Modulate = bbs.Modulate;
    }
  }
}
