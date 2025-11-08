using System.Linq;
using Curio;
using Enemy.Boss;
using Godot;
using Rewind;
using UI;

namespace Room;

public partial class BossCombat : Node {
  private Player _player;
  private MapGenerator _mapGenerator;
  private RewindManager _rewindManager;
  private PauseMenu _pauseMenu;
  private Portal _spawnedPortal = null;
  private Boss _boss;

  private ulong _levelSeed;
  private RandomNumberGenerator _upgradeRng;
  private RandomNumberGenerator _curioRng;

  [Export]
  public PackedScene PauseMenuScene { get; set; }
  [Export]
  public PackedScene PortalScene { get; set; }
  [Export]
  public PackedScene UpgradeSelectionMenuScene { get; set; }
  [Export]
  public PackedScene CurioSelectionMenuScene { get; set; } // 新增
  [Export]
  public PackedScene BossScene { get; set; }
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
      _pauseMenu.RestartFromPhaseRequested += OnRestartFromPhaseRequested;
    }

    _mapGenerator.GenerateMap();

    _player.DiedPermanently += OnPlayerDiedPermanently;
    _player.SpawnPosition = new Vector2(0, 300);
    _player.GlobalPosition = _player.SpawnPosition;

    _levelSeed = ((ulong) GD.Randi() << 32) | (ulong) GD.Randi();
    _upgradeRng = new RandomNumberGenerator();
    _upgradeRng.Seed = _levelSeed;
    _curioRng = new RandomNumberGenerator();
    _curioRng.Seed = _levelSeed + 1; // 使用不同的种子

    InitializeBoss();
  }

  private void InitializeBoss() {
    _boss = BossScene.Instantiate<Boss>();

    // 根据位面选择阶段组合
    var plane = GameManager.Instance.CurrentPlane;
    // `plane` 从 1 开始，所以我们需要 `(plane - 1)` 来得到从 0 开始的索引
    var setIndex = (plane - 1) % 3;
    Godot.Collections.Array<PackedScene> selectedPhases;

    switch (setIndex) {
      case 0: // 位面 1, 4, 7, ...
        selectedPhases = _boss.PhaseSet1;
        GD.Print($"Current plane is {plane}, selecting Boss Phase Set 1.");
        break;
      case 1: // 位面 2, 5, 8, ...
        selectedPhases = _boss.PhaseSet2;
        GD.Print($"Current plane is {plane}, selecting Boss Phase Set 2.");
        break;
      case 2: // 位面 3, 6, 9, ...
        selectedPhases = _boss.PhaseSet3;
        GD.Print($"Current plane is {plane}, selecting Boss Phase Set 3.");
        break;
      default: // 备用
        selectedPhases = _boss.PhaseSet1;
        GD.PrintErr($"Invalid plane set index {setIndex}, defaulting to Set 1.");
        break;
    }
    _boss.SetActivePhases(selectedPhases);

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
    upgradeMenu.UpgradeSelectionFinished += OnUpgradeSelectionFinished;
    upgradeMenu.StartUpgradeSelection(UpgradeSelectionMenu.Mode.Gain, 1, 3, 3, 3, _upgradeRng);
  }

  private void OnUpgradeSelectionFinished() {
    // 强化选完后，选择奇物
    var curioMenu = CurioSelectionMenuScene.Instantiate<CurioSelectionMenu>();
    AddChild(curioMenu);
    curioMenu.CurioSelectionFinished += OnCurioSelectionFinished;
    curioMenu.StartCurioSelection(3, _curioRng);
  }

  private void OnCurioSelectionFinished() {
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
    var tAxisCurio = GameManager.Instance.GetCurrentAndPendingCurios().FirstOrDefault(c => c.Type == CurioType.TAxisEnhancement);
    bool isRewindingWithCurio = tAxisCurio != null && (RewindManager.Instance.IsPreviewing || RewindManager.Instance.IsRewinding);

    if (IsInstanceValid(_boss) && _boss.InternalState == Boss.BossInternalState.Fighting && !_player.IsPermanentlyDead && !isRewindingWithCurio) {
      _player.Health -= (float) delta;
    }
  }

  private void OnRestartRequested() {
    GD.Print("Restarting level...");

    if (IsInstanceValid(_spawnedPortal)) {
      _spawnedPortal.QueueFree();
      _spawnedPortal = null;
    }

    _pauseMenu.EnablePhaseRestart = false;

    foreach (IRewindable node in GetTree().GetNodesInGroup("enemies").ToList()) {
      node.Destroy();
    }
    foreach (IRewindable node in GetTree().GetNodesInGroup("bullets").ToList()) {
      node.Destroy();
    }
    foreach (IRewindable node in GetTree().GetNodesInGroup("pickups").ToList()) {
      node.Destroy();
    }
    foreach (var node in GetTree().GetNodesInGroup("enemy_creations").ToList()) {
      node.QueueFree();
    }

    _player.ResetState();
    _rewindManager.ResetHistory();

    InitializeBoss();

    TimeManager.Instance.SetCurrentGameTime(0.0);

    // 使用之前保存的种子重新初始化 RNG，以保证强化选项不变
    _upgradeRng = new RandomNumberGenerator();
    _upgradeRng.Seed = _levelSeed;
    _curioRng = new RandomNumberGenerator();
    _curioRng.Seed = _levelSeed + 1;

    GameManager.Instance?.RestartLevel();
  }

  private void OnRestartFromPhaseRequested() {
    _boss.RestartFromCurrentPhase();
  }
}
