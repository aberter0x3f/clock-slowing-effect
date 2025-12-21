using Godot;

/// <summary>
/// 一个可交互的菜单选项，用于主菜单场景．
/// </summary>
[GlobalClass]
public partial class TitleMenuInteractable : Area3D, IInteractable {
  public enum ActionType {
    StartGame,
    BossPractice,
    Settings,
    ExitGame
  }

  [Signal]
  public delegate void StartGameRequestedEventHandler();
  [Signal]
  public delegate void BossPracticeRequestedEventHandler();

  [Export]
  public ActionType Action { get; set; } = ActionType.StartGame;

  private Label3D _label;

  public override void _Ready() {
    _label = GetNode<Label3D>("Label3D");
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

      case ActionType.BossPractice:
        EmitSignal(SignalName.BossPracticeRequested);
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
