using Godot;

namespace Rewind;

public abstract partial class RewindableArea2D : Area2D, IRewindable {
  public ulong InstanceId => GetInstanceId();
  protected bool IsDestroyed { get; private set; } = false;
  private CollisionShape2D _collisionShape;
  private Node3D _visualizer;

  public override void _Ready() {
    base._Ready();
    RewindManager.Instance.Register(this);
    _collisionShape = GetNode<CollisionShape2D>("CollisionShape2D");
    _visualizer = GetNodeOrNull<Node3D>("Visualizer");
  }

  public abstract RewindState CaptureState();
  public abstract void RestoreState(RewindState state);

  public virtual void Destroy() {
    if (IsDestroyed) return;
    IsDestroyed = true;
    RewindManager.Instance.NotifyDestroyed(this);

    SetProcess(false);
    Visible = false;
    if (_visualizer != null)
      _visualizer.Visible = false;
    _collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, true);
  }

  public virtual void Resurrect() {
    if (!IsDestroyed) return;
    IsDestroyed = false;
    RewindManager.Instance.Register(this);

    SetProcess(true);
    Visible = true;
    if (_visualizer != null)
      _visualizer.Visible = true;
    _collisionShape.SetDeferred(CollisionShape2D.PropertyName.Disabled, false);
  }

  public override void _ExitTree() {
    if (RewindManager.Instance != null) {
      RewindManager.Instance.Unregister(this);
    }
    base._ExitTree();
  }
}
