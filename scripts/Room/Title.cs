using Curio;
using Godot;
using Rewind;
using UI;

namespace Room;

public partial class Title : Node {
  private Player _player;
  private Label _uiLabel;
  private MapGenerator _mapGenerator;
  private RewindManager _rewindManager;
  private Vector2 _playerSpawnPosition;
  private DifficultyMenu _difficultyMenu;
  private CurioSelectionMenu _curioSelectionMenu;

  [Export]
  public PackedScene DifficultyMenuScene { get; set; }
  [Export]
  public PackedScene CurioSelectionMenuScene { get; set; }

  [Export(PropertyHint.File, "*.tscn")]
  public string InterLevelMenuScenePath { get; set; }

  public override void _Ready() {
    GameRootProvider.CurrentGameRoot = this;

    _player = GetNode<Player>("Player");
    _uiLabel = GetNode<Label>("CanvasLayer/Label");
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

    _playerSpawnPosition = _mapGenerator.GenerateMap();
    _player.GlobalPosition = _playerSpawnPosition;
  }

  private void OnStartGameRequested() {
    _difficultyMenu?.ShowMenu();
  }

  private void OnDifficultySelected(DifficultySetting difficulty) {
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

  public override void _Process(double delta) {
    UpdateUILabelText();
  }

  private void UpdateUILabelText() {
    if (_player == null || _uiLabel == null) return;
    string ammoText;
    if (_player.IsReloading) {
      ammoText = $"Reloading: {_player.TimeToReloaded:F1}s";
    } else {
      ammoText = $"Ammo: {_player.CurrentAmmo} / {GameManager.Instance.PlayerStats.MaxAmmoInt}";
    }
    var bulletObjectCount = GetTree().GetNodesInGroup("bullets").Count;
    var rewindTimeLeft = _rewindManager.AvailableRewindTime;

    string curioText = "Curio: None";
    var activeCurio = GameManager.Instance.GetCurrentActiveCurio();
    if (activeCurio != null) {
      string cdText = activeCurio.CurrentCooldown > 0 ? $" (CD: {activeCurio.CurrentCooldown:F1}s)" : " (Ready)";
      curioText = $"Curio: {activeCurio.Name}{cdText}";
    }

    _uiLabel.Text = $"Time HP: {_player.Health:F2}\n" +
                    $"Time Scale: {TimeManager.Instance.TimeScale:F2}\n" +
                    $"Rewind Left: {rewindTimeLeft:F1}s\n" +
                    $"{ammoText}\n" +
                    $"{curioText}\n" +
                    $"Bullet object count: {bulletObjectCount}";
  }
}
