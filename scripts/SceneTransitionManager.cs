using Godot;

public partial class SceneTransitionManager : CanvasLayer {
  public static SceneTransitionManager Instance { get; private set; }

  [Signal]
  public delegate void IntroFinishedEventHandler();

  private ColorRect _colorRect;
  private ShaderMaterial _material;
  private Tween _tween;

  public override void _Ready() {
    Instance = this;

    _colorRect = GetNode<ColorRect>("ColorRect");
    _material = _colorRect.Material as ShaderMaterial;

    // 初始状态：完全透明（半径设大一点确保看不到黑边）
    _colorRect.Visible = false;
    SetRadius(2.0f);
  }

  /// <summary>
  /// 播放「进入」动画
  /// </summary>
  public async void PlayIntro(Vector3 worldFocusPos) {
    SetupShaderCenter(worldFocusPos);
    _colorRect.Visible = true;
    StartTween(0.0f, 1.0f, 0.5f);
    await ToSignal(_tween, Tween.SignalName.Finished);
    _colorRect.Visible = false;
    EmitSignal(SignalName.IntroFinished);
  }

  /// <summary>
  /// 播放「退出」动画
  /// </summary>
  public async void TransitionToScene(string scenePath, Vector3 worldFocusPos) {
    GetTree().Paused = true; // 暂停游戏防止玩家死亡
    SetupShaderCenter(worldFocusPos);
    _colorRect.Visible = true;
    StartTween(1.0f, 0.0f, 0.5f);
    await ToSignal(_tween, Tween.SignalName.Finished);
    // 切换场景
    GetTree().ChangeSceneToFile(scenePath);
    _colorRect.Visible = false;
    GetTree().Paused = false;
  }

  private void SetupShaderCenter(Vector3 worldPos) {
    var camera = GetViewport().GetCamera3D();

    // 将 3D 世界坐标转换为 2D 屏幕坐标
    Vector2 screenPos = camera.UnprojectPosition(worldPos);
    Rect2 viewportRect = GetViewport().GetVisibleRect();

    // 转换为 0-1 的 UV 坐标
    Vector2 uvCenter = screenPos / viewportRect.Size;
    // Y 轴在 Shader UV 中是向下增长的，Godot 屏幕坐标也是，所以不需要翻转

    // 计算长宽比传入 Shader
    float aspect = viewportRect.Size.X / viewportRect.Size.Y;

    _material.SetShaderParameter("center", uvCenter);
    _material.SetShaderParameter("aspect_ratio", aspect);
  }

  private void StartTween(float fromRadius, float toRadius, float duration) {
    if (_tween != null && _tween.IsValid()) _tween.Kill();

    _tween = CreateTween();
    _tween.SetPauseMode(Tween.TweenPauseMode.Process); // 确保在 Paused=true 时也能运行
    _tween.SetEase(Tween.EaseType.Out);
    _tween.SetTrans(Tween.TransitionType.Quad);

    // 这一步是为了确保 Shader 参数从正确的值开始
    _material.SetShaderParameter("radius", fromRadius);

    _tween.TweenMethod(Callable.From<float>(SetRadius), fromRadius, toRadius, duration);
  }

  private void SetRadius(float radius) {
    _material.SetShaderParameter("radius", radius);
  }
}
