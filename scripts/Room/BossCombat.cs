using System.Linq;
using Enemy.Boss;
using Godot;
using Rewind;
using UI;

namespace Room;

public partial class BossCombat : Node {
  private Player _player;
  private Label _uiLabel;
  private MapGenerator _mapGenerator;
  private RewindManager _rewindManager;
  private PauseMenu _pauseMenu;
  private Portal _spawnedPortal = null;
  private Boss _boss;

  private ulong _levelSeed;
  private RandomNumberGenerator _upgradeRng;

  [Export]
  public PackedScene PauseMenuScene { get; set; }
  [Export]
  public PackedScene PortalScene { get; set; }
  [Export]
  public PackedScene UpgradeSelectionMenuScene { get; set; }
  [Export]
  public PackedScene BossScene { get; set; }
  [Export(PropertyHint.File, "*.tscn")]
  public string InterLevelMenuScenePath { get; set; }

  public override void _Ready() {
    GameRootProvider.CurrentGameRoot = this;

    _player = GetNode<Player>("Player");
    _uiLabel = GetNode<Label>("CanvasLayer/Label");
    _mapGenerator = GetNode<MapGenerator>("MapGenerator");
    _rewindManager = GetNode<RewindManager>("RewindManager");

    if (PauseMenuScene != null) {
      _pauseMenu = PauseMenuScene.Instantiate<PauseMenu>();
      AddChild(_pauseMenu);
      _pauseMenu.RestartRequested += OnRestartRequested;
      _pauseMenu.RestartFromPhaseRequested += OnRestartFromPhaseRequested;
    }

    _mapGenerator.GenerateMap();

    _player.DiedPermanently += OnPlayerDiedPermanently;
    _player.SpawnPosition = new Vector2(0, 300);
    _player.GlobalPosition = _player.SpawnPosition;

    _levelSeed = ((ulong) GD.Randi() << 32) | (ulong) GD.Randi();
    _upgradeRng = new RandomNumberGenerator();
    _upgradeRng.Seed = _levelSeed;

    _boss = BossScene.Instantiate<Boss>();
    _boss.Died += OnBossDefeated;
    _boss.FightingPhaseStarted += () => _pauseMenu.EnablePhaseRestart = true;
    _boss.FightingPhaseEnded += () => _pauseMenu.EnablePhaseRestart = false;
    AddChild(_boss);
  }

  private void OnBossDefeated(float difficulty) {
    GD.Print("BossRoom scene received Boss Defeated signal. Spawning portal.");
    if (PortalScene == null) {
      GD.PrintErr("PortalScene is not set in the BossRoom scene!");
      return;
    }
    if (!IsInstanceValid(_spawnedPortal)) {
      Vector2I centerCell = new Vector2I(_mapGenerator.MapWidth / 2, _mapGenerator.MapHeight / 2);
      _spawnedPortal = PortalScene.Instantiate<Portal>();
      _spawnedPortal.GlobalPosition = _mapGenerator.MapToWorld(centerCell);
      _spawnedPortal.LevelCompleted += OnLevelCompleted;
      GameRootProvider.CurrentGameRoot.CallDeferred(Node.MethodName.AddChild, _spawnedPortal);
    }
  }

  private void OnLevelCompleted(HexMap.ClearScore score) {
    // Boss 战固定奖励一个 3 级强化
    var upgradeMenu = UpgradeSelectionMenuScene.Instantiate<UpgradeSelectionMenu>();
    AddChild(upgradeMenu);
    upgradeMenu.UpgradeSelectionFinished += () => OnUpgradeSelectionFinished();
    upgradeMenu.StartUpgradeSelection(UpgradeSelectionMenu.Mode.Gain, 1, 3, 3, 3, _upgradeRng);
  }

  private void OnUpgradeSelectionFinished() {
    // Boss 战固定为标准通关
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

  public override void _Process(double delta) {
    if (IsInstanceValid(_boss) && _boss.InternalState == Boss.BossInternalState.Fighting) {
      _player.Health -= (float) delta;
    }
    UpdateUILabelText();
  }

  private void OnRestartRequested() {
    GD.Print("Restarting level...");

    if (IsInstanceValid(_spawnedPortal)) {
      _spawnedPortal.QueueFree();
      _spawnedPortal = null;
    }

    _player.ResetState();
    _rewindManager.ResetHistory();
    _boss.RestartFromStart();

    _pauseMenu.EnablePhaseRestart = false;

    foreach (var node in GetTree().GetNodesInGroup("bullets").ToList()) {
      node.QueueFree();
    }
    foreach (var node in GetTree().GetNodesInGroup("pickups").ToList()) {
      node.QueueFree();
    }

    TimeManager.Instance.SetCurrentGameTime(0.0);

    // 使用之前保存的种子重新初始化 RNG，以保证强化选项不变
    _upgradeRng = new RandomNumberGenerator();
    _upgradeRng.Seed = _levelSeed;

    GameManager.Instance?.RestartLevel();
  }

  private void OnRestartFromPhaseRequested() {
    _boss.RestartFromCurrentPhase();
  }

  private void UpdateUILabelText() {
    if (_player == null || _uiLabel == null) return;
    string ammoText = _player.IsReloading ? $"Reloading: {_player.TimeToReloaded:F1}s" : $"Ammo: {_player.CurrentAmmo} / {GameManager.Instance.PlayerStats.MaxAmmoInt}";
    var rewindTimeLeft = _rewindManager.AvailableRewindTime;
    var timeBondText = $"Time Bond: {GameManager.Instance.TimeBond:F1}s";
    var trackedObjectCount = RewindManager.Instance.TrackedObjectCount;

    _uiLabel.Text = $"Time HP: {_player.Health:F2}\n" +
                    $"{timeBondText}\n" +
                    $"Rewind Left: {rewindTimeLeft:F1}s\n" +
                    $"{ammoText}\n" +
                    $"Tracked object count: {trackedObjectCount}";
  }
}
