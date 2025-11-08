using Godot;
using Rewind;

public class PortalState : RewindState { }

public partial class Portal : RewindableArea2D, IInteractable {
  [Signal]
  public delegate void LevelCompletedEventHandler(HexMap.ClearScore score);

  [ExportGroup("Sound Effects")]
  [Export]
  public AudioStream EnterSound { get; set; }

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
    SoundManager.Instance.PlaySoundEffect(EnterSound);

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

    // 发出信号，让 Combat 场景处理后续流程
    EmitSignal(SignalName.LevelCompleted, (int) score);

    // 禁用交互，防止重复触发
    SetProcess(false);
    SetHighlight(false);
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
