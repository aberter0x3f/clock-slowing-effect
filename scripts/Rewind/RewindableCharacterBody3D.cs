using Godot;

namespace Rewind;

public abstract partial class RewindableCharacterBody3D : CharacterBody3D, IRewindable {
  public ulong InstanceId => GetInstanceId();
  public bool IsDestroyed { get; private set; } = false;
  private CollisionShape3D _collisionShape;

  public override void _Ready() {
    base._Ready();
    RewindManager.Instance.Register(this);
    _collisionShape = GetNode<CollisionShape3D>("CollisionShape3D");
  }

  public abstract RewindState CaptureState();
  public abstract void RestoreState(RewindState state);

  public virtual void Destroy() {
    if (IsDestroyed) return;
    IsDestroyed = true;
    RewindManager.Instance.NotifyDestroyed(this);

    // 禁用节点而不是删除它
    SetProcess(false);
    SetPhysicsProcess(false);
    Visible = false;
    _collisionShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, true);
  }

  public virtual void Resurrect() {
    if (!IsDestroyed) return;
    IsDestroyed = false;
    RewindManager.Instance.Register(this); // 重新注册，因为它现在是「活的」

    // 重新启用节点
    SetProcess(true);
    SetPhysicsProcess(true);
    Visible = true;
    _collisionShape.SetDeferred(CollisionShape3D.PropertyName.Disabled, false);
  }

  public override void _ExitTree() {
    if (RewindManager.Instance != null) {
      RewindManager.Instance.Unregister(this);
    }
    base._ExitTree();
  }
}
