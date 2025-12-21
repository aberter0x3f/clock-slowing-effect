using System;
using Event;
using Godot;
using UI;

public partial class EventDevice : Area3D, IInteractable {
  [Signal]
  public delegate void EventResolvedEventHandler();

  private Label3D _label;
  private EventMenu _eventMenuInstance;
  private bool _hasBeenUsed = false;
  private RandomNumberGenerator _rng;
  private GameEvent _initialEventState;

  public PackedScene EventMenuScene { get; set; }
  public PackedScene UpgradeSelectionMenuScene { get; set; }
  public string CombatScenePath { get; set; }
  public ulong LevelSeed { get; set; }

  public override void _Ready() {
    _label = GetNode<Label3D>("Label3D");

    _rng = new RandomNumberGenerator();
    _rng.Seed = LevelSeed;

    if (GameManager.Instance.ActiveEvent == null) {
      // 如果没有，获取一个新事件，初始化它，并将其存入 GameManager
      var newEvent = GameManager.Instance.GetNextEvent();
      newEvent.Initialize(_rng);
      GameManager.Instance.ActiveEvent = newEvent;
    }

    _initialEventState = (GameEvent) GameManager.Instance.ActiveEvent.DuplicateDeep();

    // 子战斗返回后，如果事件已完成，解决事件
    if (GameManager.Instance.ActiveEvent.IsFinished) {
      ResolveEvent();
    }
  }

  public void Interact() {
    if (_hasBeenUsed) return;

    if (EventMenuScene == null) {
      GD.PrintErr("EventDevice: EventMenuScene is not set!");
      return;
    }

    if (!IsInstanceValid(_eventMenuInstance)) {
      _eventMenuInstance = EventMenuScene.Instantiate<EventMenu>();
      GetTree().Root.AddChild(_eventMenuInstance);
      _eventMenuInstance.OptionSelected += OnOptionSelected;
    }
    _eventMenuInstance.ShowMenu();
  }

  private void OnOptionSelected(int index) {
    var result = GameManager.Instance.ActiveEvent.ExecuteOption(index);
    HandleEventResult(result);
  }

  private void HandleEventResult(EventExecutionResult result) {
    switch (result) {
      case UpdateEvent _:
        _eventMenuInstance.UpdateDisplay();
        break;
      case FinishEvent _:
        GameManager.Instance.ActiveEvent.IsFinished = true;
        _eventMenuInstance.HideMenu();
        ResolveEvent();
        break;
      case StartCombat s:
        GameManager.Instance.SubCombatEnemies = s.Enemies;
        GameManager.Instance.SubCombatDifficultyMultiplier = s.DifficultyMultiplier;
        _eventMenuInstance.HideMenu();
        // 战斗结束后，需要一个机制来重新解决事件
        GameManager.Instance.StartSubCombat(GetTree().CurrentScene.SceneFilePath);
        GetTree().ChangeSceneToFile(CombatScenePath);
        break;
      case ShowUpgradeSelection s:
        _eventMenuInstance.HideMenu();
        var gainMenu = UpgradeSelectionMenuScene.Instantiate<UpgradeSelectionMenu>();
        GetTree().Root.AddChild(gainMenu);
        gainMenu.UpgradeSelectionFinished += OnUpgradeSelectionMenuFinished;
        gainMenu.StartUpgradeSelection(
          s.Mode,
          s.Picks,
          s.MinLevel,
          s.MaxLevel,
          s.ChoiceCount,
          _rng
        );
        break;
      default:
        throw new ArgumentException("Invalid event type");
    }
  }

  private void OnUpgradeSelectionMenuFinished() {
    var currentEvent = GameManager.Instance.ActiveEvent;

    // 强化选择结束后，检查事件是否还有后续步骤
    if (currentEvent.IsFinished) {
      _eventMenuInstance.HideMenu();
      ResolveEvent();
    } else {
      // 如果事件未结束（例如多阶段事件），重新显示并更新事件菜单
      _eventMenuInstance.ShowMenu();
    }
  }

  private void ResolveEvent() {
    _hasBeenUsed = true;
    SetHighlight(false);
    EmitSignal(SignalName.EventResolved);
  }

  public void SetHighlight(bool highlighted) {
    if (_label == null) return;
    _label.Modulate = (highlighted && !_hasBeenUsed) ? new Color(1.0f, 1.0f, 0.5f) : Colors.White;
  }

  public void Reset() {
    _hasBeenUsed = false;
    if (IsInstanceValid(_eventMenuInstance)) {
      _eventMenuInstance.HideMenu();
    }
    // 重新初始化事件，以重置其内部状态

    GameManager.Instance.ActiveEvent = (GameEvent) _initialEventState.DuplicateDeep();

    if (GameManager.Instance.ActiveEvent.IsFinished) {
      ResolveEvent();
    }

    GD.Print("EventDevice has been reset.");
  }
}
