using Godot;

public partial class Obstacle : Node2D {
  private Node3D _visualizer;

  public int TileSize { get; set; }

  public override void _Ready() {
    _visualizer = GetNode<Node3D>("Node3D");
    UpdateVisualizer();
  }

  private void UpdateVisualizer() {
    _visualizer.GlobalPosition = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      TileSize / 2 * GameConstants.WorldScaleFactor,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );
  }
}
