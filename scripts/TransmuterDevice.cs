using Godot;
using UI;

public partial class TransmuterDevice : Area2D, IInteractable {
  private Label3D _label;
  private Node3D _visualizer;
  private TransmuterMenu _transmuterMenuInstance;
  private bool _hasBeenUsed = false;

  public PackedScene TransmuterMenuScene { get; set; }
  public PackedScene UpgradeSelectionMenuScene { get; set; }
  public ulong LevelSeed { get; set; }

  public override void _Ready() {
    _visualizer = GetNode<Node3D>("Visualizer");
    _label = _visualizer.GetNode<Label3D>("Label3D");

    _visualizer.GlobalPosition = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY + 0.2f,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );
  }

  public void Interact() {
    if (TransmuterMenuScene == null) {
      GD.PrintErr("TransmuterDevice: TransmuterMenuScene is not set!");
      return;
    }

    if (!IsInstanceValid(_transmuterMenuInstance)) {
      _transmuterMenuInstance = TransmuterMenuScene.Instantiate<TransmuterMenu>();
      GetTree().Root.AddChild(_transmuterMenuInstance);
      _transmuterMenuInstance.TransmutationRequested += OnTransmutationRequested;
    }
    // 总是打开菜单，并将当前使用状态传递进去
    _transmuterMenuInstance.Popup(_hasBeenUsed);
  }

  private void OnTransmutationRequested(Godot.Collections.Array<Upgrade> discardedUpgrades, int targetLevel) {
    _hasBeenUsed = true;
    SetHighlight(false);

    // 从玩家身上移除旧强化
    var gm = GameManager.Instance;
    foreach (var upgrade in discardedUpgrades) {
      gm.RemoveUpgrade(upgrade);
    }

    // 计算新种子
    ulong combinedHash = LevelSeed;
    foreach (var upgrade in discardedUpgrades) {
      ulong expandedHash = ExpandHash((uint) upgrade.Name.GetHashCode());
      combinedHash ^= expandedHash;
    }
    var rng = new RandomNumberGenerator();
    rng.Seed = combinedHash;

    // 弹出强化选择菜单
    var upgradeMenu = UpgradeSelectionMenuScene.Instantiate<UpgradeSelectionMenu>();
    GetTree().Root.AddChild(upgradeMenu);
    upgradeMenu.UpgradeSelectionFinished += () => {
      // 结束后，重新计算玩家属性
      gm.PlayerStats.RecalculateStats(gm.GetCurrentAndPendingUpgrades(), gm.CurrentPlayerHealth);
    };
    upgradeMenu.StartUpgradeSelection(UpgradeSelectionMenu.Mode.Gain, 1, targetLevel, targetLevel, 3, rng);
  }

  private ulong ExpandHash(uint hash) {
    ulong result = hash;
    // 一个简单的位移和乘法混合，将 32 位扩展到 64 位并增加随机性
    result = (result * 0xbf58476d1ce4e5b9L) ^ (result >> 27);
    return result;
  }

  public void SetHighlight(bool highlighted) {
    if (_label == null) return;
    _label.Modulate = (highlighted && !_hasBeenUsed) ? new Color(1.0f, 1.0f, 0.5f) : Colors.White;
  }

  /// <summary>
  /// 重置交换器状态．
  /// </summary>
  public void Reset() {
    // 重置交互设备本身，允许玩家再次使用它．
    // 之前发生的强化变更将在 GameManager.RestartLevel() 中被撤销．
    _hasBeenUsed = false;
    if (IsInstanceValid(_transmuterMenuInstance)) {
      _transmuterMenuInstance.CloseMenu();
    }
    GD.Print("TransmuterDevice has been reset.");
  }
}
