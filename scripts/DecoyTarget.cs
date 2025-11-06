using Godot;

public partial class DecoyTarget : Node2D {
  private Node3D _visualizer;

  public int TileSize { get; set; }

  public override void _Ready() {
    _visualizer = GetNode<Node3D>("Node3D");
    UpdateVisualizer();
  }

  private void UpdateVisualizer() {
    _visualizer.GlobalPosition = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );
  }
}
