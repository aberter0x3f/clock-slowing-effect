using Godot;

namespace Bullet;

public partial class PhaseArcsAndLinesBullet : BaseBullet3D {
  // --- 路径参数 ---
  public Vector3 BasePosition { get; set; }
  public float BaseAngle { get; set; }
  public float PrimaryRadius { get; set; }
  public float SecondaryRadius { get; set; }
  public float QuarterArcDuration { get; set; }
  public float SecondaryArcDuration { get; set; }
  public float FinalLinearSpeed { get; set; }
  public bool InvertHorizontalPlaneY { get; set; } = false;

  // --- 坐标系基向量 ---
  private Vector3 _verticalPlaneX;
  private Vector3 _verticalPlaneY;
  private Vector3 _horizontalPlaneX;
  private Vector3 _horizontalPlaneY;

  // --- 阶段结束时间点 ---
  private float _phase1EndTime;
  private float _phase2EndTime;
  private float _phase3EndTime;
  private float _phase4EndTime;

  public override void _Ready() {
    base._Ready();
    InitializeParameters();
  }

  private void InitializeParameters() {
    // 预计算坐标系的基向量
    _verticalPlaneX = new Vector3(Mathf.Cos(BaseAngle), Mathf.Sin(BaseAngle), 0);
    _verticalPlaneY = new Vector3(0, 0, 1);
    _horizontalPlaneX = _verticalPlaneX;
    _horizontalPlaneY = new Vector3(-Mathf.Sin(BaseAngle), Mathf.Cos(BaseAngle), 0);

    // 预计算各阶段的结束时间点
    _phase1EndTime = QuarterArcDuration;
    _phase2EndTime = _phase1EndTime + 2 * QuarterArcDuration;
    _phase3EndTime = _phase2EndTime + QuarterArcDuration;
    _phase4EndTime = _phase3EndTime + SecondaryArcDuration;
  }

  protected override void UpdatePosition(double delta) {
    // 位置完全由 _timeAlive 决定，以确保回溯的准确性
    // delta 参数在此处不被使用

    Vector2 posInPlane;
    bool useVerticalPlane = true;

    if (_timeAlive <= _phase1EndTime) {
      // 阶段 1: 第一个 1/4 圆弧
      float progress = _timeAlive / QuarterArcDuration;
      float angle = -Mathf.Pi / 2 + progress * Mathf.Pi / 2;
      posInPlane = new Vector2(
        PrimaryRadius * Mathf.Sin(progress * Mathf.Pi / 2),
        PrimaryRadius - PrimaryRadius * Mathf.Cos(progress * Mathf.Pi / 2)
      );
    } else if (_timeAlive <= _phase2EndTime) {
      // 阶段 2: 上半圆弧
      float timeInPhase = _timeAlive - _phase1EndTime;
      float progress = timeInPhase / (2 * QuarterArcDuration);
      float angle = progress * Mathf.Pi;
      var center = new Vector2(PrimaryRadius / 2, PrimaryRadius);
      var radius = PrimaryRadius / 2;
      posInPlane = center + new Vector2(radius * Mathf.Cos(angle), radius * Mathf.Sin(angle));
    } else if (_timeAlive <= _phase3EndTime) {
      // 阶段 3: 第二个 1/4 圆弧
      float timeInPhase = _timeAlive - _phase2EndTime;
      float progress = timeInPhase / QuarterArcDuration;
      float angle = Mathf.Pi + progress * Mathf.Pi / 2;
      var center = new Vector2(PrimaryRadius, PrimaryRadius);
      posInPlane = center + new Vector2(PrimaryRadius * Mathf.Cos(angle), PrimaryRadius * Mathf.Sin(angle));
    } else if (_timeAlive <= _phase4EndTime) {
      // 阶段 4: 第三个 1/4 圆弧 (在水平面上)
      useVerticalPlane = false;
      float timeInPhase = _timeAlive - _phase3EndTime;
      float progress = timeInPhase / SecondaryArcDuration;
      float angle = -Mathf.Pi / 2 + progress * Mathf.Pi / 2;
      var center = new Vector2(PrimaryRadius, SecondaryRadius);
      posInPlane = center + new Vector2(SecondaryRadius * Mathf.Cos(angle), SecondaryRadius * Mathf.Sin(angle));
    } else {
      // 阶段 5: 匀速直线运动 (在水平面上)
      useVerticalPlane = false;
      float timeInPhase = _timeAlive - _phase4EndTime;
      var startPos = new Vector2(PrimaryRadius + SecondaryRadius, SecondaryRadius);
      var direction = new Vector2(0, 1);
      posInPlane = startPos + direction * FinalLinearSpeed * timeInPhase;
    }

    // 将平面坐标转换为 3D 判定坐标系中的位置
    if (useVerticalPlane) {
      RawPosition = BasePosition + _verticalPlaneX * posInPlane.X + _verticalPlaneY * posInPlane.Y;
    } else {
      var horizontalY = _horizontalPlaneY;
      if (InvertHorizontalPlaneY) {
        horizontalY = -horizontalY;
      }
      RawPosition = BasePosition + _horizontalPlaneX * posInPlane.X + horizontalY * posInPlane.Y;
    }
  }
}
