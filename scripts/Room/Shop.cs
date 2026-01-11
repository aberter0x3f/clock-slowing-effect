using System.Collections.Generic;
using System.Linq;
using Godot;
using Rewind;
using UI;

namespace Room;

public partial class Shop : Node {
  private Player _player;
  private MapGenerator _mapGenerator;
  private RewindManager _rewindManager;
  private PauseMenu _pauseMenu;
  private Vector3 _playerSpawnPosition;
  private Portal _spawnedPortal;
  private ShopDevice _shopDevice;

  private ulong _levelSeed;
  private RandomNumberGenerator _shopRng;
  private List<(Upgrade, float)> _shopInventory; // (强化, 价格)

  [Export]
  public PackedScene PauseMenuScene { get; set; }
  [Export]
  public PackedScene PortalScene { get; set; }
  [Export]
  public PackedScene ShopDeviceScene { get; set; }
  [Export]
  public PackedScene ShopMenuScene { get; set; }
  [Export(PropertyHint.File, "*.tscn")]
  public string InterLevelMenuScenePath { get; set; }

  [ExportGroup("Shop Configuration")]
  [Export] public int MaxPurchases { get; set; } = 1;
  [Export] public int Level3Count { get; set; } = 2;
  [Export] public int Level2Count { get; set; } = 4;
  [Export] public int Level1Count { get; set; } = 6;
  [Export] public float Level1Cost { get; set; } = 25f;
  [Export] public float Level2Cost { get; set; } = 50f;
  [Export] public float Level3Cost { get; set; } = 90f;

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

    // 初始化本关卡的随机种子和 RNG
    _levelSeed = ((ulong) GD.Randi() << 32) | (ulong) GD.Randi();
    _shopRng = new RandomNumberGenerator();
    _shopRng.Seed = _levelSeed;

    GameManager.Instance?.StartLevel();

    GenerateShopInventory();
    SpawnShopDevice();
    SpawnPortal();
    SceneTransitionManager.Instance.PlayIntro(_player.GlobalPosition);
  }

  private void GenerateShopInventory() {
    _shopInventory = new List<(Upgrade, float)>();
    var gm = GameManager.Instance;
    var currentlyOwned = gm.GetCurrentAndPendingUpgrades();
    var allUpgrades = gm.UpgradeDb.AllUpgrades
      .Where(u => !currentlyOwned.Contains(u))
      .ToList();

    // 辅助函数，用于获取指定等级的强化
    void AddUpgrades(int level, int count, float cost) {
      var candidates = allUpgrades.Where(u => u.Level == level).ToList();
      // 如果数量不足，用低等级的替补
      int currentLevel = level - 1;
      while (candidates.Count < count && currentLevel > 0) {
        candidates.AddRange(allUpgrades.Where(u => u.Level == currentLevel));
        --currentLevel;
      }
      // 打乱并取出所需数量
      candidates = candidates.OrderBy(_ => _shopRng.Randf()).Take(count).ToList();
      foreach (var upgrade in candidates) {
        _shopInventory.Add((upgrade, cost));
      }
    }

    AddUpgrades(3, Level3Count, Level3Cost);
    AddUpgrades(2, Level2Count, Level2Cost);
    AddUpgrades(1, Level1Count, Level1Cost);

    // 排序：等级降序，然后名称升序
    _shopInventory = _shopInventory
      .OrderByDescending(item => item.Item1.Level)
      .ThenBy(item => item.Item1.Name)
      .ToList();
  }

  private void SpawnShopDevice() {
    if (ShopDeviceScene == null) {
      GD.PrintErr("ShopDeviceScene is not set!");
      return;
    }
    _shopDevice = ShopDeviceScene.Instantiate<ShopDevice>();
    // 放置在地图中心附近
    Vector2I centerCell = new Vector2I(_mapGenerator.MapWidth / 2 - 4, _mapGenerator.MapHeight / 2);
    _shopDevice.Position = _mapGenerator.MapToWorld(centerCell);
    _shopDevice.ShopMenuScene = ShopMenuScene;
    _shopDevice.Inventory = _shopInventory;
    _shopDevice.MaxPurchases = MaxPurchases;
    AddChild(_shopDevice);
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
    // 商店和交换器房间总是以 StandardClear 完成
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
    GD.Print("Restarting Shop level...");

    // 重置玩家状态
    _player.ResetState();
    _rewindManager.ResetHistory();

    // 重置商店设备的状态（例如购买次数）
    _shopDevice.Reset();

    // 重置 GameManager 中的待定项目（强化和债券）
    // 这将撤销在本关卡中进行的所有购买
    GameManager.Instance?.RestartLevel();
  }
}
