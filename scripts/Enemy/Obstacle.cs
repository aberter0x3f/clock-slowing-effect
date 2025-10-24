using Godot;

public partial class Obstacle : Node2D {
  private Node3D _visualizer;

  public override void _Ready() {
    _visualizer = GetNode<Node3D>("Node3D");
    UpdateVisualizer();
  }

  private void UpdateVisualizer() {
    _visualizer.GlobalPosition = new Vector3(GlobalPosition.X * 0.01f, 0.16f, GlobalPosition.Y * 0.01f);
  }
}
