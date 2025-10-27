using Godot;

/// <summary>
/// 一个可交互的菜单选项，用于主菜单场景．
/// </summary>
[GlobalClass]
public partial class TitleMenuInteractable : Area2D, IInteractable {
  public enum ActionType {
    StartGame,
    ChangeCharacter,
    Settings,
    ExitGame
  }

  [Signal]
  public delegate void StartGameRequestedEventHandler();

  [Export]
  public ActionType Action { get; set; } = ActionType.StartGame;

  private Label3D _label;
  private Node3D _visualizer;

  public override void _Ready() {
    _visualizer = GetNode<Node3D>("Visualizer");
    _label = _visualizer.GetNode<Label3D>("Label3D");

    _visualizer.GlobalPosition = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY + 0.2f,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );
  }

  public void SetHighlight(bool highlighted) {
    if (_label == null) return;

    if (highlighted) {
      _label.Modulate = new Color(1.0f, 1.0f, 0.5f); // 高亮时变为黄色
    } else {
      _label.Modulate = new Color(1.0f, 1.0f, 1.0f); // 恢复白色
    }
  }

  public void Interact() {
    switch (Action) {
      case ActionType.StartGame:
        EmitSignal(SignalName.StartGameRequested);
        break;

      case ActionType.ChangeCharacter:
        GD.Print("Action: Change Character (Not Implemented)");
        break;

      case ActionType.Settings:
        GD.Print("Action: Settings (Not Implemented)");
        break;

      case ActionType.ExitGame:
        GetTree().Quit();
        break;
    }
  }
}
