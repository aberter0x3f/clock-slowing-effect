using Godot;

public partial class InvertRingEffect : CanvasLayer {
  private ColorRect _colorRect;
  private ShaderMaterial _material;

  public override void _Ready() {
    _colorRect = GetNode<ColorRect>("ColorRect");

    if (_colorRect.Material is ShaderMaterial originalMaterial) {
      // 复制材质资源，确保每个 InvertRingEffect 实例都有自己独立的材质状态．
      // 这样可以防止上一次动画的最终值（例如半径）影响到下一次动画的初始状态．
      _material = (ShaderMaterial) originalMaterial.Duplicate();
      _colorRect.Material = _material;
    } else {
      GD.PrintErr("InvertRingEffect: ShaderMaterial not found on ColorRect.");
    }
  }

  public void StartEffect(Vector3 worldPosition) {
    float duration = 0.5f;
    float thickness = 0.1f;

    if (_material == null) {
      GD.PrintErr("DeathRingEffect: ShaderMaterial not found.");
      QueueFree();
      return;
    }

    // 获取主摄像机和视口尺寸，用于将世界坐标转换为着色器所需的 UV 坐标
    var camera = GetViewport().GetCamera3D();
    if (camera == null) {
      GD.PrintErr("DeathRingEffect: No 3D camera found in viewport.");
      QueueFree();
      return;
    }

    var viewportSize = GetViewport().GetVisibleRect().Size;
    if (viewportSize.X == 0 || viewportSize.Y == 0) {
      QueueFree();
      return;
    }

    // 将 3D 世界坐标投影到 2D 屏幕坐标
    Vector2 screenPoint = camera.UnprojectPosition(worldPosition);
    // 将屏幕坐标转换为 UV 坐标 (0-1范围)
    Vector2 uv = screenPoint / viewportSize;

    // 设置着色器的 uniform 变量
    _material.SetShaderParameter("center", uv);
    _material.SetShaderParameter("aspect_ratio", viewportSize.X / viewportSize.Y);

    // 创建一个 Tween 来动画化环的半径
    var tween = CreateTween();
    tween.SetIgnoreTimeScale(true);
    tween.SetPauseMode(Tween.TweenPauseMode.Process);
    tween.SetEase(Tween.EaseType.Out); // 让扩散看起来更自然
    tween.SetTrans(Tween.TransitionType.Cubic);

    // 动画化外环半径，从 0 扩散到足以覆盖屏幕的 1.2
    tween.TweenProperty(_material, "shader_parameter/outer_radius", 1.2f, duration).From(0.0f);
    // 并行动画化内环半径，使其跟随外环，形成一个固定厚度的环
    tween.TweenProperty(_material, "shader_parameter/inner_radius", 1.2f - thickness, duration).From(0.0f - thickness);

    // 动画结束后，自动销毁此效果节点
    tween.Finished += QueueFree;
  }
}
