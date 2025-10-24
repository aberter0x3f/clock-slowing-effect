public static class GameConstants {
  /// <summary>
  /// The Y-coordinate in 3D space that represents the 2D game plane.
  /// Most 2D objects' 3D visualizers will be placed at this height.
  /// </summary>
  public const float GamePlaneY = 0.2f;

  /// <summary>
  /// The scaling factor to convert 2D world coordinates to 3D world coordinates.
  /// e.g., a 2D position of (100, 50) becomes a 3D position of (1, GamePlaneY, 0.5).
  /// </summary>
  public const float WorldScaleFactor = 0.01f;
}
