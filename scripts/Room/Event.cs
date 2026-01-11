using Godot;
using Rewind;
using UI;

namespace Room;

public partial class Event : Node {
  private Player _player;
  private MapGenerator _mapGenerator;
  private RewindManager _rewindManager;
  private PauseMenu _pauseMenu;
  private Vector3 _playerSpawnPosition;
  private Portal _spawnedPortal;
  private EventDevice _eventDevice;

  private ulong _levelSeed;

  [Export]
  public PackedScene PauseMenuScene { get; set; }
  [Export]
  public PackedScene PortalScene { get; set; }
  [Export]
  public PackedScene EventDeviceScene { get; set; }
  [Export]
  public PackedScene EventMenuScene { get; set; }
  [Export]
  public PackedScene UpgradeSelectionMenuScene { get; set; }
  [Export(PropertyHint.File, "*.tscn")]
  public string InterLevelMenuScenePath { get; set; }
  [Export(PropertyHint.File, "*.tscn")]
  public string CombatScenePath { get; set; }

  public override void _Ready() {
    GameRootProvider.CurrentGameRoot = this;

    _player = GetNode<Player>("Player");
    _mapGenerator = GetNode<MapGenerator>("MapGenerator");
    _rewindManager = GetNode<RewindManager>("RewindManager");

    if (PauseMenuScene != null) {
      _pauseMenu = PauseMenuScene.Instantiate<PauseMenu>();
      AddChild(_pauseMenu);
      _pauseMenu.RestartRequested += OnRestartRequested;
    }
    _player.DiedPermanently += OnPlayerDiedPermanently;

    _playerSpawnPosition = _mapGenerator.GenerateMap();
    _player.GlobalPosition = _playerSpawnPosition;

    _levelSeed = ((ulong) GD.Randi() << 32) | (ulong) GD.Randi();

    GameManager.Instance?.StartLevel();

    SpawnEventDevice();
    // 传送门在事件解决后生成

    SceneTransitionManager.Instance.PlayIntro(_player.GlobalPosition);
  }

  private void SpawnEventDevice() {
    if (EventDeviceScene == null) {
      GD.PrintErr("EventDeviceScene is not set!");
      return;
    }
    _eventDevice = EventDeviceScene.Instantiate<EventDevice>();
    Vector2I centerCell = new Vector2I(_mapGenerator.MapWidth / 2 - 4, _mapGenerator.MapHeight / 2);
    _eventDevice.Position = _mapGenerator.MapToWorld(centerCell);
    _eventDevice.EventMenuScene = EventMenuScene;
    _eventDevice.UpgradeSelectionMenuScene = UpgradeSelectionMenuScene;
    _eventDevice.CombatScenePath = CombatScenePath;
    _eventDevice.LevelSeed = _levelSeed;
    _eventDevice.EventResolved += OnEventResolved;
    AddChild(_eventDevice);
  }

  private void OnEventResolved() {
    GD.Print("Event has been resolved. Spawning portal.");
    SpawnPortal();
  }

  private void SpawnPortal() {
    if (PortalScene == null) {
      GD.PrintErr("PortalScene is not set!");
      return;
    }
    Vector2I portalCell = new Vector2I(_mapGenerator.MapWidth / 2 + 4, _mapGenerator.MapHeight / 2);
    _spawnedPortal = PortalScene.Instantiate<Portal>();
    _spawnedPortal.Position = _mapGenerator.MapToWorld(portalCell);
    _spawnedPortal.LevelCompleted += OnLevelCompleted;
    AddChild(_spawnedPortal);
  }

  private void OnLevelCompleted(HexMap.ClearScore score) {
    GameManager.Instance.CompleteLevel(HexMap.ClearScore.StandardClear);
    SceneTransitionManager.Instance.TransitionToScene(InterLevelMenuScenePath, _player.GlobalPosition);
  }

  private void OnPlayerDiedPermanently() {
    if (_pauseMenu != null) {
      _pauseMenu.ShowMenu(true);
    }
  }

  public override void _Input(InputEvent @event) {
    if (_pauseMenu != null && !_pauseMenu.Visible && @event.IsActionPressed("ui_cancel")) {
      GetViewport().SetInputAsHandled();
      _pauseMenu.ShowMenu(false);
    }
  }

  private void OnRestartRequested() {
    GD.Print("Restarting Event level...");

    _player.ResetState();
    _rewindManager.ResetHistory();
    _eventDevice.Reset();

    if (IsInstanceValid(_spawnedPortal)) {
      _spawnedPortal.QueueFree();
      _spawnedPortal = null;
    }

    GameManager.Instance?.RestartLevel();
  }
}
