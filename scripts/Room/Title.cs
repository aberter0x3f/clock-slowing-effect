using Curio;
using Enemy.Boss;
using Godot;
using Rewind;
using UI;

namespace Room;

public partial class Title : Node {
  private Player _player;
  private MapGenerator _mapGenerator;
  private RewindManager _rewindManager;
  private Vector3 _playerSpawnPosition;
  private DifficultyMenu _difficultyMenu;
  private CurioSelectionMenu _curioSelectionMenu;
  private BossPracticeSelectionMenu _bossPracticeMenu;
  private DifficultySetting _selectedBossPracticeDifficulty;
  private bool _isStartingBossPractice = false;

  [Export]
  public PackedScene DifficultyMenuScene { get; set; }
  [Export]
  public PackedScene CurioSelectionMenuScene { get; set; }
  [Export]
  public PackedScene BossPracticeMenuScene { get; set; }
  [Export]
  public BossPhaseData BossPhaseDataResource { get; set; } // Boss 阶段数据资源
  [Export(PropertyHint.File, "*.tscn")]
  public string BossScenePath { get; set; }

  [Export(PropertyHint.File, "*.tscn")]
  public string InterLevelMenuScenePath { get; set; }

  public override void _Ready() {
    GameRootProvider.CurrentGameRoot = this;

    _player = GetNode<Player>("Player");
    _mapGenerator = GetNode<MapGenerator>("MapGenerator");
    _rewindManager = GetNode<RewindManager>("RewindManager");

    // 实例化并设置难度菜单
    if (DifficultyMenuScene != null) {
      _difficultyMenu = DifficultyMenuScene.Instantiate<DifficultyMenu>();
      AddChild(_difficultyMenu);
      _difficultyMenu.DifficultySelected += OnDifficultySelected;
    } else {
      GD.PrintErr("Title scene is missing the DifficultyMenuScene reference.");
    }

    // 找到「开始游戏」按钮并连接其信号
    var startGameButton = GetNode<TitleMenuInteractable>("StartGame");
    if (startGameButton != null) {
      startGameButton.StartGameRequested += OnStartGameRequested;
    } else {
      GD.PrintErr("Could not find 'StartGame' interactable node in Title scene.");
    }

    // 找到「Boss 练习」按钮并连接其信号
    var bossPracticeButton = GetNode<TitleMenuInteractable>("BossPractice");
    if (bossPracticeButton != null) {
      bossPracticeButton.BossPracticeRequested += OnBossPracticeRequested;
    } else {
      GD.PrintErr("Could not find 'BossPractice' interactable node in Title scene.");
    }

    _playerSpawnPosition = _mapGenerator.GenerateMap();
    _player.GlobalPosition = _playerSpawnPosition;
  }

  private void OnStartGameRequested() {
    _isStartingBossPractice = false;
    _difficultyMenu?.ShowMenu();
  }

  private void OnBossPracticeRequested() {
    _isStartingBossPractice = true;
    _difficultyMenu?.ShowMenu();
  }

  private void OnDifficultySelected(DifficultySetting difficulty) {
    if (_isStartingBossPractice) {
      _selectedBossPracticeDifficulty = difficulty;
      _isStartingBossPractice = false;

      if (BossPracticeMenuScene != null && BossPhaseDataResource != null) {
        _bossPracticeMenu = BossPracticeMenuScene.Instantiate<BossPracticeSelectionMenu>();
        AddChild(_bossPracticeMenu);
        _bossPracticeMenu.PhaseSelected += OnBossPhaseSelected;
        _bossPracticeMenu.MenuCancelled += () => { _selectedBossPracticeDifficulty = null; };
        _bossPracticeMenu.ShowMenu(BossPhaseDataResource);
      } else {
        GD.PrintErr("BossPracticeMenuScene or BossPhaseDataResource is not set in Title scene.");
      }
    } else {
      // 正常游戏开始流程
      GameManager.Instance.InitializeNewRun(difficulty);

      // 显示初始奇物选择菜单
      if (CurioSelectionMenuScene != null) {
        _curioSelectionMenu = CurioSelectionMenuScene.Instantiate<CurioSelectionMenu>();
        AddChild(_curioSelectionMenu);
        _curioSelectionMenu.CurioSelectionFinished += OnStartingCurioSelected;
        // 从数据库中获取所有可作为开局的奇物
        var startingCurios = new Godot.Collections.Array<BaseCurio>(GameManager.Instance.CurioDb.AllCurios)
          .Slice(0, GameManager.Instance.CurioDb.StartingCurioCount);
        _curioSelectionMenu.StartCurioSelection(startingCurios, new RandomNumberGenerator());
      } else {
        GD.PrintErr("CurioSelectionMenuScene is not set. Skipping starting curio selection.");
        StartGame();
      }
    }
  }

  private void OnBossPhaseSelected(int plane, int phaseIndex) {
    if (_selectedBossPracticeDifficulty == null) {
      GD.PrintErr("Cannot start boss practice: difficulty not selected.");
      return;
    }
    GameManager.Instance.InitializeBossPractice(_selectedBossPracticeDifficulty, plane, phaseIndex);
    GetTree().ChangeSceneToFile(BossScenePath);
  }

  private void OnStartingCurioSelected() {
    GameManager.Instance.CommitPendingCurios();
    StartGame();
  }

  private void StartGame() {
    if (string.IsNullOrEmpty(InterLevelMenuScenePath)) {
      GD.PrintErr("MapMenuScenePath is not set in the Title scene inspector.");
      return;
    }
    GetTree().ChangeSceneToFile(InterLevelMenuScenePath);
  }
}
