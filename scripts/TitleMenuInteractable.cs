using Godot;

[GlobalClass]
public partial class TitleMenuInteractable : Area3D, IInteractable {
  public enum ActionType {
    StartGame,
    BossPractice,
    SelectWeapon,
    ExitGame
  }

  [Signal] public delegate void StartGameRequestedEventHandler();
  [Signal] public delegate void BossPracticeRequestedEventHandler();
  [Signal] public delegate void WeaponSelectionRequestedEventHandler();

  [Export]
  public ActionType Action { get; set; } = ActionType.StartGame;

  private Label3D _label;

  public override void _Ready() {
    _label = GetNode<Label3D>("Label3D");
  }

  public void SetHighlight(bool highlighted) {
    if (_label == null) return;
    if (highlighted) {
      _label.Modulate = new Color(1.0f, 1.0f, 0.5f);
    } else {
      _label.Modulate = new Color(1.0f, 1.0f, 1.0f);
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
      case ActionType.SelectWeapon:
        EmitSignal(SignalName.WeaponSelectionRequested);
        break;
      case ActionType.ExitGame:
        GetTree().Quit();
        break;
    }
  }
}
