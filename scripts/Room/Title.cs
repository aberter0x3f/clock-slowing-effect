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
  private WeaponSelectionMenu _weaponMenu;
  private DifficultySetting _selectedBossPracticeDifficulty;
  private bool _isStartingBossPractice = false;

  [Export]
  public PackedScene DifficultyMenuScene { get; set; }
  [Export]
  public PackedScene CurioSelectionMenuScene { get; set; }
  [Export]
  public PackedScene BossPracticeMenuScene { get; set; }
  [Export]
  public PackedScene WeaponMenuScene { get; set; }

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

    _difficultyMenu = DifficultyMenuScene.Instantiate<DifficultyMenu>();
    AddChild(_difficultyMenu);
    _difficultyMenu.DifficultySelected += OnDifficultySelected;

    _weaponMenu = WeaponMenuScene.Instantiate<WeaponSelectionMenu>();
    AddChild(_weaponMenu);
    _weaponMenu.WeaponChanged += OnWeaponChanged;

    var startGameButton = GetNode<TitleMenuInteractable>("StartGame");
    startGameButton.StartGameRequested += OnStartGameRequested;

    var bossPracticeButton = GetNode<TitleMenuInteractable>("BossPractice");
    bossPracticeButton.BossPracticeRequested += OnBossPracticeRequested;

    var weaponButton = GetNode<TitleMenuInteractable>("SelectWeapon");
    weaponButton.WeaponSelectionRequested += OnWeaponMenuRequested;

    _playerSpawnPosition = _mapGenerator.GenerateMap();
    _player.GlobalPosition = _playerSpawnPosition;

    SceneTransitionManager.Instance.PlayIntro(_player.GlobalPosition);
  }

  private void OnStartGameRequested() {
    _isStartingBossPractice = false;
    _difficultyMenu?.ShowMenu();
  }

  private void OnBossPracticeRequested() {
    _isStartingBossPractice = true;
    _difficultyMenu?.ShowMenu();
  }

  private void OnWeaponMenuRequested() {
    _weaponMenu?.ShowMenu();
  }

  private void OnWeaponChanged() {
    _player.RefreshWeapon();
    _rewindManager.ResetHistory();
  }

  private void OnDifficultySelected(DifficultySetting difficulty) {
    if (_isStartingBossPractice) {
      _selectedBossPracticeDifficulty = difficulty;
      _isStartingBossPractice = false;

      _bossPracticeMenu = BossPracticeMenuScene.Instantiate<BossPracticeSelectionMenu>();
      AddChild(_bossPracticeMenu);
      _bossPracticeMenu.PhaseSelected += OnBossPhaseSelected;
      _bossPracticeMenu.MenuCancelled += () => { _selectedBossPracticeDifficulty = null; };
      _bossPracticeMenu.ShowMenu(BossPhaseDataResource);
    } else {
      GameManager.Instance.InitializeNewRun(difficulty);

      _curioSelectionMenu = CurioSelectionMenuScene.Instantiate<CurioSelectionMenu>();
      AddChild(_curioSelectionMenu);
      _curioSelectionMenu.CurioSelectionFinished += OnStartingCurioSelected;
      var startingCurios = new Godot.Collections.Array<BaseCurio>(GameManager.Instance.CurioDb.AllCurios)
        .Slice(0, GameManager.Instance.CurioDb.StartingCurioCount);
      _curioSelectionMenu.StartCurioSelection(startingCurios, new RandomNumberGenerator());
    }
  }

  private void OnBossPhaseSelected(int plane, int phaseIndex) {
    if (_selectedBossPracticeDifficulty == null) {
      GD.PrintErr("Cannot start boss practice: difficulty not selected.");
      return;
    }
    GameManager.Instance.InitializeBossPractice(_selectedBossPracticeDifficulty, plane, phaseIndex);
    SceneTransitionManager.Instance.TransitionToScene(BossScenePath, _player.GlobalPosition);
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
    SceneTransitionManager.Instance.TransitionToScene(InterLevelMenuScenePath, _player.GlobalPosition);
  }
}
