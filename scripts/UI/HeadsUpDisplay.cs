using Curio;
using Godot;
using Rewind;

namespace UI;

public partial class HeadsUpDisplay : CanvasLayer {
  [Export]
  public CompressedTexture2D RecordIcon { get; set; }
  [Export]
  public CompressedTexture2D RewindIcon { get; set; }

  // 左上角
  private TextureRect _statusIcon;
  private Label _rewindTimeLabel;
  private Label _maxRewindTimeLabel;
  private Label _healthLabel;
  private Label _maxHealthLabel;
  private Control _timeBondContainer;
  private Label _timeBondLabel;

  // 右上角
  private Label _ammoLabel;
  private Label _maxAmmoLabel;
  private Label _ammoLabelS;
  private HBoxContainer _skillContainer;
  private Label _skillCooldownLabel;
  private Label _skillLabels;
  private Label _curioNameLabel;

  // 游戏对象引用
  private Player _player;
  private GameManager _gameManager;
  private RewindManager _rewindManager;

  public override void _Ready() {
    // 取节点引用
    _statusIcon = GetNode<TextureRect>("TopLeftContainer/RewindTimeContainer/StatusIcon");
    _rewindTimeLabel = GetNode<Label>("TopLeftContainer/RewindTimeContainer/RewindTimeLabel");
    _maxRewindTimeLabel = GetNode<Label>("TopLeftContainer/RewindTimeContainer/MaxRewindTimeLabel");
    _healthLabel = GetNode<Label>("TopLeftContainer/HealthContainer/HealthLabel");
    _maxHealthLabel = GetNode<Label>("TopLeftContainer/HealthContainer/MaxHealthLabel");
    _timeBondContainer = GetNode<Control>("TopLeftContainer/HealthContainer/TimeBondContainer");
    _timeBondLabel = _timeBondContainer.GetNode<Label>("TimeBondLabel");
    _ammoLabel = GetNode<Label>("TopRightContainer/AmmoContainer/AmmoLabel");
    _maxAmmoLabel = GetNode<Label>("TopRightContainer/AmmoContainer/MaxAmmoLabel");
    _ammoLabelS = GetNode<Label>("TopRightContainer/AmmoContainer/LabelS");
    _skillContainer = GetNode<HBoxContainer>("TopRightContainer/SkillContainer");
    _skillCooldownLabel = _skillContainer.GetNode<Label>("HBoxContainer/SkillCooldownLabel");
    _skillLabels = _skillContainer.GetNode<Label>("HBoxContainer/LabelS");
    _curioNameLabel = _skillContainer.GetNode<Label>("TextureRect/CurioNameLabel");

    CallDeferred(nameof(FetchGameReferences));
  }

  /// <summary>
  /// 延迟获取游戏核心对象的引用，以避免 _Ready 中的时序问题．
  /// </summary>
  private void FetchGameReferences() {
    _gameManager = GameManager.Instance;
    _rewindManager = RewindManager.Instance;
    _player = GetTree().Root.GetNode<Player>("GameRoot/Player");

    if (_player == null) {
      GD.PrintErr("HeadsUpDisplay: Player node not found at 'GameRoot/Player'.");
    }
  }

  public override void _Process(double delta) {
    UpdateRewindTime();
    UpdateHealth();
    UpdateAmmo();
    UpdateCurio();
  }

  /// <summary>
  /// 更新生命值显示．
  /// </summary>
  private void UpdateHealth() {
    _healthLabel.Text = _player.Health.ToString("F1");
    _maxHealthLabel.Text = "/" + _gameManager.PlayerStats.MaxHealth.ToString("F1");
    if (_gameManager.TimeBond > 0) {
      _timeBondContainer.Visible = true;
      _timeBondLabel.Text = _gameManager.TimeBond.ToString("F1");
    } else {
      _timeBondContainer.Visible = false;
    }
  }

  /// <summary>
  /// 更新回溯时间显示．
  /// </summary>
  private void UpdateRewindTime() {
    _statusIcon.Texture = (_rewindManager.IsPreviewing || _rewindManager.IsRewinding) ? RewindIcon : RecordIcon;
    _rewindTimeLabel.Text = _rewindManager.AvailableRewindTime.ToString("F1");
    _maxRewindTimeLabel.Text = "/" + _rewindManager.MaxRecordTime.ToString("F1");
  }

  /// <summary>
  /// 更新弹药或换弹状态显示．
  /// </summary>
  private void UpdateAmmo() {
    if (_player.IsReloading) {
      _ammoLabel.Text = "RLD";
      _maxAmmoLabel.Text = _player.TimeToReloaded.ToString("F1");
      _ammoLabelS.Visible = true;
    } else {
      _ammoLabel.Text = _player.CurrentAmmo.ToString();
      _maxAmmoLabel.Text = "/" + _gameManager.PlayerStats.MaxAmmoInt.ToString();
      _ammoLabelS.Visible = false;
    }
  }

  /// <summary>
  /// 更新当前奇物（技能）的状态显示．
  /// </summary>
  private void UpdateCurio() {
    BaseCurio activeCurio = _gameManager.GetCurrentActiveCurio();

    if (activeCurio != null) {
      _skillContainer.Visible = true;
      // 截取名字的首字母作为图标文字
      _curioNameLabel.Text = activeCurio.Name.Length > 0 ? activeCurio.Name.Substring(0, 1) : "?";

      if (activeCurio.CurrentCooldown > 0) {
        _skillCooldownLabel.Text = activeCurio.CurrentCooldown.ToString("F1");
        _skillLabels.Visible = true;
        _skillContainer.Modulate = Colors.White;
      } else {
        _skillCooldownLabel.Text = "OK";
        _skillLabels.Visible = false;
        _skillContainer.Modulate = Colors.Yellow;
      }
    } else {
      _skillContainer.Visible = false;
    }
  }
}
