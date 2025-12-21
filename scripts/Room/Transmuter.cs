using Godot;
using Rewind;
using UI;

namespace Room;

public partial class Transmuter : Node {
  private Player _player;
  private MapGenerator _mapGenerator;
  private RewindManager _rewindManager;
  private PauseMenu _pauseMenu;
  private Vector3 _playerSpawnPosition;
  private Portal _spawnedPortal;
  private TransmuterDevice _transmuterDevice;

  private ulong _levelSeed;

  [Export]
  public PackedScene PauseMenuScene { get; set; }
  [Export]
  public PackedScene PortalScene { get; set; }
  [Export]
  public PackedScene TransmuterDeviceScene { get; set; }
  [Export]
  public PackedScene TransmuterMenuScene { get; set; }
  [Export]
  public PackedScene UpgradeSelectionMenuScene { get; set; }
  [Export(PropertyHint.File, "*.tscn")]
  public string InterLevelMenuScenePath { get; set; }

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

    SpawnTransmuterDevice();
    SpawnPortal();
  }

  private void SpawnTransmuterDevice() {
    if (TransmuterDeviceScene == null) {
      GD.PrintErr("TransmuterDeviceScene is not set!");
      return;
    }
    _transmuterDevice = TransmuterDeviceScene.Instantiate<TransmuterDevice>();
    Vector2I centerCell = new Vector2I(_mapGenerator.MapWidth / 2 - 4, _mapGenerator.MapHeight / 2);
    _transmuterDevice.Position = _mapGenerator.MapToWorld(centerCell);
    _transmuterDevice.TransmuterMenuScene = TransmuterMenuScene;
    _transmuterDevice.UpgradeSelectionMenuScene = UpgradeSelectionMenuScene;
    _transmuterDevice.LevelSeed = _levelSeed;
    AddChild(_transmuterDevice);
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
    GetTree().ChangeSceneToFile(InterLevelMenuScenePath);
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
    GD.Print("Restarting Transmuter level...");

    _player.ResetState();
    _rewindManager.ResetHistory();
    _transmuterDevice.Reset();
    GameManager.Instance?.RestartLevel();
  }
}
