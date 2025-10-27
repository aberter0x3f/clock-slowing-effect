using Godot;
using Rewind;

public class PortalState : RewindState { }

public partial class Portal : RewindableArea2D, IInteractable {
  [Export(PropertyHint.File, "*.tscn")]
  public string MapMenuScenePath { get; set; }

  [Export(PropertyHint.File, "*.tscn")]
  public string TitleScenePath { get; set; }

  private Label3D _label;
  private Node3D _visualizer;

  public override void _Ready() {
    base._Ready();
    _visualizer = GetNode<Node3D>("Visualizer");
    _label = _visualizer.GetNode<Label3D>("Label3D");

    _visualizer.GlobalPosition = new Vector3(
      GlobalPosition.X * GameConstants.WorldScaleFactor,
      GameConstants.GamePlaneY + 0.66f,
      GlobalPosition.Y * GameConstants.WorldScaleFactor
    );

    _visualizer.GetNode<AnimatedSprite3D>("AnimatedSprite3D").Play();
  }

  public void Interact() {
    // 根据本关表现计算得分
    var gm = GameManager.Instance;
    HexMap.ClearScore score;
    if (!gm.HadMissThisLevel && !gm.UsedSkillThisLevel && !gm.UsedSlowThisLevel) {
      score = HexMap.ClearScore.Perfect;
    } else if (!gm.HadMissThisLevel && !gm.UsedSkillThisLevel) {
      score = HexMap.ClearScore.NoMissNoSkill;
    } else if (!gm.HadMissThisLevel) {
      score = HexMap.ClearScore.NoMiss;
    } else {
      score = HexMap.ClearScore.StandardClear;
    }

    // 通知 GameManager 关卡已完成并传入分数
    GameManager.Instance.CompleteLevel(score);

    // 检查当前关卡是否为最终关卡
    bool isTargetNode = GameManager.Instance.SelectedMapPosition == GameManager.Instance.GameMap.TargetPosition;

    if (isTargetNode) {
      // 通关，返回主菜单
      GD.Print("Congratulations! Run completed. Returning to title screen.");
      GetTree().ChangeSceneToFile(TitleScenePath);
    } else {
      // 前往地图选择界面
      GetTree().ChangeSceneToFile(MapMenuScenePath);
    }
  }

  public void SetHighlight(bool highlighted) {
    if (_label == null) return;
    _label.Modulate = highlighted ? new Color(1.0f, 1.0f, 0.5f) : Colors.White;
  }

  public override RewindState CaptureState() {
    return new PortalState();
  }

  public override void RestoreState(RewindState state) {
  }
}
